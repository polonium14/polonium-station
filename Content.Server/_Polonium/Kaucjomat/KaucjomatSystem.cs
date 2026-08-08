using System.Numerics;
using Content.Server.Stack;
using Content.Shared._Polonium.Kaucjomat;
using Content.Shared.Audio;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Construction.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.Emag.Systems;
using Content.Shared.Interaction;
using Content.Shared.Jittering;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Kaucjomat;

/// <summary>
/// Deposit-return machine. A deposit runs on the machine's own clock, not the depositor's:
/// swallow the item, rattle, go quiet, then pay out or spit it back over their shoulder.
/// </summary>
public sealed partial class KaucjomatSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _receiver = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private EmagSystem _emag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KaucjomatComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<KaucjomatComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<KaucjomatComponent, BreakageEventArgs>(OnBreak);
        SubscribeLocalEvent<KaucjomatComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<KaucjomatComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<KaucjomatComponent, UnanchorAttemptEvent>(OnUnanchorAttempt);
        SubscribeLocalEvent<KaucjomatComponent, GotEmaggedEvent>(OnEmagged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<KaucjomatComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ShakeEnd == null && comp.VerdictAt == null && comp.ResultEnd == null && comp.LastDispense == null)
                continue;

            var ent = (uid, comp);
            var dead = comp.Broken || !_receiver.IsPowered(uid);

            if (!dead && comp.LastDispense is not null && now > comp.LastDispense + comp.DispenseCooldown)
            {
                var cash = _stack.SpawnAtPosition(50, comp.Currency, EjectCoordinates(ent));

                _throwing.TryThrow(cash,
                    comp.DepositDirection,
                    compensateFriction: true,
                    playSound: false,
                    doSpin: false);
                _transform.SetLocalRotation(cash, _random.NextAngle());
                _audio.PlayPvs(comp.SoundAccept, uid);

                comp.LastDispense = now;
                comp.DispensedAmount += 50;
                if (comp.DispensedAmount >= comp.DispenseAmountMax)
                {
                    comp.LastDispense = null;
                }
            }
            // Rattle for the first stretch, then go quiet while it "thinks".
            if (comp.ShakeEnd != null && (now >= comp.ShakeEnd || dead))
                StopShaking(ent);

            if (comp.VerdictAt != null)
            {
                // Losing power or getting smashed mid-cycle just spits the deposit back out.
                if (dead)
                    Abort(ent);
                else if (now >= comp.VerdictAt)
                    ResolveDeposit(ent);
            }

            if (comp.ResultEnd == null || now < comp.ResultEnd)
                continue;

            comp.ResultEnd = null;
            comp.ResultState = KaucjomatVisualState.Normal;
            UpdateVisuals(ent);
        }
    }

    private void OnMapInit(Entity<KaucjomatComponent> ent, ref MapInitEvent args)
    {
        _container.EnsureContainer<ContainerSlot>(ent.Owner, ent.Comp.ContainerId);
        UpdateVisuals(ent);
    }

    private void OnPowerChanged(Entity<KaucjomatComponent> ent, ref PowerChangedEvent args)
    {
        UpdateVisuals(ent);
    }

    private void OnBreak(Entity<KaucjomatComponent> ent, ref BreakageEventArgs args)
    {
        ent.Comp.Broken = true;
        ent.Comp.ResultEnd = null;
        Abort(ent);
        UpdateVisuals(ent);
    }

    private void OnDamageChanged(Entity<KaucjomatComponent> ent, ref DamageChangedEvent args)
    {
        // Welding it back together clears the broken face, same as a vending machine.
        if (args.DamageIncreased || !ent.Comp.Broken)
            return;

        ent.Comp.Broken = false;
        UpdateVisuals(ent);
    }

    private void OnUnanchorAttempt(Entity<KaucjomatComponent> ent, ref UnanchorAttemptEvent args)
    {
        // Same guard the biomass reclaimer uses: don't let it be unbolted mid-cycle with
        // someone's deposit still inside.
        if (ent.Comp.VerdictAt == null)
            return;

        _popup.PopupEntity(Loc.GetString("kaucjomat-busy"), ent.Owner, args.User);
        args.Cancel();
    }

    private void OnInteractUsing(Entity<KaucjomatComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Broken || !_receiver.IsPowered(ent.Owner))
            return;

        if (ent.Comp.LastDispense is not null) // disabled for the emag "cutscene"
            return;
        // Not something the machine is willing to swallow at all - let other interactions have it.
        if (_whitelist.IsWhitelistFailOrNull(ent.Comp.Whitelist, args.Used))
            return;

        args.Handled = true;

        // One item at a time, no matter how many people are queueing up.
        if (ent.Comp.VerdictAt != null || ent.Comp.ResultEnd != null)
        {
            _popup.PopupEntity(Loc.GetString("kaucjomat-busy"), ent.Owner, args.User);
            return;
        }

        // Anything that isn't a returnable container is refused on the spot - the machine
        // doesn't waste five seconds grinding on a banana peel.
        if (GetPayout(ent.Comp, args.Used) == null)
        {
            Deny(ent, args.User, "kaucjomat-deny-not-deposit");
            return;
        }

        if (!IsEmpty(ent.Comp, args.Used))
        {
            Deny(ent, args.User, "kaucjomat-deny-not-empty");
            return;
        }

        if (!_container.TryGetContainer(ent.Owner, ent.Comp.ContainerId, out var slot)
            || !_container.Insert(args.Used, slot))
        {
            Log.Error($"{ToPrettyString(ent)} could not take {ToPrettyString(args.Used)} into its deposit slot.");
            Deny(ent, args.User, "kaucjomat-error");
            return;
        }

        ent.Comp.Depositor = args.User;
        ent.Comp.DepositDirection = DirectionFrom(ent.Owner, args.User);
        ent.Comp.VerdictAt = _timing.CurTime + ent.Comp.ShakeDuration + ent.Comp.PauseDuration;
        StartShaking(ent);
    }

    private void OnEmagged(Entity<KaucjomatComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (_emag.CheckFlag(ent, EmagType.Interaction))
            return;
        ent.Comp.DispenseAmountMax = _random.Next(10, 101) * 50;
        ent.Comp.LastDispense = _timing.CurTime;
        args.Handled = true;
    }

    private void StartShaking(Entity<KaucjomatComponent> ent)
    {
        ent.Comp.ShakeEnd = _timing.CurTime + ent.Comp.ShakeDuration;
        _jitter.AddJitter(ent.Owner, ent.Comp.JitterAmplitude, ent.Comp.JitterFrequency);
        _audio.PlayPvs(ent.Comp.SoundStartup, ent.Owner);
        _ambient.SetAmbience(ent.Owner, true);
    }

    private void StopShaking(Entity<KaucjomatComponent> ent)
    {
        if (ent.Comp.ShakeEnd == null)
            return;

        ent.Comp.ShakeEnd = null;
        RemComp<JitteringComponent>(ent.Owner);
        _ambient.SetAmbience(ent.Owner, false);
    }

    /// <summary>
    /// The verdict, once the machine has finished rattling and thinking.
    /// </summary>
    private void ResolveDeposit(Entity<KaucjomatComponent> ent)
    {
        var depositor = ent.Comp.Depositor;
        ent.Comp.VerdictAt = null;
        ent.Comp.Depositor = null;

        if (TakeDeposit(ent) is not { } item)
            return;

        // Recheck: someone could have refilled it through the slot, or swapped it out.
        if (GetPayout(ent.Comp, item) is not { } payout)
        {
            Reject(ent, depositor, item, "kaucjomat-deny-not-deposit");
            return;
        }

        if (!IsEmpty(ent.Comp, item))
        {
            Reject(ent, depositor, item, "kaucjomat-deny-not-empty");
            return;
        }

        // The machine is allowed to be a dick about it.
        if (_random.Prob(ent.Comp.DenyChance))
        {
            Reject(ent, depositor, item, "kaucjomat-deny-random");
            return;
        }

        QueueDel(item);
        if (!_emag.CheckFlag(ent, EmagType.Interaction)) // scammer
        {
            var cash = _stack.SpawnAtPosition(payout, ent.Comp.Currency, EjectCoordinates(ent));

            // doSpin would line the bills up with the throw - let them land at any angle instead.
            _throwing.TryThrow(cash,
                ent.Comp.DepositDirection,
                compensateFriction: true,
                playSound: false,
                doSpin: false);
            _transform.SetLocalRotation(cash, _random.NextAngle());
        }

        _audio.PlayPvs(ent.Comp.SoundAccept, ent.Owner);
        Announce(ent, depositor, Loc.GetString("kaucjomat-accept", ("amount", payout)));
        SetResult(ent, KaucjomatVisualState.Accept);
    }

    /// <summary>
    /// Gives the deposit back without a verdict - the machine lost power or got smashed.
    /// </summary>
    private void Abort(Entity<KaucjomatComponent> ent)
    {
        ent.Comp.VerdictAt = null;
        ent.Comp.Depositor = null;
        StopShaking(ent);

        if (TakeDeposit(ent) is { } item)
            _throwing.TryThrow(item, ent.Comp.DepositDirection, compensateFriction: true);
    }

    /// <summary>
    /// Pulls whatever is in the deposit slot back out into the world, in front of the machine.
    /// </summary>
    private EntityUid? TakeDeposit(Entity<KaucjomatComponent> ent)
    {
        if (!_container.TryGetContainer(ent.Owner, ent.Comp.ContainerId, out var slot))
            return null;

        if (slot.ContainedEntities.Count == 0)
            return null;

        var item = slot.ContainedEntities[0];
        var where = EjectCoordinates(ent);

        // Forcing on the retry matters: a slot we cannot empty stays occupied forever, and every
        // later deposit would fail to insert.
        if (!_container.Remove(item, slot, destination: where)
            && !_container.Remove(item, slot, force: true, destination: where))
        {
            Log.Error($"{ToPrettyString(ent)} could not eject {ToPrettyString(item)} from its deposit slot.");
            return null;
        }

        return TerminatingOrDeleted(item) ? null : item;
    }

    /// <summary>
    /// Where the machine drops things before flinging them - a step out from its own tile, so the
    /// wallmount variant doesn't eject into the wall it is bolted to.
    /// </summary>
    private EntityCoordinates EjectCoordinates(Entity<KaucjomatComponent> ent)
    {
        var xform = Transform(ent.Owner);

        // DepositDirection is world-space; the coordinates we offset are parent-local.
        var local = (-_transform.GetWorldRotation(xform.ParentUid)).RotateVec(ent.Comp.DepositDirection);

        return xform.Coordinates.Offset(local * ent.Comp.EjectOffset);
    }

    private void Deny(Entity<KaucjomatComponent> ent, EntityUid? user, string message)
    {
        _audio.PlayPvs(ent.Comp.SoundDeny, ent.Owner);
        Announce(ent, user, Loc.GetString(message));
        SetResult(ent, KaucjomatVisualState.Deny);
    }

    /// <summary>
    /// Refuses a deposit the machine already swallowed, flinging it back out over the
    /// depositor's shoulder.
    /// </summary>
    private void Reject(Entity<KaucjomatComponent> ent, EntityUid? user, EntityUid item, string message)
    {
        Deny(ent, user, message);

        var spread = ent.Comp.RejectSpread;
        var skew = new Angle(MathHelper.DegreesToRadians(_random.NextFloat(-spread, spread)));
        var distance = _random.NextFloat(ent.Comp.RejectDistance.X, ent.Comp.RejectDistance.Y);

        _throwing.TryThrow(item, skew.RotateVec(ent.Comp.DepositDirection) * distance, compensateFriction: true);
    }

    /// <summary>
    /// Unit vector pointing from the machine towards the given entity.
    /// </summary>
    private Vector2 DirectionFrom(EntityUid machine, EntityUid user)
    {
        var away = _transform.GetWorldPosition(user) - _transform.GetWorldPosition(machine);

        // Depositor is standing on top of the machine somehow - fall back to whichever way it faces.
        if (away.LengthSquared() < 0.01f)
            away = _transform.GetWorldRotation(machine).ToWorldVec();

        return away.Normalized();
    }

    private void Announce(Entity<KaucjomatComponent> ent, EntityUid? user, string message)
    {
        if (user is { } actual && !TerminatingOrDeleted(actual))
            _popup.PopupEntity(message, ent.Owner, actual);
        else
            _popup.PopupEntity(message, ent.Owner);
    }

    private void SetResult(Entity<KaucjomatComponent> ent, KaucjomatVisualState state)
    {
        ent.Comp.ResultState = state;
        ent.Comp.ResultEnd = _timing.CurTime + ent.Comp.ResultDuration;
        UpdateVisuals(ent);
    }

    /// <summary>
    /// Payout the item would earn if it were empty, or null if it isn't a returnable container.
    /// </summary>
    private int? GetPayout(KaucjomatComponent comp, EntityUid item)
    {
        foreach (var deposit in comp.Deposits)
        {
            if (_tag.HasTag(item, deposit.Tag))
                return deposit.Payout;
        }

        return null;
    }

    private bool IsEmpty(KaucjomatComponent comp, EntityUid item)
    {
        // No drink solution at all means it isnt a container we can judge - treat it as not empty.
        return _solution.TryGetSolution(item, comp.SolutionName, out _, out var solution)
               && solution.Volume <= 0;
    }

    private void UpdateVisuals(Entity<KaucjomatComponent> ent)
    {
        var state = KaucjomatVisualState.Normal;

        if (ent.Comp.Broken)
            state = KaucjomatVisualState.Broken;
        else if (!_receiver.IsPowered(ent.Owner))
            state = KaucjomatVisualState.Off;
        else if (ent.Comp.ResultEnd != null)
            state = ent.Comp.ResultState;

        if (_light.TryGetLight(ent.Owner, out var light))
            _light.SetEnabled(ent.Owner, state is not (KaucjomatVisualState.Off or KaucjomatVisualState.Broken), light);

        _appearance.SetData(ent.Owner, KaucjomatVisuals.State, state);
    }
}
