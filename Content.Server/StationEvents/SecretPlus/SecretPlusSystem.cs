// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Ilya246 <57039557+Ilya246@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Ilya246 <ilyukarno@gmail.com>
// SPDX-FileCopyrightText: 2025 SX-7 <sn1.test.preria.2002@gmail.com>
// SPDX-FileCopyrightText: 2026 Damian Zieliński <zientasek.pl@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Goobstation.CCVar;
using Content.Server._Goobstation.StationEvents.Components;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.Antag.Selectors;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.StationEvents.Components;
using Content.Shared.Database;
using Content.Shared.Destructible.Thresholds;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Random.Helpers;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.StationEvents.SecretPlus;

public sealed class SelectedEvent
{
    public readonly EntityPrototype Proto;
    public readonly GameRuleComponent RuleComp;
    public readonly StationEventComponent? EvComp;

    public SelectedEvent(EntityPrototype proto, GameRuleComponent ruleComp, StationEventComponent? evComp = null)
    {
        Proto = proto;
        RuleComp = ruleComp;
        EvComp = evComp;
    }
}

public sealed class PlayerCount
{
    public int Players;
    public int Ghosts;
}

[UsedImplicitly]
public sealed partial class SecretPlusSystem : GameRuleSystem<SecretPlusComponent>
{
    [Dependency] private AntagSelectionSystem _antagSelection = default!;
    [Dependency] private EventManagerSystem _event = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private GameTicker _ticker = default!;

    private float _minimumTimeUntilFirstEvent;
    private float _roundstartChaosScoreMultiplier;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _log.GetSawmill("secret_plus");

        Subs.CVar(_cfg, GoobCVars.MinimumTimeUntilFirstEvent, value => _minimumTimeUntilFirstEvent = value, true);
        Subs.CVar(_cfg, GoobCVars.RoundstartChaosScoreMultiplier, value => _roundstartChaosScoreMultiplier = value, true);
    }

    protected override void Added(EntityUid uid, SecretPlusComponent scheduler, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        var totalPlayers = GetTotalPlayerCount(_playerManager.Sessions);
        scheduler.ChaosScore =
            -_random.NextFloat(scheduler.MinStartingChaos * totalPlayers, scheduler.MaxStartingChaos * totalPlayers) *
            _roundstartChaosScoreMultiplier;

        var roll = _random.NextFloat();
        roll = MathF.Pow(roll, scheduler.ChaosChangeVariationExponent);
        scheduler.ChaosChangeVariation = MathHelper.Lerp(1f,
            _random.Prob(0.5f) ? scheduler.ChaosChangeVariationMin : scheduler.ChaosChangeVariationMax,
            roll);
        LogMessage($"Using chaos change multiplier of {scheduler.ChaosChangeVariation}");

        TrySpawnRoundstartAntags((uid, scheduler));
        if (TryComp<SelectedGameRulesComponent>(uid, out var selectedRules))
            SetupEvents((uid, scheduler), CountActivePlayers(), selectedRules);
        else
            SetupEvents((uid, scheduler), CountActivePlayers());
    }

    private void SetupEvents(Entity<SecretPlusComponent> scheduler, PlayerCount count, SelectedGameRulesComponent? selectedRules = null)
    {
        scheduler.Comp.SelectedEvents.Clear();

        if (selectedRules != null)
            SelectFromTable(scheduler, count, selectedRules);
        else
            SelectFromAllEvents(scheduler, count);
    }

    private void SelectFromAllEvents(Entity<SecretPlusComponent> scheduler, PlayerCount count)
    {
        foreach (var proto in _ticker.GetAllGameRulePrototypes())
        {
            if (!proto.TryGetComponent<GameRuleComponent>(out var gameRule, _factory)
                || !proto.TryGetComponent<StationEventComponent>(out var stationEvent, _factory))
                continue;

            if (scheduler.Comp.DisallowedEvents.Contains(stationEvent.EventType)
                || (!scheduler.Comp.IgnoreTimings
                    && !_event.CanRun(proto, stationEvent, count.Players, _ticker.RoundDuration(), 1f / GetRamping(scheduler))))
                continue;

            scheduler.Comp.SelectedEvents.Add(new SelectedEvent(proto, gameRule, stationEvent));
        }
    }

    private void SelectFromTable(Entity<SecretPlusComponent> scheduler, PlayerCount count, SelectedGameRulesComponent? selectedRules)
    {
        if (selectedRules == null)
            return;

        var available = _event.AvailableEvents(
            scheduler.Comp.IgnoreTimings,
            scheduler.Comp.IgnoreTimings ? int.MaxValue : null,
            scheduler.Comp.IgnoreTimings ? TimeSpan.MaxValue : null,
            1f / GetRamping(scheduler));

        if (!_event.TryBuildLimitedEvents(selectedRules.ScheduledGameRules, available, out var possibleEvents))
            return;

        foreach (var entry in possibleEvents)
        {
            var proto = entry.Key;
            var stationEvent = entry.Value;
            if (!proto.TryGetComponent<GameRuleComponent>(out var gameRule, _factory))
                continue;

            if (scheduler.Comp.DisallowedEvents.Contains(stationEvent.EventType))
                continue;

            scheduler.Comp.SelectedEvents.Add(new SelectedEvent(proto, gameRule, stationEvent));
        }
    }

    protected override void ActiveTick(EntityUid uid, SecretPlusComponent scheduler, GameRuleComponent gameRule, float frameTime)
    {
        var count = CountActivePlayers();
        var ramp = GetRamping((uid, scheduler));
        var speedup = _event.EventSpeedup;
        var mult = scheduler.ChaosChangeVariation;

        scheduler.ChaosScore += count.Players * scheduler.LivingChaosChange * frameTime * ramp * speedup * mult;
        scheduler.ChaosScore += count.Ghosts * scheduler.DeadChaosChange * frameTime * speedup * mult;

        var currTime = _timing.CurTime;
        if (currTime < scheduler.TimeNextEvent)
            return;

        if (scheduler.TimeNextEvent == TimeSpan.Zero)
        {
            var time = _minimumTimeUntilFirstEvent / speedup;
            scheduler.TimeNextEvent = _timing.CurTime + TimeSpan.FromSeconds(time);
            LogMessage($"Started, first event in {time} seconds");
            return;
        }

        var amt = TimeSpan.FromSeconds(_random.NextDouble(scheduler.EventIntervalMin.TotalSeconds, scheduler.EventIntervalMax.TotalSeconds) / ramp / speedup);
        scheduler.TimeNextEvent = currTime + amt;

        LogMessage($"Chaos score: {scheduler.ChaosScore}, Next event at: {_ticker.RoundDuration() + amt} (ramping {ramp})");

        if (TryComp<SelectedGameRulesComponent>(uid, out var selectedRules))
            SetupEvents((uid, scheduler), count, selectedRules);
        else
            SetupEvents((uid, scheduler), count);

        var selectedEvent = ChooseEvent((uid, scheduler));
        if (selectedEvent != null)
            StartRule((uid, scheduler), selectedEvent.Proto.ID, false);
        else
            LogMessage("No runnable events");
    }

    private void TrySpawnRoundstartAntags(Entity<SecretPlusComponent> scheduler)
    {
        if (scheduler.Comp.NoRoundstartAntags)
            return;

        var primaryWeightList = _prototypeManager.Index(scheduler.Comp.PrimaryAntagsWeightTable);
        var weightList = _prototypeManager.Index(scheduler.Comp.RoundStartAntagsWeightTable);

        var count = GetTotalPlayerCount(_playerManager.Sessions);

        LogMessage($"Trying to run roundstart rules, total player count: {count}", false);

        var weights = weightList.Weights.ToDictionary();
        var primaryWeights = primaryWeightList.Weights.ToDictionary();
        const int maxIters = 50;
        var i = 0;
        var origChaos = scheduler.Comp.ChaosScore;
        while (scheduler.Comp.ChaosScore < 0 && i < maxIters)
        {
            i++;

            var pick = _random.Pick(i == 1 ? primaryWeights : weights);

            GameRuleComponent? ruleComp = null;
            if (!_prototypeManager.TryIndex(pick, out var entProto)
                || !entProto.TryGetComponent<GameRuleComponent>(out ruleComp, _factory))
                continue;

            var chaosScore = GetChaosScore(entProto, ruleComp);

            if (chaosScore == null)
            {
                Log.Warning($"Tried running roundstart event {entProto.ID}, but chaos score was null");
                continue;
            }

            var pickProb = -scheduler.Comp.ChaosScore / chaosScore.Value;
            if (i == 1)
                pickProb *= scheduler.Comp.PrimaryAntagChaosBias;
            pickProb = MathF.Min(1f, pickProb);
            if (!_random.Prob(pickProb))
                continue;

            if (!scheduler.Comp.IgnoreIncompatible)
                weights.Remove(pick);

            IndexAndStartGameMode(pick, entProto, ruleComp);

            if (weights.Count == 0)
                return;
        }

        return;

        void IndexAndStartGameMode(string pick, EntityPrototype? pickProto, GameRuleComponent? ruleComp)
        {
            if (pickProto == null
                || ruleComp == null
                || ruleComp.MinPlayers > count)
                return;

            var effPlayers = (int)MathF.Round(count * scheduler.Comp.ChaosScore / origChaos);
            LogMessage($"Roundstart rule chosen: {pick} with score {GetChaosScore(pickProto, ruleComp, effPlayers)}");
            StartRule(scheduler, pick, false, effPlayers);
        }
    }

    private void StartRule(Entity<SecretPlusComponent> scheduler, string rule, bool doStart = true, int? players = null)
    {
        var ruleUid = _ticker.AddGameRule(rule);

        scheduler.Comp.ChaosScore += GetChaosScore(ruleUid, players)!.Value;

        if (players != null && TryComp<AntagSelectionComponent>(ruleUid, out var selection))
        {
            var runningCount = 0;
            for (var i = 0; i < selection.Antags.Length; i++)
            {
                var antag = selection.Antags[i];
                var targetCount = _antagSelection.GetTargetAntagCount(antag, players.Value, ref runningCount);
                if (antag is MinMaxAntagCountSelector minMax)
                    minMax.Range = new MinMax(targetCount, targetCount);
            }
        }

        if (doStart)
            _ticker.StartGameRule(ruleUid);
    }

    private PlayerCount CountActivePlayers()
    {
        var allPlayers = _playerManager.Sessions.ToList();
        var count = new PlayerCount();
        foreach (var player in allPlayers)
        {
            if (player.AttachedEntity != null)
            {
                if (HasComp<HumanoidProfileComponent>(player.AttachedEntity))
                    count.Players += 1;
                else if (TryComp<GhostComponent>(player.AttachedEntity, out var ghost) && ghost.CanReturnToBody)
                    count.Ghosts += 1;
            }
        }

        count.Players += _event.PlayerCountBias;

        return count;
    }

    public float? GetChaosScore(Entity<GameRuleComponent?> rule, int? players = null)
    {
        if (!Resolve(rule, ref rule.Comp))
            return null;

        if (TryComp<AntagSelectionComponent>(rule, out var selection))
        {
            var score = GetAntagChaosScore(selection, players);
            if (score != null)
                return score;
        }

        return rule.Comp.ChaosScore;
    }

    public float? GetChaosScore(EntityPrototype ruleProto, GameRuleComponent? ruleComp, int? players = null)
    {
        if (ruleComp == null && !ruleProto.TryGetComponent<GameRuleComponent>(out ruleComp, _factory))
            return null;

        if (ruleProto.TryGetComponent<AntagSelectionComponent>(out var selection, _factory))
        {
            var score = GetAntagChaosScore(selection, players);
            if (score != null)
                return score;
        }

        return ruleComp.ChaosScore;
    }

    private float? GetAntagChaosScore(AntagSelectionComponent selection, int? players = null)
    {
        var any = false;
        var score = 0f;
        var runningCount = 0;
        var pool = players ?? GetTotalPlayerCount(_playerManager.Sessions);

        foreach (var antag in selection.Antags)
        {
            if (antag.ChaosScore == null)
                continue;

            any = true;
            var count = _antagSelection.GetTargetAntagCount(antag, pool, ref runningCount);
            score += antag.ChaosScore.Value * count;
        }

        return any ? score : null;
    }

    public int GetTotalPlayerCount(IList<ICommonSession> pool)
    {
        var count = 0;
        foreach (var session in pool)
        {
            if (session.Status is SessionStatus.Disconnected or SessionStatus.Zombie)
                continue;

            count++;
        }

        return count + _event.PlayerCountBias;
    }

    public float GetRamping(Entity<SecretPlusComponent> scheduler)
    {
        var curTime = _ticker.RoundDuration();
        return 1f + (float)curTime.TotalSeconds * scheduler.Comp.SpeedRamping * _event.EventSpeedup;
    }

    private SelectedEvent? ChooseEvent(Entity<SecretPlusComponent> scheduler)
    {
        var possible = scheduler.Comp.SelectedEvents;
        Dictionary<SelectedEvent, float> weights = new();

        foreach (var ev in possible)
        {
            if (ev.EvComp == null)
                continue;

            var chaosScore = GetChaosScore(ev.Proto, ev.RuleComp);
            if (chaosScore == null)
            {
                Log.Warning($"Tried running event {ev.Proto.ID}, but chaos score was null");
                continue;
            }

            var weight = chaosScore.Value;
            var negative = weight < 0f;
            weight = MathF.Abs(weight);
            weight = MathF.Pow(weight, scheduler.Comp.ChaosExponent);
            if (negative)
                weight = -weight;
            weight += scheduler.Comp.ChaosOffset;
            weight += weight < 0f ? -scheduler.Comp.ChaosThreshold : scheduler.Comp.ChaosThreshold;
            var delta = ChaosDelta(-scheduler.Comp.ChaosScore, weight, scheduler.Comp.ChaosMatching, scheduler.Comp.ChaosThreshold * scheduler.Comp.ChaosThreshold);
            weights[ev] = ev.EvComp.Weight / (delta + 1f);
        }

        return weights.Count == 0 ? null : _random.Pick(weights);
    }

    private float ChaosDelta(float chaos1, float chaos2, float logBase, float differentSignMultiplier)
    {
        var ratio = chaos2 / chaos1;
        if (ratio < 0f)
            ratio = MathF.Abs(chaos2 * chaos1 / differentSignMultiplier);
        return MathF.Abs(MathF.Log(ratio, logBase));
    }

    private void LogMessage(string message, bool showChat = true)
    {
        if (showChat)
            _adminLogger.Add(LogType.SecretPlus, LogImpact.Medium, $"{message}");
        else
            _adminLogger.Add(LogType.SecretPlus, LogImpact.High, $"{message}");
        if (showChat)
            _chat.SendAdminAnnouncement("SecretPlus " + message);
    }
}
