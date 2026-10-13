using Content.Shared._Polonium.Xenonids;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Actions;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Egg;

public sealed partial class XenoEggSystem : EntitySystem
{
    private static readonly EntProtoId LayEggAction = "ActionXenoLayEgg";

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedFacehuggerSystem _facehugger = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedXenoWeedsSystem _weeds = default!;

    private static readonly SoundPathSpecifier EggBurstSound = new("/Audio/_RMC14/Xeno/alien_egg_burst.ogg");
    private static readonly SoundPathSpecifier EggClearSound = new("/Audio/_RMC14/Xeno/alien_resin_break1.ogg");
    private static readonly TimeSpan EggCheckDelay = TimeSpan.FromSeconds(0.5);

    private TimeSpan _eggLayCooldown = TimeSpan.FromSeconds(300);

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoOvipositorCapableComponent, XenoLayEggActionEvent>(OnLayEgg);
        SubscribeLocalEvent<XenoOvipositorCapableComponent, XenoLayEggDoAfterEvent>(OnLayEggDoAfter);

        SubscribeLocalEvent<XenoEggComponent, MapInitEvent>(OnEggMapInit);
        SubscribeLocalEvent<XenoEggComponent, ActivateInWorldEvent>(OnEggActivate);
        SubscribeLocalEvent<XenoEggComponent, DestructionEventArgs>(OnEggDestroyed);

        Subs.CVar(_config, RMCCVars.RMCXenoQueenEggLayCooldownSeconds, OnEggLayCooldownChanged, true);
    }

    private void OnEggLayCooldownChanged(float seconds)
    {
        _eggLayCooldown = TimeSpan.FromSeconds(Math.Max(0, seconds));
    }

    private void OnLayEgg(Entity<XenoOvipositorCapableComponent> xeno, ref XenoLayEggActionEvent args)
    {
        if (args.Handled || _timing.ApplyingState)
            return;

        if (IsOnLayCooldown(xeno))
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-egg-lay-cooldown"), xeno, xeno);
            return;
        }

        if (!CanLayEggHere(xeno, popup: true))
            return;

        args.Handled = true;

        _popup.PopupClient(Loc.GetString("cm-xeno-egg-lay-start"), xeno, xeno);

        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.LayDelay, new XenoLayEggDoAfterEvent(), xeno)
        {
            BreakOnMove = true,
            BlockDuplicate = true,
            BreakOnDamage = true,
            CancelDuplicate = true,
            NeedHand = false,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnLayEggDoAfter(Entity<XenoOvipositorCapableComponent> xeno, ref XenoLayEggDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (IsOnLayCooldown(xeno))
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-egg-lay-cooldown"), xeno, xeno);
            return;
        }

        if (!CanLayEggHere(xeno, popup: true))
            return;

        if (_net.IsClient)
            return;

        var coords = _rmcMap.SnapToGrid(_transform.GetMoverCoordinates(xeno.Owner));
        var egg = Spawn(xeno.Comp.EggPrototype, coords);
        _hive.SetSameHive(xeno.Owner, egg);

        if (_eggLayCooldown > TimeSpan.Zero)
        {
            xeno.Comp.NextLayEgg = _timing.CurTime + _eggLayCooldown;
            xeno.Comp.LayReadyNotified = false;
            Dirty(xeno);
            SetLayEggActionCooldown(xeno.Owner, _eggLayCooldown);
        }
    }

    private bool IsOnLayCooldown(Entity<XenoOvipositorCapableComponent> xeno)
    {
        return xeno.Comp.NextLayEgg is { } next && _timing.CurTime < next;
    }

    private void SetLayEggActionCooldown(EntityUid xeno, TimeSpan cooldown)
    {
        foreach (var action in _actions.GetActions(xeno))
        {
            if (MetaData(action).EntityPrototype?.ID is not { } protoId || protoId != LayEggAction)
                continue;

            _actions.SetCooldown(action.Owner, cooldown);
            return;
        }
    }

    private bool CanLayEggHere(EntityUid xeno, bool popup)
    {
        var coords = _transform.GetMoverCoordinates(xeno);

        if (_transform.GetGrid(coords) is not { } gridUid || !HasComp<MapGridComponent>(gridUid))
        {
            if (popup)
                _popup.PopupClient(Loc.GetString("cm-xeno-egg-failed-need-grid"), xeno, xeno);
            return false;
        }

        var snapped = _rmcMap.SnapToGrid(coords);
        if (!_weeds.IsOnFriendlyWeeds(xeno) && !_weeds.IsOnWeeds(snapped))
        {
            if (popup)
                _popup.PopupClient(Loc.GetString("cm-xeno-egg-failed-must-weeds"), xeno, xeno);
            return false;
        }

        if (_rmcMap.HasAnchoredEntityEnumerator<XenoEggComponent>(snapped))
        {
            if (popup)
                _popup.PopupClient(Loc.GetString("cm-xeno-egg-failed-already-there"), xeno, xeno);
            return false;
        }

        return true;
    }

    private void OnEggMapInit(Entity<XenoEggComponent> egg, ref MapInitEvent args)
    {
        var seconds = _random.NextDouble(egg.Comp.MinGrowTime.TotalSeconds, egg.Comp.MaxGrowTime.TotalSeconds);
        egg.Comp.NextGrow = _timing.CurTime + TimeSpan.FromSeconds(seconds);
        Dirty(egg);
        _appearance.SetData(egg, XenoEggVisuals.State, egg.Comp.State);
    }

    // Polonium: jak w NSV13 ksenomorf wyjmuje facehuggera z dojrzałego jaja i sprząta pustą skorupę.
    private void OnEggActivate(Entity<XenoEggComponent> egg, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;

        if (!HasComp<XenoComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("xeno-egg-slimy"), egg, args.User);
            return;
        }

        switch (egg.Comp.State)
        {
            case XenoEggState.Growing:
                _popup.PopupEntity(Loc.GetString("xeno-egg-not-grown"), egg, args.User);
                break;
            case XenoEggState.Grown:
                _popup.PopupEntity(Loc.GetString("xeno-egg-retrieve"), egg, args.User);
                StartOpening(egg, kill: false);
                break;
            case XenoEggState.Opening:
                _popup.PopupEntity(Loc.GetString("xeno-egg-opening"), egg, args.User);
                break;
            case XenoEggState.Opened:
                _popup.PopupEntity(Loc.GetString("xeno-egg-clear"), egg, args.User);
                if (_net.IsServer)
                {
                    _audio.PlayPvs(EggClearSound, egg);
                    QueueDel(egg);
                }
                break;
        }
    }

    // Zniszczone jajo zabija facehuggera w środku.
    private void OnEggDestroyed(Entity<XenoEggComponent> egg, ref DestructionEventArgs args)
    {
        if (_net.IsClient || egg.Comp.State == XenoEggState.Opened)
            return;

        var hugger = Spawn(egg.Comp.SpawnPrototype, Transform(egg).Coordinates);
        if (TryComp<FacehuggerComponent>(hugger, out var comp))
            _facehugger.Die((hugger, comp));
    }

    private void StartOpening(Entity<XenoEggComponent> egg, bool kill)
    {
        if (egg.Comp.State is not (XenoEggState.Growing or XenoEggState.Grown))
            return;

        egg.Comp.State = XenoEggState.Opening;
        egg.Comp.KillChild = kill;
        egg.Comp.OpenAt = _timing.CurTime + egg.Comp.OpenTime;
        Dirty(egg);
        _appearance.SetData(egg, XenoEggVisuals.State, egg.Comp.State);
    }

    private void FinishOpening(Entity<XenoEggComponent> egg)
    {
        egg.Comp.State = XenoEggState.Opened;
        Dirty(egg);
        _appearance.SetData(egg, XenoEggVisuals.State, egg.Comp.State);

        var coords = Transform(egg).Coordinates;
        _audio.PlayPvs(EggBurstSound, coords);

        var hugger = Spawn(egg.Comp.SpawnPrototype, coords);

        if (!TryComp<FacehuggerComponent>(hugger, out var comp))
            return;

        if (egg.Comp.KillChild)
            _facehugger.Die((hugger, comp));
        else
            _facehugger.TryLeapAtNearby((hugger, comp));
    }

    private bool HasVictimNearby(Entity<XenoEggComponent> egg)
    {
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(egg).Coordinates, egg.Comp.TriggerRange))
        {
            if (_facehugger.CanHug(mob) && _interaction.InRangeUnobstructed(egg.Owner, mob.Owner, egg.Comp.TriggerRange))
                return true;
        }

        return false;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;

        var queens = EntityQueryEnumerator<XenoOvipositorCapableComponent>();
        while (queens.MoveNext(out var uid, out var capable))
        {
            if (capable.LayReadyNotified || capable.NextLayEgg is not { } next || time < next)
                continue;

            capable.LayReadyNotified = true;
            Dirty(uid, capable);
            _popup.PopupClient(Loc.GetString("cm-xeno-egg-lay-ready"), uid, uid);
        }

        var eggs = EntityQueryEnumerator<XenoEggComponent>();
        while (eggs.MoveNext(out var uid, out var egg))
        {
            var ent = new Entity<XenoEggComponent>(uid, egg);
            switch (egg.State)
            {
                case XenoEggState.Growing:
                    if (time < egg.NextGrow)
                        continue;

                    egg.State = XenoEggState.Grown;
                    Dirty(uid, egg);
                    _appearance.SetData(uid, XenoEggVisuals.State, egg.State);
                    break;
                case XenoEggState.Grown:
                    if (time < egg.NextCheck)
                        continue;

                    egg.NextCheck = time + EggCheckDelay;
                    if (HasVictimNearby(ent))
                        StartOpening(ent, kill: false);
                    break;
                case XenoEggState.Opening:
                    if (time >= egg.OpenAt)
                        FinishOpening(ent);
                    break;
            }
        }
    }
}
