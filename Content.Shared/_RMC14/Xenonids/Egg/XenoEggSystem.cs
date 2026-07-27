using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Egg;

public sealed partial class XenoEggSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedXenoWeedsSystem _weeds = default!;

    private static readonly SoundPathSpecifier EggBurstSound = new("/Audio/_RMC14/Xeno/alien_egg_burst.ogg");

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoOvipositorCapableComponent, XenoLayEggActionEvent>(OnLayEgg);
        SubscribeLocalEvent<XenoOvipositorCapableComponent, XenoLayEggDoAfterEvent>(OnLayEggDoAfter);

        SubscribeLocalEvent<XenoEggComponent, MapInitEvent>(OnEggMapInit);
        SubscribeLocalEvent<XenoEggComponent, StepTriggeredOffEvent>(OnEggStepped);
        SubscribeLocalEvent<XenoEggComponent, ActivateInWorldEvent>(OnEggActivate);
    }

    private void OnLayEgg(Entity<XenoOvipositorCapableComponent> xeno, ref XenoLayEggActionEvent args)
    {
        if (args.Handled || _timing.ApplyingState)
            return;

        if (!CanLayEggHere(xeno, popup: true))
            return;

        args.Handled = true;

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

        if (!CanLayEggHere(xeno, popup: true))
            return;

        if (_net.IsClient)
            return;

        var coords = _rmcMap.SnapToGrid(_transform.GetMoverCoordinates(xeno.Owner));
        var egg = Spawn(xeno.Comp.EggPrototype, coords);
        _hive.SetSameHive(xeno.Owner, egg);
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
        egg.Comp.NextGrow = _timing.CurTime + egg.Comp.GrowTime;
        Dirty(egg);
    }

    private void OnEggStepped(Entity<XenoEggComponent> egg, ref StepTriggeredOffEvent args)
    {
        if (_net.IsClient || !egg.Comp.Grown)
            return;

        if (!HasComp<MobStateComponent>(args.Tripper) || HasComp<XenoComponent>(args.Tripper))
            return;

        TryHatch(egg);
    }

    private void OnEggActivate(Entity<XenoEggComponent> egg, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (!HasComp<XenoComponent>(args.User) || !egg.Comp.Grown)
            return;

        args.Handled = true;
        TryHatch(egg);
    }

    private void TryHatch(Entity<XenoEggComponent> egg)
    {
        if (_net.IsClient || !egg.Comp.Grown)
            return;

        var coords = Transform(egg).Coordinates;
        Spawn(egg.Comp.SpawnPrototype, coords);
        _audio.PlayPvs(EggBurstSound, coords);
        QueueDel(egg);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var eggs = EntityQueryEnumerator<XenoEggComponent>();
        while (eggs.MoveNext(out var uid, out var egg))
        {
            if (!egg.Grown)
            {
                if (time < egg.NextGrow)
                    continue;

                egg.Grown = true;
                egg.NextHatch = time + egg.HatchDelay;
                Dirty(uid, egg);
                continue;
            }

            if (time < egg.NextHatch)
                continue;

            TryHatch((uid, egg));
        }
    }
}
