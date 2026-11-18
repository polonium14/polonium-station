using Content.Server.Fax;
using Content.Server.Station.Systems;
using Content.Shared.CCVar;
using Content.Shared.Fax.Components;
using Content.Shared.GameTicking;
using Content.Shared.Random.Helpers;
using Content.Shared.Station.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Corvax.StationGoal;

/// <summary>
/// System to spawn paper with station goal.
/// </summary>
public sealed partial class StationGoalPaperSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private FaxSystem _fax = default!;
    [Dependency] private ServerStationSystem _station = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        if (!_cfg.GetCVar(CCVars.StationGoal))
            return;

        var query = EntityQueryEnumerator<StationGoalComponent>();
        while (query.MoveNext(out var uid, out var station))
        {
            var crew = CompOrNull<StationJobsComponent>(uid)?.PlayerJobs.Count ?? 0;
            var weights = new Dictionary<ProtoId<StationGoalPrototype>, float>();
            foreach (var goalId in station.Goals)
            {
                var goalProto = _proto.Index(goalId);

                if (goalProto.MaxPlayers.HasValue && crew > goalProto.MaxPlayers.Value ||
                    goalProto.MinPlayers.HasValue && crew < goalProto.MinPlayers.Value)
                    continue;

                weights[goalId] = goalProto.Weight;
            }

            if (weights.Count == 0)
                continue;

            var selGoal = _proto.Index(_random.Pick(weights));

            if (SendStationGoal(uid, selGoal))
            {
                Log.Info($"Goal {selGoal.ID} has been sent to station {MetaData(uid).EntityName}");
            }
        }
    }

    public bool SendStationGoal(EntityUid ent, ProtoId<StationGoalPrototype> goal)
    {
        return SendStationGoal(ent, _proto.Index(goal));
    }

    /// <summary>
    /// Send a station goal on selected station to all faxes which are authorized to receive it.
    /// </summary>
    /// <returns>True if at least one fax received paper</returns>
    public bool SendStationGoal(EntityUid ent, StationGoalPrototype goal)
    {
        var printout = new FaxPrintout(
            Loc.GetString(goal.Text, ("station", MetaData(ent).EntityName)),
            Loc.GetString("station-goal-fax-paper-name"),
            null,
            null,
            "paper_stamp-centcom",
            [new() { StampedName = Loc.GetString("stamp-component-stamped-name-centcom"), StampedColor = Color.FromHex("#006600") }]
        );

        var wasSent = false;
        var query = EntityQueryEnumerator<FaxMachineComponent>();
        while (query.MoveNext(out var faxUid, out var fax))
        {
            if (!fax.ReceiveAllStationGoals && !(fax.ReceiveStationGoal && _station.GetOwningStation(faxUid) == ent))
                continue;

            _fax.Receive(faxUid, printout, null, fax);

            foreach (var spawnEnt in goal.Spawns)
                SpawnAtPosition(spawnEnt, Transform(faxUid).Coordinates);

            wasSent |= fax.ReceiveStationGoal;
        }

        return wasSent;
    }
}
