using System.Diagnostics.CodeAnalysis;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Telephone;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._Polonium.CallablePhone;

public abstract class SharedCallablePhoneSystem : EntitySystem
{
    [Dependency] protected readonly SharedHandsSystem Hands = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        if (_net.IsClient)
        {
            SubscribeLocalEvent<TelephoneHandsetComponent, GotEquippedHandEvent>(OnHandsetEquippedPlayLocal);
            SubscribeLocalEvent<TelephoneHandsetComponent, GotUnequippedHandEvent>(OnHandsetUnequippedPlayLocal);
        }

        SubscribeLocalEvent<TelephoneHandsetComponent, UseInHandEvent>(OnHandsetUseInHand, before: [typeof(ActivatableUISystem)]);
        SubscribeLocalEvent<TelephoneHandsetComponent, ActivatableUIOpenAttemptEvent>(OnHandsetUIOpenAttempt);

        SubscribeLocalEvent<TelephoneHandsetComponent, GetVerbsEvent<ActivationVerb>>(
            OnHandsetGetActivationVerbs,
            after: [typeof(ActivatableUISystem)]);

        SubscribeLocalEvent<TelephoneHandsetComponent, GetVerbsEvent<Verb>>(
            OnHandsetGetVerbs,
            after: [typeof(ActivatableUISystem)]);

        SubscribeLocalEvent<TelephoneHandsetComponent, ContainerGettingInsertedAttemptEvent>(OnHandsetGettingInserted);
    }

    private void OnHandsetGettingInserted(Entity<TelephoneHandsetComponent> handset, ref ContainerGettingInsertedAttemptEvent args)
    {
        if (args.Cancelled || IsAllowedHandsetContainer(handset, args.Container))
            return;

        args.Cancel();
    }

    private bool IsAllowedHandsetContainer(Entity<TelephoneHandsetComponent> handset, BaseContainer container)
    {
        if (IsHandContainer(container))
            return true;

        if (container.ID != CallablePhoneComponent.HandsetSlotId)
            return false;

        if (!HasComp<CallablePhoneComponent>(container.Owner))
            return false;

        if (handset.Comp.ParentPhone == NetEntity.Invalid)
            return true;

        return GetEntity(handset.Comp.ParentPhone) == container.Owner;
    }

    private bool IsHandContainer(BaseContainer container)
    {
        if (!TryComp<HandsComponent>(container.Owner, out var hands))
            return false;

        foreach (var hand in Hands.EnumerateHands(container.Owner, hands))
        {
            if (hand.Container == container)
                return true;
        }

        return false;
    }

    private void OnHandsetUseInHand(Entity<TelephoneHandsetComponent> entity, ref UseInHandEvent args)
    {
        if (CanOpenHandsetDirectory(entity))
            return;

        NotifyHandsetDirectoryBlocked(entity, args.User);
        args.Handled = true;
    }

    private void OnHandsetUIOpenAttempt(Entity<TelephoneHandsetComponent> entity, ref ActivatableUIOpenAttemptEvent args)
    {
        if (CanOpenHandsetDirectory(entity))
            return;

        NotifyHandsetDirectoryBlocked(entity, args.User);
        args.Cancel();
    }

    private void NotifyHandsetDirectoryBlocked(Entity<TelephoneHandsetComponent> entity, EntityUid user)
    {
        var phone = GetEntity(entity.Comp.ParentPhone);
        var message = !Exists(phone) || !HasComp<CallablePhoneComponent>(phone)
            ? Loc.GetString("callable-phone-handset-unlinked")
            : Loc.GetString("callable-phone-handset-line-busy");

        _popup.PopupClient(message, user, user, PopupType.Medium);
    }

    private void OnHandsetEquippedPlayLocal(Entity<TelephoneHandsetComponent> handset, ref GotEquippedHandEvent args)
    {
        var phone = GetEntity(handset.Comp.ParentPhone);
        if (!Exists(phone) || !TryComp<CallablePhoneComponent>(phone, out var callable))
            return;

        if (!TryComp<TelephoneComponent>(phone, out var telephone))
            return;

        var inCall = telephone.CurrentState == TelephoneState.InCall;
        var sound = inCall ? callable.PickupHandsetInCallSound : callable.PickupHandsetSound;
        _audio.PlayLocal(sound, handset.Owner, args.User);
    }

    private void OnHandsetUnequippedPlayLocal(Entity<TelephoneHandsetComponent> handset, ref GotUnequippedHandEvent args)
    {
        var phone = GetEntity(handset.Comp.ParentPhone);
        if (!Exists(phone) || !TryComp<CallablePhoneComponent>(phone, out var callable))
            return;

        if (!TryComp<TelephoneComponent>(phone, out var telephone))
            return;

        if (telephone.CurrentState != TelephoneState.InCall)
            return;

        _audio.PlayLocal(callable.HangupHandsetInCallSound, handset.Owner, args.User);
    }

    /// <summary>
    /// Whether the handset phone directory UI can be opened (idle line only).
    /// </summary>
    public bool CanOpenHandsetDirectory(Entity<TelephoneHandsetComponent> handset)
    {
        var phone = GetEntity(handset.Comp.ParentPhone);
        if (!Exists(phone) || !HasComp<CallablePhoneComponent>(phone))
            return false;

        if (TryComp<TelephoneComponent>(phone, out var telephone) && telephone.CurrentState != TelephoneState.Idle)
            return false;

        return true;
    }

    private void OnHandsetGetActivationVerbs(Entity<TelephoneHandsetComponent> handset, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (CanOpenHandsetDirectory(handset))
            return;

        RemoveCallablePhoneUiVerbs<ActivationVerb>(handset, args.Verbs);
    }

    private void OnHandsetGetVerbs(Entity<TelephoneHandsetComponent> handset, ref GetVerbsEvent<Verb> args)
    {
        if (CanOpenHandsetDirectory(handset))
            return;

        RemoveCallablePhoneUiVerbs<Verb>(handset, args.Verbs);
    }

    private void RemoveCallablePhoneUiVerbs<T>(Entity<TelephoneHandsetComponent> handset, SortedSet<T> verbs) where T : Verb
    {
        if (!TryComp<ActivatableUIComponent>(handset, out var ui) || !Equals(ui.Key, CallablePhoneUiKey.Key))
            return;

        var verbText = Loc.GetString(ui.VerbText);
        verbs.RemoveWhere(v => v.Text == verbText);
    }

    /// <summary>
    /// Whether <paramref name="source"/> may place a call to <paramref name="receiver"/>.
    /// </summary>
    public bool CanSourceDialReceiver(CallablePhoneComponent source, CallablePhoneComponent receiver)
    {
        if (source.IsCentComm)
            return true;

        if (receiver.ListedInDirectory)
        {
            if (source.ExcludeCentCommFromDial && receiver.IsCentComm)
                return false;

            return true;
        }

        if (receiver.IsCentComm && source.IncludeCentCommInDirectory)
            return true;

        return false;
    }

    /// <summary>
    /// Whether <paramref name="receiver"/> should appear in <paramref name="source"/>'s handset directory.
    /// </summary>
    public bool CanSourceSeeInDirectory(CallablePhoneComponent source, CallablePhoneComponent receiver)
    {
        if (source.IsCentComm)
            return true;

        return CanSourceDialReceiver(source, receiver);
    }

    /// <summary>
    /// Name shown in the callable phone directory.
    /// </summary>
    public string GetPhoneDisplayName(EntityUid uid)
    {
        if (TryComp<CallablePhoneComponent>(uid, out var callable) && !string.IsNullOrWhiteSpace(callable.PhoneName))
            return callable.PhoneName;

        return MetaData(uid).EntityName;
    }

    public bool UserHoldingPhoneHandset(EntityUid phone, EntityUid user)
    {
        foreach (var held in Hands.EnumerateHeld(user))
        {
            if (!TryComp<TelephoneHandsetComponent>(held, out var handset))
                continue;

            if (GetEntity(handset.ParentPhone) == phone)
                return true;
        }

        return false;
    }

    public bool IsHandsetInCradle(EntityUid phone)
    {
        return _itemSlots.GetItemOrNull(phone, CallablePhoneComponent.HandsetSlotId) != null;
    }

    public void UpdatePhoneVisual(EntityUid phone, AppearanceComponent? appearance = null)
    {
        if (!Resolve(phone, ref appearance))
            return;

        var state = IsHandsetInCradle(phone)
            ? CallablePhoneVisuals.OnHook
            : CallablePhoneVisuals.OffHook;

        _appearance.SetData(phone, CallablePhoneVisuals.HookState, state, appearance);
    }

    /// <summary>
    /// The handset is off the cradle; the line should stay open while someone walks around with it.
    /// </summary>
    public bool IsHandsetOffHook(EntityUid phone)
    {
        if (!TryComp<CallablePhoneComponent>(phone, out var callable))
            return false;

        if (callable.HandsetHolder != null)
            return true;

        return !IsHandsetInCradle(phone);
    }

    /// <summary>
    /// The handset entity held by <paramref name="holder"/> for <paramref name="phone"/>, if any.
    /// </summary>
    public EntityUid? GetHandsetHeldBy(EntityUid phone, EntityUid holder)
    {
        foreach (var held in Hands.EnumerateHeld(holder))
        {
            if (!TryComp<TelephoneHandsetComponent>(held, out var handset))
                continue;

            if (GetEntity(handset.ParentPhone) == phone)
                return held;
        }

        return null;
    }

    /// <summary>
    /// The handset entity currently off the cradle for this phone, if any.
    /// </summary>
    public EntityUid? GetOffHookHandset(EntityUid phone, EntityUid? holder = null)
    {
        if (holder != null)
        {
            var held = GetHandsetHeldBy(phone, holder.Value);
            if (held != null)
                return held;
        }

        if (IsHandsetInCradle(phone))
            return null;

        var query = EntityQueryEnumerator<TelephoneHandsetComponent>();
        while (query.MoveNext(out var uid, out var handset))
        {
            if (GetEntity(handset.ParentPhone) == phone)
                return uid;
        }

        return null;
    }

    /// <summary>
    /// Whether mob is within cord range of phone
    /// </summary>
    public bool IsWithinHandsetCordRange(EntityUid phone, EntityUid mob, CallablePhoneComponent callable)
    {
        var phoneCoords = Transform(phone).Coordinates;
        var mobCoords = Transform(mob).Coordinates;

        if (!phoneCoords.TryDistance(EntityManager, mobCoords, out var distance))
            return false;

        return distance <= callable.HandsetCordRange;
    }

    public bool IsCallablePhoneContactValid(NetEntity netEntity)
    {
        return TryGetEntity(netEntity, out var uid)
               && Exists(uid)
               && HasComp<CallablePhoneComponent>(uid)
               && HasComp<TelephoneComponent>(uid);
    }

    /// <summary>
    /// Verify that the receivrs are the valid phones and resolve them
    /// </summary>
    public bool TryResolveCallablePhoneReceiver(
        NetEntity receiverNet,
        CallablePhoneComponent source,
        [NotNullWhen(true)] out EntityUid? receiverUid,
        [NotNullWhen(true)] out CallablePhoneComponent? receiverCallable,
        [NotNullWhen(true)] out TelephoneComponent? receiverTelephone)
    {
        receiverUid = null;
        receiverCallable = null;
        receiverTelephone = null;

        if (!TryGetEntity(receiverNet, out var uid) || !Exists(uid))
            return false;

        if (!TryComp<CallablePhoneComponent>(uid, out var callable))
            return false;

        if (!TryComp<TelephoneComponent>(uid, out var telephone))
            return false;

        if (!CanSourceDialReceiver(source, callable))
            return false;

        receiverUid = uid;
        receiverCallable = callable;
        receiverTelephone = telephone;
        return true;
    }

    public virtual bool ShouldUseAnonymousAdminCallerName(EntityUid phone, CallablePhoneComponent callable)
    {
        return callable.IsCentComm;
    }
}
