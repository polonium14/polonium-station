using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Body.Components;
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
        return new BodyDebugEuiState(name, _bodyDebug.GetOrgans(_target), _bodyDebug.GetLimbs(_target), _bodyDebug.GetWoundTypes(),
            _entities.System<CirculationSystem>().IsStopped(_target),
            _entities.GetComponentOrNull<RespiratorComponent>(_target)?.Saturation,
            _error);
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
                foreach (var (netTarget, slider, value) in apply.Changes)
                {
                    if (!_entities.TryGetEntity(netTarget, out var target))
                        continue;

                    var targetName = _entities.ToPrettyString(target.Value);
                    var done = slider switch
                    {
                        BodyDebugSlider.Organ => _bodyDebug.TrySetOrganIntegrity(_target, target.Value, value),
                        BodyDebugSlider.Limb => _bodyDebug.TrySetLimbIntegrity(_target, target.Value, value),
                        BodyDebugSlider.Bone => _bodyDebug.TrySetBoneIntegrity(_target, target.Value, value),
                        _ => false,
                    };

                    if (Check(done, "body-debug-error-integrity"))
                        Log($"set the {slider} integrity of {targetName} to {value}");
                }

                break;

            case BodyDebugEuiMsg.Act act:
                if (!_entities.TryGetEntity(act.Target, out var acted))
                    break;

                // Taken before the action, the target may be deleted by it.
                var actedName = _entities.ToPrettyString(acted.Value);
                var success = act.Action switch
                {
                    BodyDebugAction.KillOrgan => _bodyDebug.TryKillOrgan(_target, acted.Value),
                    BodyDebugAction.ReviveOrgan => _bodyDebug.TryReviveOrgan(_target, acted.Value),
                    BodyDebugAction.RemoveOrgan => _bodyDebug.TryRemoveOrgan(_target, acted.Value),
                    BodyDebugAction.HealLimb => _bodyDebug.TryHealLimb(_target, acted.Value),
                    BodyDebugAction.StopBleeding => _bodyDebug.TryStopBleeding(_target, acted.Value),
                    BodyDebugAction.Amputate => _bodyDebug.TryAmputate(_target, acted.Value),
                    _ => false,
                };

                if (Check(success, "body-debug-error-action"))
                    Log($"used {act.Action} on {actedName}");

                break;

            case BodyDebugEuiMsg.Insert insert:
                if (_prototypes.HasIndex<OrganCategoryPrototype>(insert.Category)
                    && Check(_bodyDebug.TryPutBack(_target, insert.Category), "body-debug-error-insert"))
                    Log($"put back a fresh {insert.Category}");

                break;

            case BodyDebugEuiMsg.SetFailureStage failureStage:
                if (_entities.TryGetEntity(failureStage.Organ, out var failing)
                    && Check(_bodyDebug.TrySetFailureStage(_target, failing.Value, failureStage.Stage), "body-debug-error-failure"))
                    Log($"set the failure of {_entities.ToPrettyString(failing.Value)} to stage {failureStage.Stage}");

                break;

            case BodyDebugEuiMsg.AddWound wound:
                if (_entities.TryGetEntity(wound.Limb, out var limb)
                    && Check(_bodyDebug.TryAddWound(_target, limb.Value, wound.DamageType, Math.Clamp(wound.Amount, 1, BodyDebugSystem.MaxWoundDamage)), "body-debug-error-wound"))
                    Log($"dealt {wound.Amount} {wound.DamageType} damage to {_entities.ToPrettyString(limb.Value)}");

                break;
        }

        StateDirty();
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
