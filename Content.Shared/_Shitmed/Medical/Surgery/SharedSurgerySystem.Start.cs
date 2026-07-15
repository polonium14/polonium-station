using Content.Shared._Shitmed.CCVar;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Shared._Shitmed.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    [Dependency] private IConfigurationManager _config = default!;

    private EntityQuery<SurgeryTargetComponent> _targetQuery;

    private bool _noSelfOperate;

    private void InitializeStart()
    {
        _targetQuery = GetEntityQuery<SurgeryTargetComponent>();

        SubscribeLocalEvent<SurgeryToolComponent, GetVerbsEvent<UtilityVerb>>(OnUtilityVerb);
        SubscribeLocalEvent<SurgeryToolComponent, AfterInteractEvent>(OnAfterInteract);

        // cvar is yes var is no, invert it
        Subs.CVar(_config, SurgeryCVars.CanOperateOnSelf, x => _noSelfOperate = !x, true);
    }

    /// <summary>
    /// Returns true if surgery was actually started (UI opened), false if a gate (not lying
    /// down, self-operate disabled) blocked it - both gates already show their own popup, so
    /// callers don't need to give separate feedback on a false return.
    /// </summary>
    private bool AttemptStartSurgery(Entity<SurgeryToolComponent> ent, EntityUid user, EntityUid target)
    {
        if (!IsLyingDown(target, user))
            return false;

        if (_noSelfOperate && user == target)
        {
            _popup.PopupClient(Loc.GetString("surgery-error-self-surgery"), user, user);
            return false;
        }

        _ui.OpenUi(target, SurgeryUIKey.Key, user);
        RefreshUI(target);
        return true;
    }

    private void OnUtilityVerb(Entity<SurgeryToolComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        var target = args.Target;
        if (!args.CanInteract
            || !args.CanAccess
            || !_targetQuery.HasComp(target))
            return;

        var user = args.User;

        var verb = new UtilityVerb()
        {
            Act = () => AttemptStartSurgery(ent, user, target),
            Icon = new SpriteSpecifier.Rsi(new("Objects/Specific/Medical/Surgery/scalpel.rsi"), "scalpel"),
            Text = Loc.GetString("surgery-verb-text"),
            Message = Loc.GetString("surgery-verb-message"),
            DoContactInteraction = true
        };

        args.Verbs.Add(verb);
    }

    /// <summary>
    /// Lets a surgery tool (scalpel, etc.) start surgery by direct use on a lying-down patient,
    /// not only via the explicit "Start Surgery" utility verb. Mirrors TourniquetSystem's own
    /// AfterInteractEvent handling for the same tool-used-on-a-mob shape. Only marks the event
    /// handled once surgery actually starts - a blocked attempt (not lying down, self-operate
    /// disabled) already gets its own popup out of AttemptStartSurgery, and leaving Handled
    /// false lets any other AfterInteract behavior on the same item still fire.
    /// </summary>
    private void OnAfterInteract(Entity<SurgeryToolComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled
            || !args.CanReach
            || args.Target is not { } target
            || !_targetQuery.HasComp(target))
            return;

        if (AttemptStartSurgery(ent, args.User, target))
            args.Handled = true;
    }
}
