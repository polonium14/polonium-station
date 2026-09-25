using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Shared._Polonium.NewLife;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Components;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.NewLife;

/// <summary>
/// Lets a dead player go back to the lobby and join the running round as a brand new character, once they have spent <see cref="CCVars.NewLifeDelay"/> as a ghost.
/// </summary>
public sealed partial class NewLifeRuleSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedRoleSystem _roles = default!;

    private readonly Dictionary<NetUserId, HashSet<string>> _previousCharacters = new();
    private readonly Dictionary<NetUserId, int> _usedLives = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeNetworkEvent<NewLifeRequestEvent>(OnNewLifeRequest);
    }

    public bool Enabled => _cfg.GetCVar(CCVars.NewLifeEnabled);

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (!TryComp<GhostComponent>(ev.Entity, out var ghost) || HasComp<NewLifeComponent>(ev.Entity))
            return;

        // admin ghosts
        if (ghost.CanGhostInteract)
            return;

        // observers who never had a character have nothing to start over from.
        // The attach happens before the observer role is added and before the mind records its first body,
        // so a fresh observer shows up here with no original entity at all.
        if (!_mind.TryGetMind(ev.Player, out var mindId, out var mind)
            || mind.OriginalOwnedEntity == null
            || mind.OriginalOwnedEntity == GetNetEntity(ev.Entity)
            || _roles.MindHasRole<ObserverRoleComponent>(mindId))
        {
            return;
        }

        var sinceDeath = _timing.RealTime - ghost.TimeOfDeath;

        if (sinceDeath < TimeSpan.Zero)
            sinceDeath = TimeSpan.Zero;

        var comp = AddComp<NewLifeComponent>(ev.Entity);

        comp.GhostedAt = _timing.CurTime - sinceDeath;
        comp.UsedLives = _usedLives.GetValueOrDefault(ev.Player.UserId);

        Dirty(ev.Entity, comp);
    }

    private void OnNewLifeRequest(NewLifeRequestEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        if (!Enabled || _ticker.RunLevel != GameRunLevel.InRound)
            return;

        if (session.AttachedEntity is not { } ghost || !TryComp<NewLifeComponent>(ghost, out var comp))
            return;

        var delay = TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.NewLifeDelay));

        if (_timing.CurTime < comp.GhostedAt + delay)
            return;

        var used = _usedLives.GetValueOrDefault(session.UserId);

        var max = _cfg.GetCVar(CCVars.NewLifeMaxNewLives);

        if (max > 0 && used >= max)
            return;

        _usedLives[session.UserId] = used + 1;

        var characterName = _mind.TryGetMind(session, out _, out var mind) ? mind.CharacterName : null;
        if (!string.IsNullOrWhiteSpace(characterName))
        {
            if (!_previousCharacters.TryGetValue(session.UserId, out var names))
                _previousCharacters[session.UserId] = names = [];

            names.Add(characterName);
        }

        _adminLog.Add(LogType.Respawn,
            LogImpact.Medium,
            $"{session:player} took new life #{used + 1}, leaving {ToPrettyString(ghost):entity} (was {characterName ?? "unknown"}).");

        // lands in the lobby through PlayerJoinLobby, which sends the previous characters over
        _ticker.Respawn(session);
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        if (!_previousCharacters.TryGetValue(ev.PlayerSession.UserId, out var names))
            return;

        RaiseNetworkEvent(new NewLifePreviousCharactersEvent(new List<string>(names)), ev.PlayerSession);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _previousCharacters.Clear();
        _usedLives.Clear();
    }
}
