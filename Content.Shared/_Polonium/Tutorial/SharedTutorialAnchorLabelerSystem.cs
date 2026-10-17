using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Map.Components;

namespace Content.Shared._Polonium.Tutorial;

public abstract partial class SharedTutorialAnchorLabelerSystem : EntitySystem
{
    [Dependency] protected SharedUserInterfaceSystem UserInterfaceSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TutorialAnchorLabelerComponent, BeforeRangedInteractEvent>(OnBeforeRanged);
        SubscribeLocalEvent<TutorialAnchorLabelerComponent, GetVerbsEvent<UtilityVerb>>(OnUtilityVerb);
        SubscribeLocalEvent<TutorialAnchorLabelerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<TutorialAnchorLabelerComponent, TutorialAnchorLabelerAnchorChangedMessage>(OnAnchorChanged);
        SubscribeLocalEvent<TutorialAnchorLabelerComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnAfterState(Entity<TutorialAnchorLabelerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateUI(ent);
    }

    protected virtual void UpdateUI(Entity<TutorialAnchorLabelerComponent> ent)
    {
    }

    private void OnBeforeRanged(Entity<TutorialAnchorLabelerComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (args.Handled || args.Target is not { Valid: true } target)
            return;

        if (!CanStamp(ent, target))
            return;

        // eat the click so we dont also pry the door / pick the item up
        args.Handled = true;
        ApplyAnchor(ent, args.User, target);
    }

    private void OnUtilityVerb(Entity<TutorialAnchorLabelerComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanAccess || !CanStamp(ent, args.Target))
            return;

        var user = args.User;
        var target = args.Target;

        if (ent.Comp.AssignedAnchor != string.Empty)
        {
            args.Verbs.Add(new UtilityVerb
            {
                Act = () => ApplyAnchor(ent, user, target),
                Text = Loc.GetString("tutorial-anchor-labeler-add-text"),
            });
        }

        if (HasComp<TutorialAnchorComponent>(target))
        {
            args.Verbs.Add(new UtilityVerb
            {
                Act = () => ApplyAnchor(ent, user, target, peel: true),
                Text = Loc.GetString("tutorial-anchor-labeler-remove-text"),
                Priority = -1,
            });
        }
    }

    private void ApplyAnchor(Entity<TutorialAnchorLabelerComponent> ent, EntityUid user, EntityUid target, bool peel = false)
    {
        var id = peel ? string.Empty : ent.Comp.AssignedAnchor.Trim();

        if (id.Length == 0)
        {
            if (!HasComp<TutorialAnchorComponent>(target))
                return;

            RemComp<TutorialAnchorComponent>(target);
            _popup.PopupEntity(Loc.GetString("tutorial-anchor-labeler-removed"), user, user);
            _adminLogger.Add(LogType.Action, LogImpact.Low,
                $"{ToPrettyString(user):user} peeled TutorialAnchor from {ToPrettyString(target):target}");
            return;
        }

        var anchor = EnsureComp<TutorialAnchorComponent>(target);
        anchor.AnchorId = id;
        Dirty(target, anchor);

        _popup.PopupEntity(Loc.GetString("tutorial-anchor-labeler-applied", ("anchor", id)), user, user);
        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):user} stuck TutorialAnchor '{id}' on {ToPrettyString(target):target}");
    }

    private bool CanStamp(Entity<TutorialAnchorLabelerComponent> ent, EntityUid target)
    {
        if (target == ent.Owner)
            return false;

        return !HasComp<MapComponent>(target) && !HasComp<MapGridComponent>(target);
    }

    private void OnAnchorChanged(Entity<TutorialAnchorLabelerComponent> ent, ref TutorialAnchorLabelerAnchorChangedMessage args)
    {
        var id = args.Anchor.Trim();
        ent.Comp.AssignedAnchor = id[..Math.Min(ent.Comp.MaxAnchorChars, id.Length)];
        UpdateUI(ent);
        Dirty(ent);

        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):user} set {ToPrettyString(ent):labeler} to '{ent.Comp.AssignedAnchor}'");
    }

    private void OnExamined(Entity<TutorialAnchorLabelerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var text = ent.Comp.AssignedAnchor == string.Empty
            ? Loc.GetString("tutorial-anchor-labeler-examine-blank")
            : Loc.GetString("tutorial-anchor-labeler-examine", ("anchor", ent.Comp.AssignedAnchor));
        args.PushMarkup(text);
    }
}
