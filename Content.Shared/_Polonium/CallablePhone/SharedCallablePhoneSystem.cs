// SPDX-License-Identifier: MIT

using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Labels.Components;
using Content.Shared.Telephone;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;

namespace Content.Shared._Polonium.CallablePhone;

public abstract class SharedCallablePhoneSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TelephoneHandsetComponent, GetVerbsEvent<ActivationVerb>>(
            OnHandsetGetActivationVerbs,
            after: [typeof(ActivatableUISystem)]);

        SubscribeLocalEvent<TelephoneHandsetComponent, GetVerbsEvent<Verb>>(
            OnHandsetGetVerbs,
            after: [typeof(ActivatableUISystem)]);
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
    /// Name shown in the callable phone directory.
    /// </summary>
    public string GetPhoneDisplayName(EntityUid uid)
    {
        if (TryComp<CallablePhoneComponent>(uid, out var callable) && !string.IsNullOrWhiteSpace(callable.PhoneName))
            return callable.PhoneName;

        if (TryComp<LabelComponent>(uid, out var label) && !string.IsNullOrEmpty(label.CurrentLabel))
            return label.CurrentLabel;

        return MetaData(uid).EntityName;
    }

    public bool UserHoldingPhoneHandset(EntityUid phone, EntityUid user)
    {
        foreach (var held in _hands.EnumerateHeld(user))
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
        foreach (var held in _hands.EnumerateHeld(holder))
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
}
