using Content.Shared._Polonium.Objectives;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Prototypes;
using Content.Shared.Objectives.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Objectives;

public sealed class CustomObjectiveSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedObjectivesSystem _objectives = default!;
    [Dependency] private MetaDataSystem _meta = default!;

    private static readonly EntProtoId CustomObjectiveId = "PoloniumCustomObjective";

    private const float PartialProgress = 0.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CustomObjectiveConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(Entity<CustomObjectiveConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = ent.Comp.Status switch
        {
            CustomObjectiveStatus.Completed => 1f,
            CustomObjectiveStatus.Partial => PartialProgress,
            _ => 0f,
        };
    }

    public bool TryAdd(
        Entity<MindComponent> mind,
        string title,
        string description,
        ProtoId<ObjectiveIssuerPrototype> issuer,
        out EntityUid objective)
    {
        objective = default;

        if (!_objectives.TryCreateObjective(mind, CustomObjectiveId, out var created))
            return false;

        objective = created.Value;
        _meta.SetEntityName(objective, title);
        _meta.SetEntityDescription(objective, description);
        _objectives.SetIssuer(objective, issuer);
        _mind.AddObjective(mind, mind.Comp, objective);
        return true;
    }

    public bool TrySetStatus(Entity<MindComponent> mind, EntityUid objective, CustomObjectiveStatus status)
    {
        if (!mind.Comp.Objectives.Contains(objective) ||
            !TryComp<CustomObjectiveConditionComponent>(objective, out var condition))
        {
            return false;
        }

        condition.Status = status;
        return true;
    }

    public bool TryRemove(Entity<MindComponent> mind, EntityUid objective)
    {
        var index = mind.Comp.Objectives.IndexOf(objective);
        if (index < 0 || !HasComp<CustomObjectiveConditionComponent>(objective))
            return false;

        return _mind.TryRemoveObjective(mind, mind.Comp, index);
    }

    public IEnumerable<Entity<CustomObjectiveConditionComponent, ObjectiveComponent>> GetCustomObjectives(MindComponent mind)
    {
        foreach (var objective in mind.Objectives)
        {
            if (TryComp<CustomObjectiveConditionComponent>(objective, out var condition) &&
                TryComp<ObjectiveComponent>(objective, out var comp))
            {
                yield return (objective, condition, comp);
            }
        }
    }
}
