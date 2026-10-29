using Content.Shared._RMC14.Xenonids;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Throwing;
using Content.Shared.Zombies;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Polonium.Xenonids;

public abstract partial class SharedFacehuggerSystem : EntitySystem
{
    public const string MaskSlot = "mask";
    public const string HeadSlot = "head";

    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected IRobustRandom Random = default!;
    [Dependency] protected INetManager Net = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;
    [Dependency] protected InventorySystem Inventory = default!;
    [Dependency] protected SharedInteractionSystem Interaction = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private ClothingSystem _clothing = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<MobStateComponent>> _nearby = new();

    private static readonly DamageSpecifier AttachDamage = new()
    {
        DamageDict = { ["Slash"] = 5 },
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FacehuggerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FacehuggerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<FacehuggerComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<FacehuggerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<FacehuggerComponent, ThrowDoHitEvent>(OnThrowHit);
        SubscribeLocalEvent<FacehuggerComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<FacehuggerComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<FacehuggerComponent, DamageDealtEvent>(OnDamageDealt);
    }

    private void OnMapInit(Entity<FacehuggerComponent> ent, ref MapInitEvent args)
    {
        UpdateVisuals(ent);
    }

    private void OnExamined(Entity<FacehuggerComponent> ent, ref ExaminedEvent args)
    {
        var key = ent.Comp.State switch
        {
            FacehuggerState.Active => "xeno-facehugger-examine-active",
            _ => "xeno-facehugger-examine-still",
        };
        args.PushMarkup(Loc.GetString(key));

        if (ent.Comp.Sterile)
            args.PushMarkup(Loc.GetString("xeno-facehugger-examine-sterile"));
    }

    /// <summary>
    /// Aktywnego facehuggera nie da się podnieść. Kto spróbuje, dostaje nim w twarz.
    /// </summary>
    private void OnPickupAttempt(Entity<FacehuggerComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (ent.Comp.State != FacehuggerState.Active || ent.Comp.Sterile || HasComp<XenoComponent>(args.User))
            return;

        args.Cancel();

        if (Net.IsServer)
            TryLeap(ent, args.User);
    }

    private void OnAfterInteract(Entity<FacehuggerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == args.User)
            return;

        if (ent.Comp.State != FacehuggerState.Active || !HasComp<MobStateComponent>(target))
            return;

        args.Handled = true;

        if (Net.IsServer)
            TryLeap(ent, target);
    }

    private void OnThrowHit(Entity<FacehuggerComponent> ent, ref ThrowDoHitEvent args)
    {
        if (Net.IsServer && ent.Comp.State == FacehuggerState.Active)
            TryLeap(ent, args.Target);
    }

    private void OnEquipped(Entity<FacehuggerComponent> ent, ref GotEquippedEvent args)
    {
        if (args.Slot != MaskSlot || ent.Comp.State != FacehuggerState.Active || !CanHug(args.EquipTarget, checkHead: false, hugger: ent))
            return;

        Attach(ent, args.EquipTarget);
    }

    private void OnUnequipped(Entity<FacehuggerComponent> ent, ref GotUnequippedEvent args)
    {
        if (ent.Comp.Victim != args.EquipTarget)
            return;

        // Zdjęty przed czasem: nic nie złożył i za chwilę znów zacznie polować.
        ent.Comp.Victim = null;
        ent.Comp.ImpregnateAt = null;
        if (ent.Comp.State == FacehuggerState.Idle)
            ent.Comp.WakeAt = Timing.CurTime + RandomTime(ent.Comp.MinIdleTime, ent.Comp.MaxIdleTime);
        Dirty(ent);
    }

    private void OnDamageDealt(Entity<FacehuggerComponent> ent, ref DamageDealtEvent args)
    {
        if (!Net.IsServer)
            return;

        var total = args.Damage.GetTotal();
        if (total <= 0)
            return;

        ent.Comp.DamageTaken += total.Float();
        if (ent.Comp.DamageTaken >= ent.Comp.DeathDamage)
            Die(ent);
    }

    /// <summary>
    /// Czy facehugger może się przyczepić do tego moba. Odpowiednik CanHug z NSV13.
    /// </summary>
    /// <param name="checkHead">Czy hełm zasłaniający usta ma odstraszać. Jajo i polujący facehugger omijają osoby
    /// w hełmach. Bez tego sprawdzenia (rzut, próba podniesienia) facehugger skacze i rozbija się o hełm.</param>
    /// <param name="hugger">Facehugger, który już siedzi w slocie maski i nie ma się liczyć jako zajęta twarz.</param>
    public bool CanHug(EntityUid target, bool checkHead = true, EntityUid? hugger = null)
    {
        if (HasComp<XenoComponent>(target)
            || HasComp<XenoInfectedComponent>(target)
            || HasComp<ZombieComponent>(target))
            return false;

        if (!TryComp<MobStateComponent>(target, out var mobState) || _mobState.IsDead(target, mobState))
            return false;

        if (!HasComp<BodyComponent>(target) || !HasComp<BloodstreamComponent>(target))
            return false;

        if (!Inventory.HasSlot(target, MaskSlot))
            return false;

        if (Inventory.TryGetSlotEntity(target, MaskSlot, out var mask)
            && mask != hugger
            && TryComp<FacehuggerComponent>(mask, out var worn)
            && worn.ImpregnateAt != null)
            return false;

        return !checkHead || !IsMouthCoveredByHead(target, out _);
    }

    public bool IsMouthCoveredByHead(EntityUid target, out EntityUid head)
    {
        head = default;
        if (!Inventory.TryGetSlotEntity(target, HeadSlot, out var headItem))
            return false;

        head = headItem.Value;
        return TryComp<IngestionBlockerComponent>(headItem, out var blocker) && blocker.Enabled;
    }

    /// <summary>
    /// Skok na twarz. Działa tylko z bliska, tak jak Leap w NSV13.
    /// </summary>
    public bool TryLeap(Entity<FacehuggerComponent> hugger, EntityUid target)
    {
        if (!Net.IsServer || hugger.Comp.State != FacehuggerState.Active
            || TerminatingOrDeleted(hugger) || TerminatingOrDeleted(target)
            || !CanHug(target, checkHead: false))
            return false;

        PopupBoth(target,
            Loc.GetString("xeno-facehugger-leap-self", ("hugger", hugger.Owner)),
            Loc.GetString("xeno-facehugger-leap-others", ("hugger", hugger.Owner), ("target", target)),
            PopupType.LargeCaution);

        if (IsMouthCoveredByHead(target, out var head))
        {
            PopupBoth(target,
                Loc.GetString("xeno-facehugger-smash-self", ("hugger", hugger.Owner), ("helmet", head)),
                Loc.GetString("xeno-facehugger-smash-others", ("hugger", hugger.Owner), ("target", target), ("helmet", head)),
                PopupType.Medium);
            Die(hugger);
            return false;
        }

        var torn = Inventory.TryGetSlotEntity(target, MaskSlot, out var mask)
            && Inventory.TryUnequip(target, MaskSlot, silent: true, force: true);

        if (!Inventory.TryEquip(target, hugger, MaskSlot, silent: true, force: true))
        {
            if (torn)
                Inventory.TryEquip(target, mask!.Value, MaskSlot, silent: true, force: true);
            return false;
        }

        if (torn)
        {
            PopupBoth(target,
                Loc.GetString("xeno-facehugger-tear-self", ("hugger", hugger.Owner), ("mask", mask!.Value)),
                Loc.GetString("xeno-facehugger-tear-others", ("hugger", hugger.Owner), ("target", target), ("mask", mask!.Value)),
                PopupType.Medium);
        }

        return true;
    }

    /// <summary>
    /// Skacze na pierwszą osobę obok, której twarz nie jest osłonięta.
    /// </summary>
    public bool TryLeapAtNearby(Entity<FacehuggerComponent> hugger, TransformComponent? xform = null)
    {
        if (!Resolve(hugger, ref xform) || hugger.Comp.State != FacehuggerState.Active || TerminatingOrDeleted(hugger))
            return false;

        _nearby.Clear();
        _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(hugger, xform), hugger.Comp.LeapRange, _nearby);

        foreach (var target in _nearby)
        {
            if (!CanHug(target) ||!Interaction.InRangeUnobstructed(hugger.Owner, target.Owner, hugger.Comp.LeapRange))
                continue;

            if (TryLeap(hugger, target))
                return true;

            // Rozbił się o hełm.
            if (hugger.Comp.State != FacehuggerState.Active)
                return false;
        }

        return false;
    }

    private void Attach(Entity<FacehuggerComponent> hugger, EntityUid victim)
    {
        if (!Net.IsServer)
            return;

        hugger.Comp.Victim = victim;
        hugger.Comp.ImpregnateAt = Timing.CurTime + RandomTime(hugger.Comp.MinImpregnationTime, hugger.Comp.MaxImpregnationTime);

        if (!hugger.Comp.Sterile)
        {
            _damageable.TryChangeDamage(victim, AttachDamage, origin: hugger);
            _statusEffects.TryAddStatusEffectDuration(victim, SleepingSystem.StatusEffectForcedSleeping, hugger.Comp.KnockoutTime);
        }

        // Żeby nie skoczył od razu na tego, kto go zerwie.
        SetState(hugger, FacehuggerState.Idle);
        hugger.Comp.WakeAt = null;
        Dirty(hugger);
    }

    public void SetState(Entity<FacehuggerComponent> ent, FacehuggerState state)
    {
        if (ent.Comp.State == state)
            return;

        ent.Comp.State = state;
        Dirty(ent);
        UpdateVisuals(ent);
    }

    public void Die(Entity<FacehuggerComponent> ent, bool impregnated = false)
    {
        if (ent.Comp.State is FacehuggerState.Dead or FacehuggerState.Impregnated)
            return;

        ent.Comp.ImpregnateAt = null;
        ent.Comp.WakeAt = null;
        SetState(ent, impregnated ? FacehuggerState.Impregnated : FacehuggerState.Dead);

        if (!impregnated)
            Popup.PopupEntity(Loc.GetString("xeno-facehugger-die", ("hugger", ent.Owner)), ent, PopupType.Small);
    }

    private void UpdateVisuals(Entity<FacehuggerComponent> ent)
    {
        _appearance.SetData(ent, FacehuggerVisuals.State, ent.Comp.State);

        var prefix = ent.Comp.State switch
        {
            FacehuggerState.Active => null,
            FacehuggerState.Idle => "inactive",
            _ => "dead",
        };
        _clothing.SetEquippedPrefix(ent, prefix);
        _item.SetHeldPrefix(ent, ent.Comp.State == FacehuggerState.Active ? "active" : null);
    }

    protected void PopupBoth(EntityUid target, string selfMessage, string othersMessage, PopupType type)
    {
        Popup.PopupEntity(selfMessage, target, target, type);
        Popup.PopupEntity(othersMessage, target, Filter.PvsExcept(target), true, type);
    }

    protected TimeSpan RandomTime(TimeSpan min, TimeSpan max)
    {
        return TimeSpan.FromSeconds(Random.NextDouble(min.TotalSeconds, max.TotalSeconds));
    }
}
