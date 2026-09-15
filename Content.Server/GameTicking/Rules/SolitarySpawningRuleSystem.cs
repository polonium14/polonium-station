// SPDX-FileCopyrightText: 2025 Errant <35878406+Errant-4@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.GameTicking.Prototypes;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Preferences.Managers;
using Content.Shared._Polonium.Tutorial;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Rules;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Station.Components;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Rules;

// TODO Integration test

/// <summary>
/// This system overrides the normal spawn process, and puts each player on their own personal map.
/// </summary>
/// <remarks>
/// Currently, this always targets every player.
/// The main station will still spawn, but no one will ever be on it. As such, when this game rule is in use,
/// the server should be forced to use the 'Empty' map, to avoid spawning a bunch of unnecessary entities and active mobs
/// </remarks>
public sealed class SolitarySpawningSystem : GameRuleSystem<SolitarySpawningRuleComponent>
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    // A list of the station entities generated for each player (and the map they are on).
    // Used for respawning players on their own station, and for deleting unused maps.
    private readonly Dictionary<ICommonSession, (EntityUid, MapId)> _stations = [];
    private readonly HashSet<NetUserId> _pendingLobbyJoins = [];

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
    }

    public bool TryJoinFromLobby(ICommonSession session)
    {
        if (GameTicker.UserHasJoinedGame(session))
            return false;

        var preset = GameTicker.CurrentPreset ?? GameTicker.Preset;
        if (preset?.ID != "Tutorial")
            return false;

        if (GameTicker.RunLevel == GameRunLevel.PreRoundLobby)
        {
            GameTicker.ToggleReady(session, true);
            GameTicker.StartRound();

            if (GameTicker.UserHasJoinedGame(session))
                return true;

            // timer already kicking off StartRound, wait and spawn after
            if (GameTicker.RunLevel == GameRunLevel.PreRoundLobby)
            {
                _pendingLobbyJoins.Add(session.UserId);
                return true;
            }
        }

        if (GameTicker.RunLevel != GameRunLevel.InRound)
            return false;

        return TryRestartTutorial(session);
    }

    public bool TryRestartTutorial(ICommonSession session)
    {
        if (!TryGetActivePrototype(out var proto))
            return false;

        var profile = _prefs.GetPreferencesOrNull(session.UserId)?.SelectedCharacter as HumanoidCharacterProfile
                      ?? HumanoidCharacterProfile.Random();

        _mind.WipeMind(session);

        if (_stations.Remove(session, out var stored))
        {
            var (station, mapId) = stored;
            _map.DeleteMap(mapId);
            if (!Deleted(station))
                Del(station);
        }

        if (!CreateSolitaryStation(session, profile, proto, out var stationTarget))
            return false;

        SpawnPlayer(session, profile, proto.Job, stationTarget.Value, proto.WelcomeLoc, proto.TutorialFlow);
        return true;
    }

    /// <summary>
    /// A player is trying to enter the round, or is respawning
    /// </summary>
    private void OnBeforeSpawn(PlayerBeforeSpawnEvent args)
    {
        var session = args.Player;
        var active = false;

        // Check if any Solitary Spawning rules are running
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out var comp, out _))
        {
            // TODO check blacklists/whitelists from the gamerule

            // Only need to report failure if the player was covered under any active rules
            active = true;

            if (comp.Prototypes.Count == 0)
            {
                Log.Warning("No prototypes were included in SolitarySpawningRuleComponent");
                continue;
            }

            ProtoId<SolitarySpawningPrototype>? playerChoice = null;

            //TODO query SolitarySpawningManager which option the player picked when joining

            if (playerChoice is null || !comp.Prototypes.Contains(playerChoice.Value))
            {
                Log.Warning($"Received invalid player choice from '{session}'. Player chose '{playerChoice}'. " +
                            $"Defaulting to first option: '{comp.Prototypes.First().Id}'");

                playerChoice = comp.Prototypes.First();
            }

            if (!_proto.TryIndex(playerChoice, out var proto))
            {
                Log.Warning($"Solitary spawning failed for {session} - prototype '{playerChoice}' does not exist");
                continue;
            }
            Log.Debug($"Solitary spawning prototype '{playerChoice}' selected for {session}");

            var job = proto.Job;

            if (RequestExistingStation(session, out var stationExist))
            {
                Log.Debug($"Existing solitary station found for {session}. Not creating a new map.");
                SpawnPlayer(session, args.Profile, job, stationExist.Value, null, proto.TutorialFlow);
                args.Handled = true;
                break;
            }

            if (!CreateSolitaryStation(session, args.Profile, proto, out var stationTarget))
                continue;

            SpawnPlayer(session, args.Profile, job, stationTarget.Value, proto.WelcomeLoc, proto.TutorialFlow);
            args.Handled = true;
            break;
        }

        // If any SolitarySpawningRules are active, it is likely a notable malfunction if a player spawns normally
        if (active && args.Handled == false)
            Log.Warning($"Solitary spawning failed for {session.Name}, spawning on the normal map.");
    }

    private bool TryGetActivePrototype([NotNullWhen(true)] out SolitarySpawningPrototype? proto)
    {
        proto = null;
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out var comp, out _))
        {
            if (comp.Prototypes.Count == 0)
                continue;

            if (_proto.TryIndex(comp.Prototypes.First(), out proto))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Create a personal map for one player
    /// </summary>
    private bool CreateSolitaryStation(
        ICommonSession session,
        HumanoidCharacterProfile profile,
        SolitarySpawningPrototype prototype,
        [NotNullWhen(true)] out EntityUid? stationTarget)
    {
        stationTarget = null;
        var proto = prototype.Map;

        if (!_proto.TryIndex(proto, out var map))
        {
            Log.Error($"Solitary spawning failed for {session} - Invalid map prototype: {proto}");
            return false;
        }

        // Create the new map and station, and assign them identifiable names
        var stationName = Loc.GetString("solitary-station-name", ("character", profile.Name));
        var mapName = Loc.GetString("solitary-map-name", ("character", profile.Name));
        var query = GameTicker.LoadGameMap(map, out var mapId, stationName: stationName);
        var newMap = query.First();
        _meta.SetEntityName(Transform(newMap).ParentUid, mapName);

        _map.InitializeMap(mapId);

        if (!TryComp<StationMemberComponent>(newMap, out var member))
        {
            Log.Error($"Solitary spawning failed for {session} - Target station not found");
            return false;
        }

        stationTarget = member.Station;

        //store the newly created station entity and map for this session, for respawn and cleanup purposes
        _stations[session] = (stationTarget.Value, mapId);
        return true;
    }

    /// <summary>
    /// Spawn the player and their gear
    /// </summary>
    private void SpawnPlayer(
        ICommonSession session,
        HumanoidCharacterProfile? humanoid,
        ProtoId<JobPrototype> jobId,
        EntityUid station,
        LocId? message,
        ProtoId<Content.Shared._Polonium.Tutorial.Prototypes.TutorialFlowPrototype>? tutorialFlow)
    {
        if (humanoid is null)
        {
            Log.Error($"Solitary spawning failed for {session} - Player has no character profile");
            return;
        }

        GameTicker.DoSpawn(session, humanoid, station, jobId, true, out var mob, out _, out var jobName);

        // Latejoin is not a relevant concept for solitary spawns - the station did not even exist beforehand
        // Also, round flow does not exist in the regular sense on a tutorial server
        // So all spawns are recorded as just "Joined"
        _adminLogger.Add(LogType.RoundStartJoin,
            LogImpact.Medium,
            $"Player {session.Name} has spawned on a solitary map. Joined as {humanoid.Name:characterName} on station {Name(station):stationName} with {ToPrettyString(mob):entity} as a {jobName:jobName}.");

        if (message is not null)
            _chatManager.DispatchServerMessage(session, Loc.GetString(message));

        // let the tutorial system deal with this, not our problem
        if (tutorialFlow is { } flow)
            RaiseLocalEvent(new TutorialStartRequestedEvent(mob, flow));
    }

    /// <summary>
    /// Checks if a player already has a station allocated to them.
    /// </summary>
    private bool RequestExistingStation(ICommonSession session, [NotNullWhen(true)] out EntityUid? station)
    {
        station = null;

        if (!_stations.TryGetValue(session, out var stored))
            return false;

        station = stored.Item1;
        return true;
    }

    private void OnRoundStarted(RoundStartedEvent args)
    {
        foreach (var userId in _pendingLobbyJoins)
        {
            if (!_player.TryGetSessionById(userId, out var session))
                continue;

            if (GameTicker.UserHasJoinedGame(session))
                continue;

            TryRestartTutorial(session);
        }

        _pendingLobbyJoins.Clear();
    }

    /// Clear the saved station list, since the maps are being deleted
    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _stations.Clear();
        _pendingLobbyJoins.Clear();
    }

    private void MapCleanup()
    {
        // TODO map cleanup x minutes after the player left the server, or if they go back to the lobby
    }
}
