using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Lightning;
using Content.Server.Lightning.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Chat;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Trigger;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server._Polonium.Abductors;

public sealed partial class AbductorGasBurstOnTriggerSystem : XOnTriggerSystem<AbductorGasBurstOnTriggerComponent>
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;

    protected override void OnTrigger(Entity<AbductorGasBurstOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        if (_atmosphere.GetContainingMixture(target, false, true) is not { } air)
            return;

        air.AdjustMoles(ent.Comp.Gas, ent.Comp.Moles);
        args.Handled = true;
    }
}

public sealed partial class AbductorArcsOnTriggerSystem : XOnTriggerSystem<AbductorArcsOnTriggerComponent>
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private LightningSystem _lightning = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<LightningTargetComponent>> _inRange = new();
    private readonly List<EntityUid> _targets = new();

    protected override void OnTrigger(Entity<AbductorArcsOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        _inRange.Clear();
        _targets.Clear();
        _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(target), ent.Comp.Range, _inRange);
        foreach (var candidate in _inRange)
        {
            if (candidate.Owner != target)
                _targets.Add(candidate);
        }

        _random.Shuffle(_targets);
        var bolts = Math.Min(ent.Comp.Bolts, _targets.Count);
        for (var i = 0; i < bolts; i++)
        {
            _lightning.ShootLightning(target, _targets[i], ent.Comp.Lightning, triggerLightningEvents: false);
        }

        args.Handled = true;
    }
}

public sealed partial class AbductorCreatureCallOnTriggerSystem : XOnTriggerSystem<AbductorCreatureCallOnTriggerComponent>
{
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    protected override void OnTrigger(Entity<AbductorCreatureCallOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        if (!_blocker.CanEmote(target))
            return;

        var call = _random.Pick(ent.Comp.Calls);
        _chat.TrySendInGameICMessage(target, Loc.GetString(call.Emote), InGameICChatType.Emote, ChatTransmitRange.Normal, checkRadioPrefix: false);
        _audio.PlayPvs(call.Sound, target);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Popup), target, target);
        args.Handled = true;
    }
}

public sealed partial class AbductorSwapOnTriggerSystem : XOnTriggerSystem<AbductorSwapOnTriggerComponent>
{
    [Dependency] private AbductorShipSystem _ship = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<MobStateComponent>> _inRange = new();
    private readonly List<EntityUid> _targets = new();

    protected override void OnTrigger(Entity<AbductorSwapOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        if (_container.IsEntityInContainer(target) || IsBuckled(target))
            return;

        _inRange.Clear();
        _targets.Clear();
        _lookup.GetEntitiesInRange(Transform(target).Coordinates, ent.Comp.Range, _inRange);
        foreach (var candidate in _inRange)
        {
            if (candidate.Owner != target
                && _mobState.IsAlive(candidate, candidate.Comp)
                && !_container.IsEntityInContainer(candidate)
                && !IsBuckled(candidate))
            {
                _targets.Add(candidate);
            }
        }

        if (_targets.Count == 0)
            return;

        var other = _random.Pick(_targets);
        _ship.StopPulling(target);
        _ship.StopPulling(other);
        _transform.SwapPositions(target, other);

        var popup = Loc.GetString(ent.Comp.Popup);
        _popup.PopupEntity(popup, target, target, PopupType.MediumCaution);
        _popup.PopupEntity(popup, other, other, PopupType.MediumCaution);
        args.Handled = true;
    }

    private bool IsBuckled(EntityUid uid)
    {
        return TryComp<BuckleComponent>(uid, out var buckle) && buckle.Buckled;
    }
}
