using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._Polonium.Objectives;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Eui;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Prototypes;
using Content.Shared.Objectives.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Objectives;

public sealed partial class AdminObjectivesEui : BaseEui
{
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ILogManager _logManager = default!;

    private readonly CustomObjectiveSystem _customObjectives;
    private readonly SharedObjectivesSystem _objectives;
    private readonly SharedMindSystem _mind;
    private readonly ISawmill _sawmill;
    private readonly NetUserId _target;
    private readonly string _targetName;

    public AdminObjectivesEui(IDependencyCollection deps, NetUserId target, string targetName)
    {
        deps.InjectDependencies(this, oneOff: true);

        _customObjectives = _entities.System<CustomObjectiveSystem>();
        _objectives = _entities.System<SharedObjectivesSystem>();
        _mind = _entities.System<SharedMindSystem>();
        _sawmill = _logManager.GetSawmill("admin-objectives");
        _target = target;
        _targetName = targetName;
    }

    public override void Opened()
    {
        base.Opened();
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        var objectives = new List<AdminObjectiveEntry>();
        var hasMind = _mind.TryGetMind(_target, out var mind);

        if (hasMind)
        {
            foreach (var uid in mind!.Value.Comp.Objectives)
            {
                if (!_entities.TryGetComponent<ObjectiveComponent>(uid, out var comp))
                    continue;

                var meta = _entities.GetComponent<MetaDataComponent>(uid);
                var issuer = _prototypes.TryIndex(comp.Issuer, out var issuerProto)
                    ? issuerProto.LocalizedName
                    : comp.Issuer.Id;
                CustomObjectiveStatus? status =
                    _entities.TryGetComponent<CustomObjectiveConditionComponent>(uid, out var condition)
                        ? condition.Status
                        : null;

                objectives.Add(new AdminObjectiveEntry(
                    _entities.GetNetEntity(uid),
                    meta.EntityName,
                    meta.EntityDescription,
                    issuer,
                    _objectives.GetProgress(uid, mind.Value),
                    status));
            }
        }

        return new AdminObjectivesEuiState(_targetName, objectives, hasMind);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!_admins.HasAdminFlag(Player, AdminFlags.Admin))
        {
            Close();
            return;
        }

        if (!_mind.TryGetMind(_target, out var mind))
        {
            StateDirty();
            return;
        }

        switch (msg)
        {
            case AdminObjectivesEuiMsg.AddStandard add:
                HandleAddStandard(mind.Value, add);
                break;
            case AdminObjectivesEuiMsg.AddCustom add:
                HandleAddCustom(mind.Value, add);
                break;
            case AdminObjectivesEuiMsg.SetStatus setStatus:
                HandleSetStatus(mind.Value, setStatus);
                break;
            case AdminObjectivesEuiMsg.Remove remove:
                HandleRemove(mind.Value, remove);
                break;
        }

        StateDirty();
    }

    private void HandleAddStandard(Entity<MindComponent> mind, AdminObjectivesEuiMsg.AddStandard add)
    {
        if (add.Prototype == CustomObjectives.Prototype ||
            !_prototypes.TryIndex<EntityPrototype>(add.Prototype, out var proto) ||
            proto.Abstract ||
            !proto.HasComp<ObjectiveComponent>(_entities.ComponentFactory))
        {
            _sawmill.Warning($"{Player} sent an invalid objective prototype for {_targetName}");
            return;
        }

        if (!_objectives.TryCreateObjective(mind, add.Prototype, out var objective))
        {
            SendMessage(new AdminObjectivesEuiMsg.AddStandardFailed(add.Prototype));
            return;
        }

        _mind.AddObjective(mind, mind.Comp, objective.Value);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{Player:actor} gave {_targetName} the objective {_entities.ToPrettyString(objective.Value):objective}");
    }

    private void HandleAddCustom(Entity<MindComponent> mind, AdminObjectivesEuiMsg.AddCustom add)
    {
        var title = add.Title.Trim();
        var description = add.Description.Trim();

        if (title.Length == 0 ||
            title.Length > CustomObjectives.MaxTitleLength ||
            description.Length > CustomObjectives.MaxDescriptionLength ||
            !_prototypes.HasIndex<ObjectiveIssuerPrototype>(add.Issuer))
        {
            _sawmill.Warning($"{Player} sent an invalid custom objective for {_targetName}");
            return;
        }

        if (!_customObjectives.TryAdd(mind, title, description, add.Issuer, out var objective))
        {
            _sawmill.Warning($"Could not add a custom objective to {_targetName}");
            return;
        }

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{Player:actor} gave {_targetName} a custom objective {_entities.ToPrettyString(objective):objective}: {title}");
    }

    private void HandleSetStatus(Entity<MindComponent> mind, AdminObjectivesEuiMsg.SetStatus setStatus)
    {
        if (!Enum.IsDefined(setStatus.Status) ||
            !_entities.TryGetEntity(setStatus.Objective, out var objective) ||
            !_customObjectives.TrySetStatus(mind, objective.Value, setStatus.Status))
        {
            _sawmill.Warning($"{Player} sent an invalid custom objective status for {_targetName}");
            return;
        }

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{Player:actor} set the custom objective {_entities.ToPrettyString(objective.Value):objective} of {_targetName} to {setStatus.Status}");
    }

    private void HandleRemove(Entity<MindComponent> mind, AdminObjectivesEuiMsg.Remove remove)
    {
        var index = _entities.TryGetEntity(remove.Objective, out var objective)
            ? mind.Comp.Objectives.IndexOf(objective.Value)
            : -1;

        if (index < 0)
        {
            _sawmill.Warning($"{Player} tried to remove an invalid objective from {_targetName}");
            return;
        }

        var name = _entities.ToPrettyString(objective!.Value);
        if (!_mind.TryRemoveObjective(mind, mind.Comp, index))
            return;

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{Player:actor} removed the objective {name:objective} from {_targetName}");
    }
}
