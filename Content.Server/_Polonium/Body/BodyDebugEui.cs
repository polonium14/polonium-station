using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Server.Administration;
using Content.Shared._Polonium.Body;
using Content.Shared.Body;
using Content.Shared.Database;
using Content.Shared.Eui;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Body;

public sealed partial class BodyDebugEui : BaseEui
{
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly BodyDebugSystem _bodyDebug;
    private readonly EntityUid _target;
    private string? _error;

    public BodyDebugEui(IDependencyCollection deps, EntityUid target)
    {
        deps.InjectDependencies(this, oneOff: true);

        _bodyDebug = _entities.System<BodyDebugSystem>();
        _target = target;
    }

    public override void Opened()
    {
        base.Opened();
        _admins.OnPermsChanged += OnPermsChanged;
        StateDirty();
    }

    public override void Closed()
    {
        base.Closed();
        _admins.OnPermsChanged -= OnPermsChanged;
    }

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player == Player && !_admins.HasAdminFlag(Player, BodyDebugSystem.RequiredFlag))
            Close();
    }

    public override EuiStateBase GetNewState()
    {
        var name = _entities.TryGetComponent<MetaDataComponent>(_target, out var meta) ? meta.EntityName : string.Empty;
        return new BodyDebugEuiState(name, _bodyDebug.GetOrgans(_target), _error);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is CloseEuiMessage)
            return;

        if (!_admins.HasAdminFlag(Player, BodyDebugSystem.RequiredFlag) || _entities.Deleted(_target))
        {
            Close();
            return;
        }

        _error = null;
        switch (msg)
        {
            case BodyDebugEuiMsg.Apply apply:
                foreach (var (netOrgan, percent) in apply.Changes)
                {
                    if (!_entities.TryGetEntity(netOrgan, out var organ))
                        continue;

                    var organName = _entities.ToPrettyString(organ.Value);
                    if (Check(_bodyDebug.TrySetOrganIntegrity(_target, organ.Value, percent), "body-debug-error-integrity"))
                        Log($"set the integrity of {organName} to {percent}%");
                }

                break;

            case BodyDebugEuiMsg.Kill kill:
                Act(kill.Organ, _bodyDebug.TryKillOrgan, "killed", "body-debug-error-kill");
                break;

            case BodyDebugEuiMsg.Revive revive:
                Act(revive.Organ, _bodyDebug.TryReviveOrgan, "revived", "body-debug-error-revive");
                break;

            case BodyDebugEuiMsg.Remove remove:
                Act(remove.Organ, _bodyDebug.TryRemoveOrgan, "removed", "body-debug-error-remove");
                break;

            case BodyDebugEuiMsg.Insert insert:
                if (_prototypes.HasIndex<OrganCategoryPrototype>(insert.Category)
                    && Check(_bodyDebug.TryInsertOrgan(_target, insert.Category), "body-debug-error-insert"))
                    Log($"inserted a fresh {insert.Category} organ");

                break;
        }

        StateDirty();
    }

    private void Act(NetEntity netOrgan, Func<EntityUid, EntityUid, bool> action, string verb, string error)
    {
        if (!_entities.TryGetEntity(netOrgan, out var organ))
            return;

        // Taken before the action, the organ may be deleted by it.
        var organName = _entities.ToPrettyString(organ.Value);
        if (Check(action(_target, organ.Value), error))
            Log($"{verb} {organName}");
    }

    private bool Check(bool success, string error)
    {
        if (!success)
            _error = error;

        return success;
    }

    private void Log(string action)
    {
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{Player:actor} {action} in the body of {_entities.ToPrettyString(_target):target} with the body debug window");
    }
}
