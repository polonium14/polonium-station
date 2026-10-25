using System.Linq;
using Content.Server._Polonium.Audio;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared._Polonium.Audio;
using Content.Shared.Audio;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.EvacScenarios;

public sealed partial class EvacScenarioSystem : GameRuleSystem<EvacScenarioComponent>
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private AudioPreloadSystem _preload = default!;

    public bool RollingEnabled { get; private set; }

    /// <summary>
    /// Whether an admin chose the scenario of this round's evacuation instead of leaving it to chance.
    /// </summary>
    public bool IsForced { get; private set; }

    /// <summary>
    /// The scenario an admin chose, where null means a plain flight. Only used when <see cref="IsForced"/> is set.
    /// </summary>
    public EntProtoId? ForcedScenario { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.EvacScenariosEnabled, value => RollingEnabled = value, true);

        SubscribeLocalEvent<EvacShuttlesLaunchingEvent>(OnShuttlesLaunching);
        SubscribeLocalEvent<FTLStartedEvent>(OnFTLStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        ClearForcedScenario();
    }

    public void ForceScenario(EntProtoId? scenario)
    {
        IsForced = true;
        ForcedScenario = scenario;
    }

    public void ClearForcedScenario()
    {
        IsForced = false;
        ForcedScenario = null;
    }

    /// <summary>
    /// Every evac flight scenario, in a stable order.
    /// </summary>
    public IEnumerable<(EntityPrototype Proto, EvacScenarioComponent Scenario)> GetScenarios()
    {
        foreach (var proto in ProtoMan.EnumeratePrototypes<EntityPrototype>().OrderBy(p => p.ID))
        {
            if (proto.Abstract || !proto.TryComp<EvacScenarioComponent>(out var scenario, Factory))
                continue;

            yield return (proto, scenario);
        }
    }

    public float GetChance(EvacScenarioComponent scenario)
    {
        return GetChance(scenario, GetRoundRules());
    }

    private static float GetChance(EvacScenarioComponent scenario, HashSet<string> roundRules)
    {
        foreach (var entry in scenario.RuleChances)
        {
            if (entry.Rules.Any(rule => roundRules.Contains(rule.Id)))
                return entry.Chance;
        }

        return scenario.Chance;
    }

    /// <summary>
    /// IDs of every game rule added this round.
    /// </summary>
    private HashSet<string> GetRoundRules()
    {
        var rules = new HashSet<string>();
        var query = EntityQueryEnumerator<GameRuleComponent, MetaDataComponent>();
        while (query.MoveNext(out _, out var meta))
        {
            if (meta.EntityPrototype is { } proto)
                rules.Add(proto.ID);
        }

        return rules;
    }

    private EntProtoId? RollScenario()
    {
        if (!RollingEnabled)
            return null;

        var roundRules = GetRoundRules();
        var roll = RobustRandom.NextFloat();

        foreach (var (proto, scenario) in GetScenarios())
        {
            var chance = GetChance(scenario, roundRules);
            if (roll < chance)
                return proto.ID;

            roll -= chance;
        }

        return null;
    }

    private void OnShuttlesLaunching(ref EvacShuttlesLaunchingEvent ev)
    {
        if ((IsForced ? ForcedScenario : RollScenario()) is not { } id)
            return;

        var rule = GameTicker.AddGameRule(id);
        if (!TryComp<EvacScenarioComponent>(rule, out var scenario))
        {
            Log.Error($"{id} was picked as an evac flight scenario but has no {nameof(EvacScenarioComponent)}");
            GameTicker.EndGameRule(rule);
            return;
        }

        var stations = EntityQueryEnumerator<StationEmergencyShuttleComponent>();
        while (stations.MoveNext(out var station))
        {
            if (station.EmergencyShuttle is { } shuttle && Exists(shuttle))
                scenario.Shuttles.Add(shuttle);
        }

        if (scenario.FlightDuration is { } duration)
            ev.TransitTime = (float) (scenario.StartDelay + duration).TotalSeconds;

        _adminLog.Add(LogType.EventStarted,
            LogImpact.High,
            $"Evac flight scenario {id} {(IsForced ? "forced by an admin" : "rolled")} for {scenario.Shuttles.Count} emergency shuttle(s)");

        GameTicker.StartGameRule(rule);
    }

    private void OnFTLStarted(ref FTLStartedEvent ev)
    {
        var query = EntityQueryEnumerator<EvacScenarioComponent, ActiveGameRuleComponent>();
        while (query.MoveNext(out var scenario, out _))
        {
            if (scenario.DepartedAt != null || !scenario.Shuttles.Contains(ev.Entity))
                continue;

            scenario.DepartedAt = Timing.CurTime;
            PreloadMusic(scenario);
        }
    }

    protected override void Started(EntityUid uid, EvacScenarioComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        component.Announcements.Sort((a, b) => a.Time.CompareTo(b.Time));
    }

    protected override void ActiveTick(EntityUid uid, EvacScenarioComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (!TryGetFlightTime((uid, component), out var flightTime))
            return;

        if (!component.Started)
        {
            component.Started = true;
            PlayMusic(component);
        }

        while (component.NextAnnouncement < component.Announcements.Count
               && component.Announcements[component.NextAnnouncement].Time <= flightTime)
        {
            Announce(component, component.Announcements[component.NextAnnouncement]);
            component.NextAnnouncement++;
        }
    }

    protected override void Ended(EntityUid uid, EvacScenarioComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        if (component.Music != null && component.Started && GameTicker.RunLevel == GameRunLevel.InRound)
            RaiseNetworkEvent(new StopStationEventMusic(StationEventMusicType.EvacScenario), Filter.Broadcast());
    }

    public IReadOnlySet<EntityUid> GetShuttles(EvacScenarioComponent scenario)
    {
        return scenario.Shuttles;
    }

    /// <summary>
    /// How long ago the scenario started, <see cref="EvacScenarioComponent.StartDelay"/> after the jump to hyperspace.
    /// False until then.
    /// </summary>
    public bool TryGetFlightTime(Entity<EvacScenarioComponent?> rule, out TimeSpan flightTime)
    {
        flightTime = default;
        if (!Resolve(rule, ref rule.Comp, false) || rule.Comp.DepartedAt is not { } departed)
            return false;

        flightTime = Timing.CurTime - departed - rule.Comp.StartDelay;
        return flightTime >= TimeSpan.Zero;
    }

    private void PreloadMusic(EvacScenarioComponent scenario)
    {
        if (scenario.Music == null)
            return;

        scenario.ResolvedMusic = _preload.Preload(scenario.Music,
            Filter.Empty().AddWhere(GameTicker.UserHasJoinedGame),
            PreloadAudioKind.StationEventMusic);
    }

    private void PlayMusic(EvacScenarioComponent scenario)
    {
        if (scenario.Music == null)
            return;

        var filter = Filter.Empty().AddWhere(GameTicker.UserHasJoinedGame);

        RaiseNetworkEvent(new StopStationEventMusic(StationEventMusicType.EvacScenario), filter);

        RaiseNetworkEvent(new StationEventMusicEvent(scenario.ResolvedMusic ?? _audio.ResolveSound(scenario.Music),
                StationEventMusicType.EvacScenario,
                scenario.Music.Params),
            filter);
    }

    private void Announce(EvacScenarioComponent scenario, EvacScenarioAnnouncement announcement)
    {
        var message = Loc.GetString(announcement.Message);
        var sender = announcement.Sender is { } senderId ? Loc.GetString(senderId) : null;
        var playSound = announcement.Sound != null;

        if (!announcement.ShuttleOnly)
        {
            _chat.DispatchGlobalAnnouncement(message, sender, playSound, announcement.Sound, announcement.Color);
            return;
        }

        var filter = Filter.Empty();
        foreach (var shuttle in scenario.Shuttles)
        {
            if (Exists(shuttle))
                filter.AddInGrid(shuttle, EntityManager);
        }

        _chat.DispatchFilteredAnnouncement(filter,
            message,
            sender: sender,
            playSound: playSound,
            announcementSound: announcement.Sound,
            colorOverride: announcement.Color);
    }
}
