using Content.Shared.Actions;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Devour;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Movement.Systems;
using Content.Shared.Polymorph.Components;
using Content.Shared.Polymorph.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Polonium.Morph;

public abstract partial class SharedMorphSystem : EntitySystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] protected IGameTiming Timing = default!;

    private const string StomachBuiXmlGeneratedName = "MorphStomachBoundUserInterface";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MorphComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MorphComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MorphComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<MorphComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<MorphComponent, DevourActionEvent>(OnDevourAttempt, before: [typeof(DevourSystem)]);
        SubscribeLocalEvent<MorphComponent, MorphStomachActionEvent>(OnStomachAction);

        SubscribeLocalEvent<ChameleonDisguisedComponent, ComponentInit>(OnDisguisedInit);
        SubscribeLocalEvent<ChameleonDisguisedComponent, ComponentRemove>(OnDisguisedRemove);

        SubscribeLocalEvent<MorphDisguiseComponent, InteractHandEvent>(OnAmbush, before: [typeof(SharedChameleonProjectorSystem)]);
        SubscribeLocalEvent<MorphDisguiseComponent, ExaminedEvent>(OnDisguiseExamined);
    }

    private void OnMapInit(Entity<MorphComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.DisguiseActionEntity, ent.Comp.DisguiseAction);
        _actions.AddAction(ent, ref ent.Comp.RevealActionEntity, ent.Comp.RevealAction);
        _actions.AddAction(ent, ref ent.Comp.StomachActionEntity, ent.Comp.StomachAction);
        _actions.AddAction(ent, ref ent.Comp.SpitActionEntity, ent.Comp.SpitAction);

        var userInterface = EnsureComp<UserInterfaceComponent>(ent);
        _ui.SetUi((ent, userInterface), MorphStomachUiKey.Key, new InterfaceData(StomachBuiXmlGeneratedName));

        // no spawning prediction
        if (_net.IsClient)
            return;

        var container = _container.EnsureContainer<ContainerSlot>(ent, MorphComponent.ProjectorContainerId);
        ent.Comp.Projector = Spawn(ent.Comp.ProjectorProto);
        _container.Insert(ent.Comp.Projector.Value, container);
        Dirty(ent);
    }

    private void OnShutdown(Entity<MorphComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.DisguiseActionEntity);
        _actions.RemoveAction(ent.Owner, ent.Comp.RevealActionEntity);
        _actions.RemoveAction(ent.Owner, ent.Comp.StomachActionEntity);
        _actions.RemoveAction(ent.Owner, ent.Comp.SpitActionEntity);
    }

    private void OnRefreshSpeed(Entity<MorphComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Disguised)
            args.ModifySpeed(ent.Comp.DisguisedSpeedModifier);
    }

    private void OnGetMeleeDamage(Entity<MorphComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (ent.Comp.Disguised)
            args.Damage *= ent.Comp.DisguisedDamageMultiplier;
    }

    private void OnDevourAttempt(Entity<MorphComponent> ent, ref DevourActionEvent args)
    {
        if (args.Handled || !ent.Comp.Disguised)
            return;

        args.Handled = true;
        _popup.PopupClient(Loc.GetString("morph-devour-disguised"), ent, ent);
    }

    private void OnStomachAction(Entity<MorphComponent> ent, ref MorphStomachActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!_ui.IsUiOpen(ent.Owner, MorphStomachUiKey.Key, args.Performer))
            _ui.OpenUi(ent.Owner, MorphStomachUiKey.Key, args.Performer);
    }

    private void OnDisguisedInit(Entity<ChameleonDisguisedComponent> ent, ref ComponentInit args)
    {
        if (!TryComp<MorphComponent>(ent, out var morph))
            return;

        morph.Disguised = true;
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnDisguisedRemove(Entity<ChameleonDisguisedComponent> ent, ref ComponentRemove args)
    {
        if (!TryComp<MorphComponent>(ent, out var morph))
            return;

        morph.Disguised = false;
        // also covers reveals that didn't go through the morph, like being shoved into a locker
        morph.NextDisguise = Timing.CurTime + morph.DisguiseCooldown;
        Dirty(ent.Owner, morph);
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnAmbush(Entity<MorphDisguiseComponent> ent, ref InteractHandEvent args)
    {
        if (!TryComp<ChameleonDisguiseComponent>(ent, out var disguise)
            || !TryComp<MorphComponent>(disguise.User, out var morph))
            return;

        var victim = args.User;
        _stun.TryKnockdown(victim, morph.AmbushKnockdown);
        _bloodstream.TryAddToBloodstream(victim, new Solution(morph.AmbushReagent, morph.AmbushReagentAmount));

        _popup.PopupPredicted(
            Loc.GetString("morph-ambush-victim", ("morph", disguise.User)),
            Loc.GetString("morph-ambush-others", ("morph", disguise.User), ("victim", victim)),
            disguise.User,
            victim,
            PopupType.LargeCaution);

        // the chameleon projector system reveals the morph after this
    }

    private void OnDisguiseExamined(Entity<MorphDisguiseComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("morph-disguise-examine"), -1);
    }
}
