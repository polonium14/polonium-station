using Content.Client.Disposal.Unit;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Disposal.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Disposal.UI;

/// <summary>
/// What a disposal or mailing unit window shows, read from the unit's components every frame.
/// </summary>
public sealed class DisposalDeviceModel
{
    private const double SameFlushSeconds = 1.0;
    private const double LateFlushSeconds = 1.0;
    private static readonly TimeSpan AccessCheckInterval = TimeSpan.FromSeconds(0.25);

    private readonly IEntityManager _entMan;
    private readonly IGameTiming _timing;
    private readonly IPlayerManager _player;
    private readonly DisposalUnitSystem _disposal;
    private readonly SharedPowerReceiverSystem _power;
    private readonly AccessReaderSystem _access;
    private readonly SharedHandsSystem _hands;

    private TimeSpan _seenPressurized;
    private TimeSpan? _flushStart;
    private TimeSpan? _flushAt;
    private TimeSpan _nextAccessCheck;
    private bool _watching;

    public readonly EntityUid Owner;

    public IReadOnlyList<EntityUid> Contents = Array.Empty<EntityUid>();
    public DisposalsPressureState State = DisposalsPressureState.Ready;
    public EntityUid? Held;
    public string? RouteTag;
    public string? MailTag;
    public string? MailTarget;
    public IReadOnlyList<string> MailTargets = Array.Empty<string>();
    public float Pressure = 1f;
    public float SinceFlush = float.MaxValue;
    public int FlushCount;
    public bool Powered;
    public bool SwitchedOn;
    public bool Engaged;
    public bool RouteAllowed;
    public bool HandBusy;

    public DisposalDeviceModel(EntityUid owner, IEntityManager entMan, IGameTiming timing, IPlayerManager player)
    {
        Owner = owner;
        _entMan = entMan;
        _timing = timing;
        _player = player;
        _disposal = entMan.System<DisposalUnitSystem>();
        _power = entMan.System<SharedPowerReceiverSystem>();
        _access = entMan.System<AccessReaderSystem>();
        _hands = entMan.System<SharedHandsSystem>();
    }

    /// <summary>
    /// Reads the current state of the unit.
    /// </summary>
    public void Refresh()
    {
        if (!_entMan.TryGetComponent(Owner, out DisposalUnitComponent? unit))
            return;

        var now = _timing.CurTime;
        Contents = unit.Container?.ContainedEntities ?? (IReadOnlyList<EntityUid>) Array.Empty<EntityUid>();
        Engaged = unit.Engaged;
        Powered = _power.IsPowered(Owner);
        SharedApcPowerReceiverComponent? receiver = null;
        SwitchedOn = _power.ResolveApc(Owner, ref receiver) && !receiver.PowerDisabled;
        State = _disposal.GetState((Owner, unit));
        Pressure = State == DisposalsPressureState.Ready
            ? 1f
            : Math.Clamp(1f - (float) (unit.NextPressurized - now).TotalSeconds * unit.PressurePerSecond, 0f, 1f);

        ReadFlush(unit, now);
        ReadHand();
        ReadRouting(now);
        ReadMailing();
    }

    /// <summary>
    /// Whether an entity can be taken out by hand.
    /// </summary>
    public bool IsItem(EntityUid uid)
    {
        return _entMan.HasComponent<ItemComponent>(uid);
    }

    /// <summary>
    /// Whether an entity is a creature small enough to be seen dropping in.
    /// </summary>
    public bool IsSmallCreature(EntityUid uid)
    {
        return _disposal.IsSmallCreature(uid);
    }

    /// <summary>
    /// The name of an entity as the game shows it.
    /// </summary>
    public string Name(EntityUid uid)
    {
        return _entMan.GetComponent<MetaDataComponent>(uid).EntityName;
    }

    private void ReadFlush(DisposalUnitComponent unit, TimeSpan now)
    {
        // A flush moves NextPressurized forward. The client predicts it and the server may land a few ticks away,
        // so times this close together are the same flush.
        if (unit.NextPressurized != TimeSpan.Zero && unit.NextPressurized != _seenPressurized)
        {
            _seenPressurized = unit.NextPressurized;
            var start = _seenPressurized > now
                ? _seenPressurized - TimeSpan.FromSeconds(1f / unit.PressurePerSecond)
                : _seenPressurized;

            if (_flushStart is not { } known || Math.Abs((start - known).TotalSeconds) > SameFlushSeconds)
            {
                _flushStart = start;
                // The news of a flush comes late. Its animation is timed from the moment it is seen, so that it plays whole.
                _flushAt = _watching && (now - start).TotalSeconds < LateFlushSeconds ? now : start;
                FlushCount++;
            }
        }

        _watching = true;
        SinceFlush = _flushAt is { } flush ? (float) (now - flush).TotalSeconds : float.MaxValue;
    }

    private void ReadHand()
    {
        Held = null;
        HandBusy = false;
        if (_player.LocalEntity is not { } local || _hands.GetActiveItem(local) is not { } item)
            return;

        if (_entMan.HasComponent<VirtualItemComponent>(item))
            HandBusy = true;
        else
            Held = item;
    }

    private void ReadRouting(TimeSpan now)
    {
        RouteTag = _entMan.TryGetComponent(Owner, out DisposalTaggerComponent? tagger) && tagger.Editable ? tagger.Tag : null;
        if (now < _nextAccessCheck)
            return;

        _nextAccessCheck = now + AccessCheckInterval;
        RouteAllowed = _player.LocalEntity is { } local && HasAccess(local);
    }

    /// <summary>
    /// The access check without the entry the usual one leaves in the reader's log, because nobody has used the reader yet.
    /// </summary>
    private bool HasAccess(EntityUid user)
    {
        if (!_entMan.TryGetComponent(Owner, out AccessReaderComponent? reader))
            return true;

        var sources = _access.FindPotentialAccessItems(user);
        var tags = _access.FindAccessTags(user, sources);
        _access.FindStationRecordKeys(user, out var keys, sources);
        return _access.IsAllowed(tags, keys, Owner, reader);
    }

    private void ReadMailing()
    {
        if (!_entMan.TryGetComponent(Owner, out MailingUnitComponent? mailing))
            return;

        MailTag = mailing.Tag;
        MailTarget = mailing.Target;
        MailTargets = mailing.TargetList;
    }
}
