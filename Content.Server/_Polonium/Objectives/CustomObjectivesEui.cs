using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._Polonium.Objectives;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Eui;
using Content.Shared.Mind;
using Content.Shared.Objectives.Prototypes;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Objectives;

public sealed partial class CustomObjectivesEui : BaseEui
{
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ILogManager _logManager = default!;

    private readonly CustomObjectiveSystem _customObjectives;
    private readonly SharedMindSystem _mind;
    private readonly ISawmill _sawmill;
    private readonly NetUserId _target;
    private readonly string _targetName;

    public CustomObjectivesEui(IDependencyCollection deps, NetUserId target, string targetName)
    {
        deps.InjectDependencies(this, oneOff: true);

        _customObjectives = _entities.System<CustomObjectiveSystem>();
        _mind = _entities.System<SharedMindSystem>();
        _sawmill = _logManager.GetSawmill("custom-objectives");
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
        var objectives = new List<CustomObjectiveEntry>();
        var hasMind = _mind.TryGetMind(_target, out var mind);

        if (hasMind)
        {
            foreach (var (uid, condition, comp) in _customObjectives.GetCustomObjectives(mind!.Value.Comp))
            {
                var meta = _entities.GetComponent<MetaDataComponent>(uid);
                var issuer = _prototypes.TryIndex(comp.Issuer, out var issuerProto)
                    ? issuerProto.LocalizedName
                    : comp.Issuer.Id;

                objectives.Add(new CustomObjectiveEntry(
                    _entities.GetNetEntity(uid),
                    meta.EntityName,
                    meta.EntityDescription,
                    issuer,
                    condition.Status));
            }
        }

        var issuers = new List<CustomObjectiveIssuer>();
        foreach (var issuer in _prototypes.EnumeratePrototypes<ObjectiveIssuerPrototype>())
        {
            issuers.Add(new CustomObjectiveIssuer(issuer.ID, issuer.LocalizedName));
        }

        issuers.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));

        return new CustomObjectivesEuiState(_targetName, objectives, issuers, hasMind);
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
            case CustomObjectivesEuiMsg.Add add:
                HandleAdd(mind.Value, add);
                break;
            case CustomObjectivesEuiMsg.SetStatus setStatus:
                HandleSetStatus(mind.Value, setStatus);
                break;
            case CustomObjectivesEuiMsg.Remove remove:
                HandleRemove(mind.Value, remove);
                break;
        }

        StateDirty();
    }

    private void HandleAdd(Entity<MindComponent> mind, CustomObjectivesEuiMsg.Add add)
    {
        var title = add.Title.Trim();
        var description = add.Description.Trim();

        if (title.Length == 0 ||
            title.Length > CustomObjectiveLimits.MaxTitleLength ||
            description.Length > CustomObjectiveLimits.MaxDescriptionLength ||
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

    private void HandleSetStatus(Entity<MindComponent> mind, CustomObjectivesEuiMsg.SetStatus setStatus)
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

    private void HandleRemove(Entity<MindComponent> mind, CustomObjectivesEuiMsg.Remove remove)
    {
        if (!_entities.TryGetEntity(remove.Objective, out var objective))
            return;

        var name = _entities.ToPrettyString(objective.Value);
        if (!_customObjectives.TryRemove(mind, objective.Value))
        {
            _sawmill.Warning($"{Player} tried to remove an invalid custom objective from {_targetName}");
            return;
        }

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{Player:actor} removed the custom objective {name:objective} from {_targetName}");
    }
}
