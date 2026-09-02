using System.Linq;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Content.Shared.Mobs.Components;

namespace Content.Shared._Polonium.Medical.IV;

public abstract partial class SharedIVDripSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _packsToUpdate = [];

    private EntityQuery<IVBagComponent> _bloodPackQuery;

    public override void Initialize()
    {
        _bloodPackQuery = GetEntityQuery<IVBagComponent>();

        SubscribeLocalEvent<IVDripComponent, EntInsertedIntoContainerMessage>(OnIVDripEntInserted);
        SubscribeLocalEvent<IVDripComponent, EntRemovedFromContainerMessage>(OnIVDripEntRemoved);
        SubscribeLocalEvent<IVDripComponent, AfterAutoHandleStateEvent>(OnIVDripAfterHandleState);
        SubscribeLocalEvent<IVDripComponent, CanDragEvent>(OnIVDripCanDrag);
        SubscribeLocalEvent<IVDripComponent, CanDropDraggedEvent>(OnIVDripCanDropDragged);
        SubscribeLocalEvent<IVDripComponent, DragDropDraggedEvent>(OnIVDripDragDropDragged);
        SubscribeLocalEvent<IVDripComponent, InteractHandEvent>(OnIVInteractHand);
        SubscribeLocalEvent<IVDripComponent, GetVerbsEvent<InteractionVerb>>(OnIVVerbs);
        SubscribeLocalEvent<IVDripComponent, ExaminedEvent>(OnIVExamine);

        SubscribeLocalEvent<IVBagComponent, MapInitEvent>(OnIVBagMapInit);
        SubscribeLocalEvent<IVBagComponent, AfterAutoHandleStateEvent>(OnIVBagAfterState);
        SubscribeLocalEvent<IVBagComponent, SolutionChangedEvent>(OnIVBagSolutionChanged);
        SubscribeLocalEvent<IVBagComponent, AfterInteractEvent>(OnIVBagAfterInteract);
        SubscribeLocalEvent<IVBagComponent, AttachIVBagDoAfterEvent>(OnIVBagAttachDoAfter);
        SubscribeLocalEvent<IVBagComponent, GotUnequippedHandEvent>(OnIVBagUnequippedHand);
        SubscribeLocalEvent<IVBagComponent, GetVerbsEvent<InteractionVerb>>(OnIVBagVerbs);
        SubscribeLocalEvent<IVBagComponent, ExaminedEvent>(OnIVBagExamine);
    }

    private void OnIVDripEntInserted(Entity<IVDripComponent> iv, ref EntInsertedIntoContainerMessage args)
    {
        UpdateIVVisuals(iv);
    }

    private void OnIVDripEntRemoved(Entity<IVDripComponent> iv, ref EntRemovedFromContainerMessage args)
    {
        UpdateIVVisuals(iv);
    }

    private void OnIVDripAfterHandleState(Entity<IVDripComponent> iv, ref AfterAutoHandleStateEvent args)
    {
        UpdateIVAppearance(iv);
    }

    private void OnIVDripCanDrag(Entity<IVDripComponent> iv, ref CanDragEvent args)
    {
        args.Handled = true;
    }

    private void OnIVDripCanDropDragged(Entity<IVDripComponent> iv, ref CanDropDraggedEvent args)
    {
        if (!HasComp<MobStateComponent>(args.Target) || !InRange(iv, args.Target, iv.Comp.Range))
            return;
        args.Handled = true;
        args.CanDrop = true;
    }

    private void OnIVDripDragDropDragged(Entity<IVDripComponent> iv, ref    DragDropDraggedEvent args)
    {
        if (args.Handled)
            return;

        if (iv.Comp.AttachedTo == default)
            AttachIV(iv, args.User, args.Target);
        else
            DetachIV(iv, args.User, false);
    }

    private void OnIVInteractHand(Entity<IVDripComponent> iv, ref InteractHandEvent args)
    {
        DetachIV(iv, args.User, false);
    }

    private void OnIVVerbs(Entity<IVDripComponent> iv, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Act = () => ToggleInject(iv, user),
            Text = Loc.GetString("cm-iv-verb-toggle-inject"),
        });
    }

    private void OnIVExamine(Entity<IVDripComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(IVDripComponent)))
        {
            var injectingMsg = ent.Comp.Injecting
                ? "cm-iv-examine-injecting"
                : "cm-iv-examine-drawing";
            args.PushMarkup(Loc.GetString(injectingMsg, ("iv", ent.Owner)));

            var chemicalsMsg = Loc.GetString("cm-iv-examine-chemicals-none");
            if (_containers.TryGetContainer(ent, ent.Comp.Slot, out var container) &&
                container.ContainedEntities.FirstOrDefault() is { Valid: true } packId &&
                TryComp(packId, out IVBagComponent? pack) &&
                _solutionContainer.TryGetSolution(packId, pack.Solution, out _, out var solution))
            {
                chemicalsMsg = Loc.GetString("cm-iv-examine-chemicals",
                    ("attached", packId),
                    ("units", solution.Volume.Int()));
            }

            args.PushMarkup(chemicalsMsg);

            var attachedMsg = ent.Comp.AttachedTo is { } attached
                ? Loc.GetString("cm-iv-examine-attached", ("attached", attached))
                : Loc.GetString("cm-iv-examine-attached-none");
            args.PushMarkup(attachedMsg);
        }
    }

    private void OnIVBagMapInit(Entity<IVBagComponent> pack, ref MapInitEvent args)
    {
        _packsToUpdate.Add(pack);
    }

    private void OnIVBagAfterState(Entity<IVBagComponent> pack, ref AfterAutoHandleStateEvent args)
    {
        UpdatePackVisuals(pack);
    }

    private void OnIVBagSolutionChanged(Entity<IVBagComponent> pack, ref SolutionChangedEvent args)
    {
        UpdatePackVisuals(pack);
    }

    private void OnIVBagAfterInteract(Entity<IVBagComponent> pack, ref AfterInteractEvent args)
    {
        if (args.Target is not { } target)
            return;

        if (!HasComp<MobStateComponent>(target) || !InRange(pack, target, pack.Comp.Range))
            return;

        args.Handled = true;

        var user = args.User;
        if (pack.Comp.AttachedTo != null)
        {
            DetachPack(pack, user, false);
            return;
        }

        if (user == target)
        {
            _popup.PopupEntity(Loc.GetString("cm-blood-pack-cannot-self"), user, user);
            return;
        }

        var delay = pack.Comp.AttachDelay;
        if (delay > TimeSpan.Zero)
        {
            var selfPoke = Loc.GetString("cm-blood-pack-poke-self", ("pack", pack.Owner), ("target", target));
            var othersPoke = Loc.GetString("cm-blood-pack-poke-others",
                ("user", user),
                ("pack", pack.Owner),
                ("target", target));
            _popup.PopupEntity(selfPoke, othersPoke, target, user);
        }

        var ev = new AttachIVBagDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, user, delay, ev, pack, target, pack)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnIVBagAttachDoAfter(Entity<IVBagComponent> pack, ref AttachIVBagDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        AttachPack(pack, args.User, target);
    }

    private void OnIVBagUnequippedHand(Entity<IVBagComponent> pack, ref GotUnequippedHandEvent args)
    {
        DetachPack(pack, args.User, true);
    }

    private void OnIVBagVerbs(Entity<IVBagComponent> pack, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Act = () => ToggleInject(pack, user),
            Text = Loc.GetString("cm-iv-verb-toggle-inject"),
        });
    }

    private void OnIVBagExamine(Entity<IVBagComponent> pack, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(IVBagComponent)))
        {
            var injectingMsg = pack.Comp.Injecting
                ? "cm-iv-examine-injecting"
                : "cm-iv-examine-drawing";
            args.PushMarkup(Loc.GetString(injectingMsg, ("iv", pack.Owner)));

            var attachedMsg = pack.Comp.AttachedTo is { } attached
                ? Loc.GetString("cm-iv-examine-attached", ("attached", attached))
                : Loc.GetString("cm-iv-examine-attached-none");
            args.PushMarkup(attachedMsg);

            if (_solutionContainer.TryGetSolution(pack.Owner, pack.Comp.Solution, out _, out var solution))
                args.PushMarkup(Loc.GetString("cm-blood-pack-contains", ("units", solution.Volume.Int())));
        }
    }

    protected bool InRange(EntityUid iv, EntityUid to, float range)
    {
        var ivPos = _transform.GetMapCoordinates(iv);
        var toPos = _transform.GetMapCoordinates(to);
        return ivPos.InRange(toPos, range);
    }

    private void AttachIV(Entity<IVDripComponent> iv, EntityUid user, EntityUid to)
    {
        if (!InRange(iv, to, iv.Comp.Range))
            return;

        iv.Comp.AttachedTo = to;
        Dirty(iv);

        AttachFeedback(iv, user, to, iv.Comp.Injecting);
    }

    protected void DetachIV(Entity<IVDripComponent> iv, EntityUid? user, bool rip)
    {
        if (iv.Comp.AttachedTo is not { } target)
            return;

        iv.Comp.AttachedTo = default;
        Dirty(iv);

        if (rip)
            DoRip(iv.Comp.RipDamage, target, user, iv.Comp.RipEmote);
        else
            DoDetachFeedback(iv, target, user);
    }

    private void AttachPack(Entity<IVBagComponent> pack, EntityUid user, EntityUid to)
    {
        if (!InRange(pack, to, pack.Comp.Range))
            return;

        pack.Comp.AttachedTo = to;
        Dirty(pack);

        AttachFeedback(pack, user, to, pack.Comp.Injecting);
    }

    protected void DetachPack(Entity<IVBagComponent> pack, EntityUid? user, bool rip)
    {
        if (pack.Comp.AttachedTo is not { } target)
            return;

        pack.Comp.AttachedTo = default;
        Dirty(pack);

        if (rip)
            DoRip(pack.Comp.RipDamage, target, user, pack.Comp.RipEmote);
        else
            DoDetachFeedback(pack, target, user);
    }

    private void ToggleInject(Entity<IVDripComponent> iv, EntityUid user)
    {
        ToggleInject(iv, ref iv.Comp.Injecting, user);
        Dirty(iv);
    }

    private void ToggleInject(Entity<IVBagComponent> pack, EntityUid user)
    {
        ToggleInject(pack, ref pack.Comp.Injecting, user);
        Dirty(pack);
    }

    private void ToggleInject(EntityUid iv, ref bool injecting, EntityUid user)
    {
        injecting = !injecting;

        var msg = injecting
            ? Loc.GetString("cm-iv-now-injecting")
            : Loc.GetString("cm-iv-now-taking");

        _popup.PopupEntity(msg, iv, user);
    }

    protected void UpdatePackVisuals(Entity<IVBagComponent> pack)
    {
        if (!_solutionContainer.TryGetSolution(pack.Owner, pack.Comp.Solution, out _, out var solution))
        {
            UpdatePackAppearance(pack);
            return;
        }

        if (_containers.TryGetContainingContainer((pack, null), out var container) &&
            TryComp(container.Owner, out IVDripComponent? iv))
        {
            iv.FillColor = solution.GetColor(_prototype);
            iv.FillPercentage = (int) (FillFraction(solution) * 100);
            Dirty(container.Owner, iv);
            UpdateIVAppearance((container.Owner, iv));
        }

        UpdatePackAppearance(pack);
    }

    protected void UpdateIVVisuals(Entity<IVDripComponent> iv)
    {
        if (!_containers.TryGetContainer(iv, iv.Comp.Slot, out var container))
            return;

        var color = Color.White;
        var percentage = 0;

        foreach (var entity in container.ContainedEntities)
        {
            if (!TryComp(entity, out IVBagComponent? pack) ||
                !_solutionContainer.TryGetSolution(entity, pack.Solution, out _, out var solution))
                continue;

            color = solution.GetColor(_prototype);
            percentage = (int) (FillFraction(solution) * 100);
            break;
        }

        iv.Comp.FillColor = color;
        iv.Comp.FillPercentage = percentage;

        Dirty(iv);

        UpdateIVAppearance(iv);
    }

    protected virtual void UpdateIVAppearance(Entity<IVDripComponent> iv)
    {
    }

    protected virtual void UpdatePackAppearance(Entity<IVBagComponent> pack)
    {
        if (_net.IsClient)
            return;

        if (_solutionContainer.TryGetSolution(pack.Owner, pack.Comp.Solution, out var solEnt))
        {
            var solution = solEnt.Value.Comp.Solution;
            pack.Comp.FillPercentage = FillFraction(solution);
            pack.Comp.FillColor = solution.GetColor(_prototype);
        }
        else
        {
            pack.Comp.FillPercentage = FixedPoint2.Zero;
            pack.Comp.FillColor = Color.Transparent;
        }

        Dirty(pack);
    }

    private static FixedPoint2 FillFraction(Solution solution)
        => solution.MaxVolume > FixedPoint2.Zero ? solution.Volume / solution.MaxVolume : FixedPoint2.Zero;

    protected virtual void DoRip(DamageSpecifier? damage,
        EntityUid attached,
        EntityUid? user,
        ProtoId<EmotePrototype> ripEmote)
    {
        if (damage != null)
            _damageable.TryChangeDamage(attached, damage, true);

        // everyone in PVS sees the same message, and PopupEntity predicts itself
        _popup.PopupEntity(Loc.GetString("cm-iv-rip", ("target", attached)), attached);
    }

    private void AttachFeedback(EntityUid iv, EntityUid user, EntityUid to, bool injecting)
    {
        var selfMessage = injecting ? "cm-iv-attach-self-injecting" : "cm-iv-attach-self-drawing";
        var othersMessage = injecting ? "cm-iv-attach-others-injecting" : "cm-iv-attach-others-drawing";

        _popup.PopupEntity(Loc.GetString(selfMessage, ("iv", iv), ("target", to)),
            Loc.GetString(othersMessage, ("iv", iv), ("user", user), ("target", to)),
            to,
            user);
    }

    private void DoDetachFeedback(EntityUid iv, EntityUid attached, EntityUid? user)
    {
        var selfMessage = Loc.GetString("cm-iv-detach-self", ("iv", iv), ("target", attached));

        // no user means an automatic detach (walked out of range) - the "others" string
        // interpolates $user, so there is nothing to show anyone else
        if (user == null)
        {
            _popup.PopupEntity(selfMessage, attached);
            return;
        }

        _popup.PopupEntity(selfMessage,
            Loc.GetString("cm-iv-detach-others", ("iv", iv), ("user", user), ("target", attached)),
            attached,
            user);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var pack in _packsToUpdate)
        {
            if (_bloodPackQuery.TryComp(pack, out var comp))
                UpdatePackVisuals((pack, comp));
        }

        _packsToUpdate.Clear();
    }
}
