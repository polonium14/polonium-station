using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Nutrition.EntitySystems;
using Content.Shared._Polonium.Smoking;
using Content.Shared.Administration.Logs;
using Content.Shared.Atmos;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Smoking;
using Content.Shared.Temperature;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Smoking;

/// <summary>
/// The small parts of smoking that upstream lacks: smokables go out without oxygen and catch from fires, rigged ones
/// blow up when lit, and smokers can light each other's cigarettes, blow smoke, and stomp butts out.
/// </summary>
public sealed partial class SmokingExtrasSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInternalsSystem _internals = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SmokingSystem _smoking = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPointLightSystem _light = default!;

    private static readonly ProtoId<ReagentPrototype> Oxygen = "Oxygen";
    private static readonly ProtoId<EmotePrototype> Cough = "Cough";

    private const string MaskSlot = "mask";

    /// <summary>
    /// Below this many moles of oxygen around it a smokable goes out (tg: one mole). A station tile holds about 21.
    /// </summary>
    private const float MinOxygenMoles = 1f;

    private static readonly TimeSpan OxygenCheckInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Goon: smoke in the face makes one in five cough, and one in five of those drops what they hold.
    /// </summary>
    private const float CoughChance = 0.2f;

    private const float DropChance = 0.2f;

    private const int SoloPuffMessages = 4;

    /// <summary>
    /// How far from the middle of the tile in front a mob still counts as facing the smoker.
    /// </summary>
    private const float FaceRange = 0.5f;

    private readonly List<EntityUid> _outOfOxygen = new();
    private TimeSpan _nextOxygenCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmokableComponent, TileFireEvent>(OnTileFire);
        SubscribeLocalEvent<SmokableComponent, IgnitedEvent>(OnIgnited);
        SubscribeLocalEvent<SmokableComponent, ExtinguishedEvent>(OnExtinguished);
        SubscribeLocalEvent<SmokableComponent, GetVerbsEvent<Verb>>(OnSmokableVerbs);
        SubscribeLocalEvent<SmokableComponent, BlowSmokeActionEvent>(OnBlowSmokeAction);
        SubscribeLocalEvent<InventoryComponent, GetVerbsEvent<Verb>>(OnMobVerbs);
        SubscribeLocalEvent<InventoryComponent, InteractUsingEvent>(OnMobInteractUsing);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextOxygenCheck)
            return;

        _nextOxygenCheck = _timing.CurTime + OxygenCheckInterval;

        var query = EntityQueryEnumerator<BurningComponent, SmokableComponent>();
        while (query.MoveNext(out var uid, out _, out var smokable))
        {
            if (!HasOxygen((uid, smokable)))
                _outOfOxygen.Add(uid);
        }

        // Putting it out removes BurningComponent, so not while the query runs.
        foreach (var uid in _outOfOxygen)
        {
            _smoking.SetSmokableState(uid, SmokableState.Unlit);
            if (_container.TryGetContainingContainer(uid, out var container))
                _popup.PopupEntity(Loc.GetString("smokable-out-of-oxygen", ("smokable", uid)), uid, container.Owner);
        }

        _outOfOxygen.Clear();
    }

    private bool HasOxygen(Entity<SmokableComponent> ent)
    {
        // Oxygen in the smokable itself keeps it lit anywhere, like tg's space cigarettes.
        if (_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution)
            && solution.GetTotalPrototypeQuantity(Oxygen) > 0)
            return true;

        // A helmet with internals (tg: can_breathe_helmet).
        if (MaskWearer(ent) is { } wearer
            && TryComp<InternalsComponent>(wearer, out var internals)
            && _internals.AreInternalsWorking(internals))
            return true;

        var air = _atmos.GetContainingMixture(ent.Owner, excite: false);
        return air != null && air.GetMoles(Gas.Oxygen) >= MinOxygenMoles;
    }

    private void OnTileFire(Entity<SmokableComponent> ent, ref TileFireEvent args)
    {
        _smoking.TryLight(ent);
    }

    private void OnIgnited(Entity<SmokableComponent> ent, ref IgnitedEvent args)
    {
        SetEmberLight(ent, true);

        if (!TryComp<SmokableExplosiveComponent>(ent, out var explosive)
            || !_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution))
            return;

        var intensity = 0f;
        foreach (var (reagent, perUnit) in explosive.Reagents)
        {
            intensity += solution.GetTotalPrototypeQuantity(reagent).Float() * perUnit;
        }

        if (intensity <= 0f)
            return;

        _container.TryGetContainingContainer(ent.Owner, out var container);
        var holder = container?.Owner;
        _adminLog.Add(LogType.Explosion,
            LogImpact.Medium,
            $"Rigged {ToPrettyString(ent):smokable} held by {ToPrettyString(holder):holder} was lit and exploded with intensity {intensity}");

        _explosion.QueueExplosion(ent.Owner,
            SharedExplosionSystem.DefaultExplosionPrototypeId,
            intensity,
            explosive.Slope,
            explosive.MaxTileIntensity,
            canCreateVacuum: false);
        QueueDel(ent);
    }

    private void OnExtinguished(Entity<SmokableComponent> ent, ref ExtinguishedEvent args)
    {
        SetEmberLight(ent, false);
    }

    /// <summary>
    /// The faint glow of the ember, on while the smokable burns.
    /// </summary>
    private void SetEmberLight(EntityUid uid, bool enabled)
    {
        if (_light.TryGetLight(uid, out var light))
            _light.SetEnabled(uid, enabled, light);
    }

    private void OnSmokableVerbs(Entity<SmokableComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.State != SmokableState.Lit || _container.IsEntityInContainer(ent))
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("smokable-verb-stomp"),
            Act = () =>
            {
                _smoking.SetSmokableState(ent, SmokableState.Burnt, ent);
                _popup.PopupEntity(Loc.GetString("smokable-stomp", ("user", user), ("smokable", ent.Owner)), ent);
            },
        });
    }

    private void OnMobVerbs(Entity<InventoryComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        AddLightVerb(ent, ref args);
    }

    /// <summary>
    /// Lighting the cigarette in someone's mouth with whatever hot thing is in the active hand. A plain verb rather
    /// than a utility one, because utility verbs are asked of the held item and lighters share no component.
    /// </summary>
    private void AddLightVerb(Entity<InventoryComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (args.Using is not { } lighter || !CanLightWorn(ent, lighter))
            return;

        var user = args.User;
        var target = ent.Owner;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("smokable-verb-light"),
            Act = () => LightWorn(user, target, lighter),
        });
    }

    /// <summary>
    /// Clicking someone with a lighter or a lit cigarette lights the one in their mouth (tg).
    /// </summary>
    private void OnMobInteractUsing(Entity<InventoryComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !CanLightWorn(ent, args.Used))
            return;

        LightWorn(args.User, ent, args.Used);
        args.Handled = true;
    }

    private bool CanLightWorn(EntityUid target, EntityUid lighter)
    {
        if (WornSmokable(target) is not { } smokable || smokable.Comp.State != SmokableState.Unlit || smokable.Owner == lighter)
            return false;

        var isHot = new IsHotEvent();
        RaiseLocalEvent(lighter, isHot);
        return isHot.IsHot;
    }

    private void LightWorn(EntityUid user, EntityUid target, EntityUid lighter)
    {
        if (WornSmokable(target) is not { } smokable)
            return;

        _smoking.TryLight(smokable);
        if (smokable.Comp.State == SmokableState.Lit && user != target)
            _popup.PopupEntity(Loc.GetString("smokable-light-other", ("user", user), ("target", target), ("lighter", lighter)), target);
    }

    private void OnBlowSmokeAction(Entity<SmokableComponent> ent, ref BlowSmokeActionEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;
        if (ent.Comp.State != SmokableState.Lit)
        {
            _popup.PopupEntity(Loc.GetString("smokable-blow-unlit"), user, user);
            return;
        }

        args.Handled = true;
        RaiseNetworkEvent(new SmokePuffEvent(GetNetEntity(user)), Filter.Pvs(user));

        if (FacedMob(user) is not { } target)
        {
            var line = _random.Next(1, SoloPuffMessages + 1);
            _popup.PopupEntity(Loc.GetString($"smokable-blow-{line}", ("user", user)), user);
            return;
        }

        _popup.PopupEntity(Loc.GetString("smokable-blow-face", ("user", user), ("target", target)), target, PopupType.Medium);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):user} blew smoke into the face of {ToPrettyString(target):target}");

        if (!_random.Prob(CoughChance))
            return;

        _chat.TryEmoteWithChat(target, Cough);
        if (_random.Prob(DropChance) && _hands.GetActiveItem(target) is { } held)
            _hands.TryDrop(target, held);
    }

    /// <summary>
    /// A mob on the tile the user faces, the one to get the smoke in the face (tg: face to face in front).
    /// </summary>
    private EntityUid? FacedMob(EntityUid user)
    {
        var (position, rotation) = _transform.GetWorldPositionRotation(user);
        var front = new MapCoordinates(position + rotation.GetCardinalDir().ToVec(), _transform.GetMapId(user));

        foreach (var mob in _lookup.GetEntitiesInRange<InventoryComponent>(front, FaceRange))
        {
            if (mob.Owner != user)
                return mob;
        }

        return null;
    }

    private EntityUid? MaskWearer(EntityUid smokable)
    {
        return _container.TryGetContainingContainer(smokable, out var container)
               && _inventory.TryGetSlotEntity(container.Owner, MaskSlot, out var worn)
               && worn == smokable
            ? container.Owner
            : null;
    }

    private Entity<SmokableComponent>? WornSmokable(EntityUid mob)
    {
        return _inventory.TryGetSlotEntity(mob, MaskSlot, out var worn) && TryComp<SmokableComponent>(worn, out var smokable)
            ? (worn.Value, smokable)
            : null;
    }
}
