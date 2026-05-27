// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Server.Speech;
using Content.Server.Speech.Components;
using Content.Server.Telephone;
using Content.Shared._Polonium.CallablePhone;
using Content.Shared.Chat;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.Hands;
using Content.Shared._Polonium.Prayer;
using Content.Shared.Speech;
using Content.Shared.Telephone;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.CallablePhone;

public sealed class CallablePhoneSystem : SharedCallablePhoneSystem
{
    [Dependency] private readonly TelephoneSystem _telephone = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SpeechSoundSystem _speechSound = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;

    private readonly HashSet<EntityUid> _centCommAwaitingPickup = new();
    private readonly HashSet<EntityUid> _centCommActiveCalls = new();
    private readonly Dictionary<EntityUid, NetUserId> _centCommAnsweringAdmin = new();

    /// <summary>
    /// Admins with an open chat window for a CentComm phone line.
    /// </summary>
    private readonly Dictionary<NetEntity, HashSet<ICommonSession>> _openDeviceChats = new();

    private float _updateTimer = 1f;
    private const float UpdateTime = 1f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CallablePhoneComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CallablePhoneComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CallablePhoneComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<CallablePhoneComponent, EntRemovedFromContainerMessage>(OnRemoved);

        SubscribeLocalEvent<TelephoneHandsetComponent, ActivatableUIOpenAttemptEvent>(OnHandsetUIOpenAttempt);
        SubscribeLocalEvent<TelephoneHandsetComponent, BeforeActivatableUIOpenEvent>(OnHandsetBeforeUIOpen);
        SubscribeLocalEvent<TelephoneHandsetComponent, CallablePhoneCallMessage>(OnHandsetCall);
        SubscribeLocalEvent<TelephoneHandsetComponent, CallablePhoneAnswerMessage>(OnHandsetAnswer);
        SubscribeLocalEvent<TelephoneHandsetComponent, CallablePhoneHangUpMessage>(OnHandsetHangUp);
        SubscribeLocalEvent<TelephoneHandsetComponent, GotEquippedHandEvent>(OnHandsetEquipped);
        SubscribeLocalEvent<TelephoneHandsetComponent, GotUnequippedHandEvent>(OnHandsetUnequipped);
        SubscribeLocalEvent<TelephoneHandsetComponent, ListenAttemptEvent>(OnHandsetListenAttempt);
        SubscribeLocalEvent<TelephoneHandsetComponent, ListenEvent>(OnHandsetListen);

        SubscribeLocalEvent<CallablePhoneComponent, TelephoneStateChangeEvent>(OnTelephoneStateChange);
        SubscribeLocalEvent<CallablePhoneComponent, TelephoneCallCommencedEvent>(OnCallCommenced);
        SubscribeLocalEvent<CallablePhoneComponent, TelephoneCallEndedEvent>(OnCallEnded);

        SubscribeLocalEvent<EntitySpokeEvent>(OnHandsetHolderSpoke);
        SubscribeLocalEvent<CallablePhoneComponent, TelephoneMessageReceivedEvent>(OnCentCommTelephoneMessageReceived);

        SubscribeNetworkEvent<CentCommCallPickupResponseEvent>(OnCentCommPickupResponse);
        SubscribeNetworkEvent<PrayableChatSendMessageEvent>(OnCentCommChatSendMessage);
        SubscribeNetworkEvent<PrayableChatCloseEvent>(OnCentCommChatClose);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateTimer += frameTime;
        if (_updateTimer < UpdateTime)
            return;

        _updateTimer -= UpdateTime;

        var uiQuery = AllEntityQuery<CallablePhoneComponent, TelephoneComponent>();
        while (uiQuery.MoveNext(out var uid, out _, out var telephone))
        {
            UpdateUiState((uid, telephone));
        }
    }

    private void OnMapInit(Entity<CallablePhoneComponent> entity, ref MapInitEvent args)
    {
        if (!string.IsNullOrEmpty(entity.Comp.PhoneName))
        {
            entity.Comp.PhoneName = Loc.GetString(entity.Comp.PhoneName);
            Dirty(entity);
        }

        LinkHandsetInSlot(entity);
        UpdatePhoneVisual(entity);
    }

    private void OnShutdown(Entity<CallablePhoneComponent> entity, ref ComponentShutdown args)
    {
        StopCallWaitingLoop(entity);

        if (TryComp<TelephoneComponent>(entity, out var telephone) && _telephone.IsTelephoneEngaged((entity, telephone)))
            _telephone.EndTelephoneCalls((entity, telephone));
    }

    private void OnInserted(Entity<CallablePhoneComponent> entity, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != CallablePhoneComponent.HandsetSlotId)
            return;

        entity.Comp.HandsetHolder = null;
        Dirty(entity);

        if (TryComp<TelephoneHandsetComponent>(args.Entity, out var handset))
        {
            handset.ParentPhone = GetNetEntity(entity);
            Dirty(args.Entity, handset);
        }

        _ui.CloseUi(args.Entity, CallablePhoneUiKey.Key);

        if (!TryComp<TelephoneComponent>(entity, out var telephone))
            return;

        var micHangup = telephone.CurrentState is TelephoneState.InCall or TelephoneState.EndingCall;
        PlayHandsetHangup(entity, micHangup);

        _telephone.SetSpeakerForTelephone((entity, telephone), null);

        if (_telephone.IsTelephoneEngaged((entity, telephone)))
            _telephone.EndTelephoneCalls((entity, telephone));

        UpdatePhoneVisual(entity);
    }

    private void OnRemoved(Entity<CallablePhoneComponent> entity, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != CallablePhoneComponent.HandsetSlotId)
            return;

        // HandsetHolder and answer are set in OnHandsetEquipped (after pickup completes).
        entity.Comp.HandsetHolder = null;
        Dirty(entity);

        UpdatePhoneVisual(entity);
    }

    private void OnHandsetEquipped(Entity<TelephoneHandsetComponent> handset, ref GotEquippedHandEvent args)
    {
        var phone = GetEntity(handset.Comp.ParentPhone);
        if (!Exists(phone) || !TryComp<CallablePhoneComponent>(phone, out var callable))
            return;

        if (!TryComp<TelephoneComponent>(phone, out var telephone))
            return;

        callable.HandsetHolder = args.User;
        Dirty(phone, callable);

        var inCall = telephone.CurrentState == TelephoneState.InCall;

        if (telephone.CurrentState == TelephoneState.Ringing)
        {
            if (!callable.IsCentComm)
                _telephone.AnswerTelephone((phone, telephone), args.User);
        }

        PlayHandsetPickup((phone, callable), inCall);

        UpdateHandsetRelay(phone, telephone, args.User);

        UpdateUiState((phone, telephone));

        if (telephone.CurrentState == TelephoneState.Idle)
            _ui.TryOpenUi(handset.Owner, CallablePhoneUiKey.Key, args.User);
    }

    private void OnHandsetUnequipped(Entity<TelephoneHandsetComponent> handset, ref GotUnequippedHandEvent args)
    {
        _ui.CloseUi(handset.Owner, CallablePhoneUiKey.Key);

        var phone = GetEntity(handset.Comp.ParentPhone);
        if (!TryComp<CallablePhoneComponent>(phone, out var callable))
            return;

        if (callable.HandsetHolder == args.User)
        {
            if (TryComp<TelephoneComponent>(phone, out var telephone))
            {
                if (telephone.CurrentState == TelephoneState.InCall)
                    PlayHandsetHangup((phone, callable), micVariant: true);

                UpdateHandsetRelay(phone, telephone, null);
            }

            callable.HandsetHolder = null;
            Dirty(phone, callable);
        }
    }

    private void OnTelephoneStateChange(Entity<CallablePhoneComponent> entity, ref TelephoneStateChangeEvent args)
    {
        if (args.OldState == TelephoneState.Calling)
            StopCallWaitingLoop(entity);

        if (args.NewState != TelephoneState.Idle)
            CloseHandsetUis(entity);
    }

    private void OnCallCommenced(Entity<CallablePhoneComponent> entity, ref TelephoneCallCommencedEvent args)
    {
        StopCallWaitingLoop(entity);

        if (!TryComp<TelephoneComponent>(entity, out var telephone))
            return;

        UpdateHandsetRelay(entity, telephone, entity.Comp.HandsetHolder);
    }

    private void OnCallEnded(Entity<CallablePhoneComponent> entity, ref TelephoneCallEndedEvent args)
    {
        StopCallWaitingLoop(entity);
        ClearHandsetMicrophones(entity);

        if (!entity.Comp.IsCentComm)
            return;

        _centCommAwaitingPickup.Remove(entity);
        _centCommAnsweringAdmin.Remove(entity);

        if (_centCommActiveCalls.Remove(entity))
        {
            NotifyDeviceChatLog(entity, Loc.GetString("callable-phone-centcomm-call-ended"));
            SetDeviceChatInputEnabled(entity, false);
        }
    }

    private void OnHandsetListenAttempt(Entity<TelephoneHandsetComponent> handset, ref ListenAttemptEvent args)
    {
        var phone = GetEntity(handset.Comp.ParentPhone);
        if (!TryComp<TelephoneComponent>(phone, out var telephone))
        {
            args.Cancel();
            return;
        }

        _telephone.ProcessListenAttempt((phone, telephone), ref args, checkProximityToPhone: false);
    }

    private void OnHandsetListen(Entity<TelephoneHandsetComponent> handset, ref ListenEvent args)
    {
        var phone = GetEntity(handset.Comp.ParentPhone);
        if (!TryComp<TelephoneComponent>(phone, out var telephone))
            return;

        _telephone.ProcessListen((phone, telephone), ref args);
    }

    private void OnHandsetUIOpenAttempt(Entity<TelephoneHandsetComponent> entity, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!CanOpenHandsetDirectory(entity))
            args.Cancel();
    }

    private void OnHandsetBeforeUIOpen(Entity<TelephoneHandsetComponent> entity, ref BeforeActivatableUIOpenEvent args)
    {
        var phone = GetEntity(entity.Comp.ParentPhone);
        if (TryComp<TelephoneComponent>(phone, out var telephone))
            UpdateUiState((phone, telephone));
    }

    private void OnHandsetCall(Entity<TelephoneHandsetComponent> entity, ref CallablePhoneCallMessage args)
    {
        var phone = GetEntity(entity.Comp.ParentPhone);
        if (!Exists(phone) || !TryComp<CallablePhoneComponent>(phone, out var callable))
            return;

        OnCall((phone, callable), ref args);
    }

    private void OnHandsetAnswer(Entity<TelephoneHandsetComponent> entity, ref CallablePhoneAnswerMessage args)
    {
        var phone = GetEntity(entity.Comp.ParentPhone);
        if (!Exists(phone) || !TryComp<CallablePhoneComponent>(phone, out var callable))
            return;

        OnAnswer((phone, callable), ref args);
    }

    private void OnHandsetHangUp(Entity<TelephoneHandsetComponent> entity, ref CallablePhoneHangUpMessage args)
    {
        var phone = GetEntity(entity.Comp.ParentPhone);
        if (!Exists(phone) || !TryComp<CallablePhoneComponent>(phone, out var callable))
            return;

        OnHangUp((phone, callable), ref args);
    }

    private void OnCall(Entity<CallablePhoneComponent> source, ref CallablePhoneCallMessage args)
    {
        if (!UserHoldingPhoneHandset(source, args.Actor))
            return;

        if (!TryComp<TelephoneComponent>(source, out var sourceTelephone))
            return;

        var receiverUid = GetEntity(args.Receiver);

        if (!TryComp<CallablePhoneComponent>(receiverUid, out var receiverCallable) ||
            !receiverCallable.ListedInDirectory ||
            !TryComp<TelephoneComponent>(receiverUid, out var receiverTelephone))
        {
            return;
        }

        var sourceEnt = (source.Owner, sourceTelephone);
        var receiverEnt = (receiverUid, receiverTelephone);

        if (!_telephone.IsSourceAbleToReachReceiver(sourceEnt, receiverEnt))
        {
            _popup.PopupEntity(Loc.GetString("callable-phone-call-unreachable"), source, args.Actor);
            return;
        }

        if (_telephone.IsTelephoneEngaged(receiverEnt))
        {
            PlayPhoneSound(source, source.Comp.BusyTone);
            _popup.PopupEntity(Loc.GetString("callable-phone-call-busy"), source, args.Actor);
            return;
        }

        _telephone.CallTelephone(sourceEnt, receiverEnt, args.Actor);

        if (!_telephone.IsTelephoneEngaged(sourceEnt))
        {
            _popup.PopupEntity(Loc.GetString("callable-phone-call-failed"), source, args.Actor);
            return;
        }

        BeginOutboundCallAudio(source);

        if (receiverCallable.IsCentComm)
            BeginCentCommCall(receiverUid, receiverTelephone);
    }

    private void BeginCentCommCall(EntityUid phone, TelephoneComponent telephone)
    {
        _centCommAwaitingPickup.Add(phone);

        var callerName = _telephone.GetPlainCallerIdForEntity(
            telephone.LastCallerId.Item1,
            telephone.LastCallerId.Item2);

        SendCentCommRingNotification(phone, callerName);
        PromptAdminGhostsForCentCommCall(phone, callerName);
    }

    private void SendCentCommRingNotification(EntityUid phone, string callerName)
    {
        if (!TryComp<CallablePhoneComponent>(phone, out var callable) || !callable.IsCentComm)
            return;

        _chatManager.SendAdminAnnouncement(
            $"{Loc.GetString(callable.AdminChatPrefix)} <{callerName}>: {Loc.GetString("callable-phone-centcomm-call-ringing")}");

        _audio.PlayGlobal("/Audio/Items/ring.ogg",
            Filter.Empty().AddPlayers(_adminManager.ActiveAdmins), false, AudioParams.Default.WithVolume(-8f));
    }

    private void OpenDeviceChatForAdmin(ICommonSession admin, EntityUid uid, bool inputEnabled = true)
    {
        if (!TryComp<CallablePhoneComponent>(uid, out var callable) || !callable.IsCentComm)
            return;

        var netEntity = GetNetEntity(uid);
        var openEvent = new OpenPrayableChatEvent(netEntity, admin.Name, string.Empty, inputEnabled);

        RegisterDeviceChat(admin, netEntity);
        RaiseNetworkEvent(openEvent, admin);
    }

    private void NotifyDeviceChatLog(EntityUid uid, string message)
    {
        NotifyDeviceChatListeners(uid, string.Empty, message, incoming: false, isLog: true);
    }

    private void SetDeviceChatInputEnabled(EntityUid uid, bool enabled)
    {
        var netEntity = GetNetEntity(uid);

        if (!_openDeviceChats.ContainsKey(netEntity))
            return;

        var ev = new PrayableChatSetInputEnabledEvent(netEntity, enabled);

        foreach (var session in _openDeviceChats[netEntity].ToArray())
        {
            RaiseNetworkEvent(ev, session);
        }
    }

    private bool IsAdminInDeviceChat(ICommonSession admin, EntityUid uid)
    {
        return _openDeviceChats.TryGetValue(GetNetEntity(uid), out var sessions) && sessions.Contains(admin);
    }

    private void NotifyDeviceChatListeners(EntityUid uid, string sender, string message, bool incoming, bool isLog = false)
    {
        var netEntity = GetNetEntity(uid);

        if (!_openDeviceChats.TryGetValue(netEntity, out var sessions))
            return;

        var chatMessage = new PrayableChatTextMessageEvent(netEntity, sender, message, incoming, isLog);

        foreach (var session in sessions.ToArray())
        {
            RaiseNetworkEvent(chatMessage, session);
        }
    }

    private void RegisterDeviceChat(ICommonSession session, NetEntity entity)
    {
        if (!_openDeviceChats.TryGetValue(entity, out var sessions))
        {
            sessions = new HashSet<ICommonSession>();
            _openDeviceChats[entity] = sessions;
        }

        sessions.Add(session);
    }

    private void UnregisterDeviceChat(ICommonSession session, NetEntity entity)
    {
        if (!_openDeviceChats.TryGetValue(entity, out var sessions))
            return;

        sessions.Remove(session);

        if (sessions.Count == 0)
            _openDeviceChats.Remove(entity);
    }

    private bool IsCentCommCallActive(EntityUid uid)
    {
        return TryComp<TelephoneComponent>(uid, out var telephone)
            && telephone.CurrentState == TelephoneState.InCall;
    }

    private void OnCentCommChatSendMessage(PrayableChatSendMessageEvent msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.IsAdmin(args.SenderSession))
            return;

        if (!TryGetEntity(msg.Entity, out var uid) ||
            !TryComp<CallablePhoneComponent>(uid, out var callable) ||
            !callable.IsCentComm)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(msg.Message))
            return;

        if (!IsCentCommCallActive(uid.Value))
            return;

        ReplyThroughCentCommPhone(args.SenderSession, uid.Value, callable, msg.Message.Trim());
        NotifyDeviceChatListeners(uid.Value, args.SenderSession.Name, msg.Message.Trim(), incoming: false);
    }

    private void ReplyThroughCentCommPhone(ICommonSession admin, EntityUid uid, CallablePhoneComponent callable, string message)
    {
        var name = Loc.GetString(callable.AdminChatPrefix);

        if (TryComp<TelephoneComponent>(uid, out var telephone) && _telephone.IsTelephoneEngaged((uid, telephone)))
            _telephone.RelayTelephoneMessage(uid, message, (uid, telephone), skipCentCommReceivers: true);

        _audio.PlayPvs("/Audio/Items/ring.ogg", uid, AudioParams.Default.WithVolume(-8f));

        _adminLogger.Add(
            LogType.AdminMessage,
            LogImpact.Low,
            $"{admin.Name} spoke through {ToPrettyString(uid)} as {name}: {message}");
    }

    private void OnCentCommChatClose(PrayableChatCloseEvent msg, EntitySessionEventArgs args)
    {
        UnregisterDeviceChat(args.SenderSession, msg.Entity);
        OnAdminDeviceChatClosed(msg, args);
    }

    private void PromptAdminGhostsForCentCommCall(EntityUid phone, string callerName)
    {
        var netPhone = GetNetEntity(phone);
        var prompt = new CentCommCallPickupPromptEvent(netPhone, callerName);

        foreach (var session in _adminManager.AllAdmins)
        {
            if (session.AttachedEntity == null || !HasComp<GhostComponent>(session.AttachedEntity))
                continue;

            RaiseNetworkEvent(prompt, session);
        }
    }

    private void OnAdminDeviceChatClosed(PrayableChatCloseEvent msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.IsAdmin(args.SenderSession, includeDeAdmin: true))
            return;

        if (!TryGetEntity(msg.Entity, out var phone) ||
            !_centCommAnsweringAdmin.TryGetValue(phone.Value, out var answeringAdmin) ||
            answeringAdmin != args.SenderSession.UserId ||
            !_centCommActiveCalls.Contains(phone.Value))
        {
            return;
        }

        if (!TryComp<CallablePhoneComponent>(phone, out var callable) ||
            !callable.IsCentComm ||
            !TryComp<TelephoneComponent>(phone, out var telephone))
        {
            return;
        }

        PlayRemoteDisconnectOnCallers(telephone);
        _telephone.EndTelephoneCalls((phone.Value, telephone));
    }

    private void OnCentCommPickupResponse(CentCommCallPickupResponseEvent msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.IsAdmin(args.SenderSession, includeDeAdmin: true))
            return;

        if (args.SenderSession.AttachedEntity == null || !HasComp<GhostComponent>(args.SenderSession.AttachedEntity))
            return;

        if (!TryGetEntity(msg.Phone, out var phone) ||
            !TryComp<CallablePhoneComponent>(phone, out var callable) ||
            !callable.IsCentComm)
        {
            return;
        }

        if (!msg.Accepted)
        {
            DeclineCentCommCall(phone.Value);
            return;
        }

        AcceptCentCommCall(args.SenderSession, phone.Value);
    }

    private void DeclineCentCommCall(EntityUid phone)
    {
        if (!TryComp<TelephoneComponent>(phone, out var telephone))
            return;

        if (telephone.CurrentState != TelephoneState.Ringing && !_centCommAwaitingPickup.Contains(phone))
            return;

        PlayRemoteBusyOnCallers(telephone);
        _centCommAwaitingPickup.Remove(phone);
        _telephone.EndTelephoneCalls((phone, telephone));
    }

    private void AcceptCentCommCall(ICommonSession admin, EntityUid phone)
    {
        if (!TryComp<TelephoneComponent>(phone, out var telephone))
            return;

        if (IsAdminInDeviceChat(admin, phone))
            return;

        var isRinging = telephone.CurrentState == TelephoneState.Ringing;
        var isActive = telephone.CurrentState == TelephoneState.InCall || _centCommActiveCalls.Contains(phone);

        if (!isRinging && !isActive)
            return;

        if (isRinging)
        {
            if (admin.AttachedEntity == null)
                return;

            if (telephone.CurrentState == TelephoneState.Ringing)
            {
                _telephone.AnswerTelephone((phone, telephone), admin.AttachedEntity.Value);
                PlayRemoteDisconnectOnCallers(telephone);
                _centCommAwaitingPickup.Remove(phone);
                _centCommActiveCalls.Add(phone);
                _centCommAnsweringAdmin[phone] = admin.UserId;

                OpenDeviceChatForAdmin(admin, phone);
                NotifyDeviceChatLog(phone, Loc.GetString("callable-phone-centcomm-call-started"));
            }
            else if (telephone.CurrentState == TelephoneState.InCall)
            {
                _centCommActiveCalls.Add(phone);
                OpenDeviceChatForAdmin(admin, phone);
                NotifyDeviceChatLog(
                    phone,
                    Loc.GetString("callable-phone-centcomm-admin-joined", ("admin", admin.Name)));
            }

            return;
        }

        _centCommActiveCalls.Add(phone);
        OpenDeviceChatForAdmin(admin, phone);
        NotifyDeviceChatLog(
            phone,
            Loc.GetString("callable-phone-centcomm-admin-joined", ("admin", admin.Name)));
    }

    private void OnAnswer(Entity<CallablePhoneComponent> entity, ref CallablePhoneAnswerMessage args)
    {
        if (!UserHoldingPhoneHandset(entity, args.Actor))
            return;

        if (!TryComp<TelephoneComponent>(entity, out var telephone))
            return;

        _telephone.AnswerTelephone((entity, telephone), args.Actor);
    }

    private void OnHangUp(Entity<CallablePhoneComponent> entity, ref CallablePhoneHangUpMessage args)
    {
        if (!UserHoldingPhoneHandset(entity, args.Actor))
            return;

        if (!TryComp<TelephoneComponent>(entity, out var telephone))
            return;

        _telephone.EndTelephoneCalls((entity, telephone));
    }

    private void LinkHandsetInSlot(Entity<CallablePhoneComponent> entity)
    {
        var handset = _itemSlots.GetItemOrNull(entity, CallablePhoneComponent.HandsetSlotId);
        if (handset == null)
            return;

        if (!TryComp<TelephoneHandsetComponent>(handset, out var comp))
            return;

        comp.ParentPhone = GetNetEntity(entity);
        Dirty(handset.Value, comp);
    }

    private void UpdateHandsetRelay(EntityUid phone, TelephoneComponent telephone, EntityUid? holder)
    {
        UpdateSpeaker(phone, telephone, holder);
        UpdateMicrophone(phone, telephone, holder);
    }

    private void UpdateSpeaker(EntityUid phone, TelephoneComponent telephone, EntityUid? holder)
    {
        if (telephone.CurrentState != TelephoneState.InCall)
        {
            _telephone.SetSpeakerForTelephone((phone, telephone), null);
            return;
        }

        var speechEntity = GetOffHookHandset(phone, holder) ?? phone;

        if (TryComp<SpeechComponent>(speechEntity, out var speech))
            _telephone.SetSpeakerForTelephone((phone, telephone), (speechEntity, speech));
        else
            _telephone.SetSpeakerForTelephone((phone, telephone), null);
    }

    private void UpdateMicrophone(EntityUid phone, TelephoneComponent telephone, EntityUid? holder)
    {
        ClearHandsetMicrophones(phone);

        if (telephone.CurrentState != TelephoneState.InCall)
            return;

        var handset = GetOffHookHandset(phone, holder);
        if (handset == null)
            return;

        _telephone.SetListenerState(handset.Value, true, telephone.ListeningRange);
    }

    private void ClearHandsetMicrophones(EntityUid phone)
    {
        var query = EntityQueryEnumerator<TelephoneHandsetComponent>();
        while (query.MoveNext(out var uid, out var handset))
        {
            if (GetEntity(handset.ParentPhone) != phone)
                continue;

            _telephone.SetListenerState(uid, false, 0);
        }
    }

    private void OnCentCommTelephoneMessageReceived(Entity<CallablePhoneComponent> entity, ref TelephoneMessageReceivedEvent args)
    {
        if (!entity.Comp.IsCentComm)
            return;

        var nameEv = new TransformSpeakerNameEvent(args.MessageSource, Name(args.MessageSource));
        RaiseLocalEvent(args.MessageSource, nameEv);

        NotifyDeviceChatListeners(entity, nameEv.VoiceName, args.Message, incoming: true);
    }

    private void OnHandsetHolderSpoke(EntitySpokeEvent args)
    {
        if (!TryGetHandsetForActiveCall(args.Source, out var handset))
            return;

        if (!TryComp<SpeechComponent>(args.Source, out var speech))
            return;

        // Block the holder's speech sound; play from the handset instead.
        speech.LastTimeSoundPlayed = _timing.CurTime;

        var sound = _speechSound.GetSpeechSound(handset, args.Message);
        if (sound == null)
            return;

        handset.Comp.LastTimeSoundPlayed = _timing.CurTime;
        _audio.PlayPvs(sound, handset);
    }

    private bool TryGetHandsetForActiveCall(EntityUid holder, out Entity<SpeechComponent> handset)
    {
        handset = default;

        var query = EntityQueryEnumerator<CallablePhoneComponent, TelephoneComponent>();
        while (query.MoveNext(out var phone, out var callable, out var telephone))
        {
            if (callable.HandsetHolder != holder || telephone.CurrentState != TelephoneState.InCall)
                continue;

            var handsetEnt = GetHandsetHeldBy(phone, holder);
            if (handsetEnt == null || !TryComp<SpeechComponent>(handsetEnt.Value, out var speech))
                continue;

            handset = (handsetEnt.Value, speech);
            return true;
        }

        return false;
    }

    private void PlayPhoneSound(EntityUid phone, SoundSpecifier? sound)
    {
        if (sound == null)
            return;

        _audio.PlayPvs(sound, phone);
    }

    private void BeginOutboundCallAudio(Entity<CallablePhoneComponent> entity)
    {
        StopCallWaitingLoop(entity);

        if (!TryComp<TelephoneComponent>(entity, out var telephone) || telephone.CurrentState != TelephoneState.Calling)
            return;

        if (entity.Comp.DialSound == null)
        {
            StartCallWaitingLoop(entity);
            return;
        }

        PlayPhoneSound(entity, entity.Comp.DialSound);
        var generation = entity.Comp.CallWaitingDelayGeneration;
        var resolvedDial = _audio.ResolveSound(entity.Comp.DialSound);
        if (ResolvedSoundSpecifier.IsNullOrEmpty(resolvedDial))
        {
            StartCallWaitingLoop(entity);
            return;
        }

        var delay = _audio.GetAudioLength(resolvedDial);

        Timer.Spawn(delay, () =>
        {
            if (!Exists(entity) || entity.Comp.CallWaitingDelayGeneration != generation)
                return;

            if (!TryComp<TelephoneComponent>(entity, out var tel) || tel.CurrentState != TelephoneState.Calling)
                return;

            StartCallWaitingLoop(entity);
        });
    }

    private void StartCallWaitingLoop(Entity<CallablePhoneComponent> entity)
    {
        if (entity.Comp.CallWaitingTone == null || entity.Comp.CallWaitingStream != null)
            return;

        entity.Comp.CallWaitingStream = _audio.PlayPvs(
            entity.Comp.CallWaitingTone,
            entity,
            AudioParams.Default.WithLoop(true))?.Entity;
    }

    private void StopCallWaitingLoop(Entity<CallablePhoneComponent> entity)
    {
        entity.Comp.CallWaitingDelayGeneration++;
        entity.Comp.CallWaitingStream = _audio.Stop(entity.Comp.CallWaitingStream);
    }

    private void PlayHandsetPickup(Entity<CallablePhoneComponent> phone, bool inCall)
    {
        PlayPhoneSound(phone, inCall ? phone.Comp.PickupHandsetInCallSound : phone.Comp.PickupHandsetSound);
    }

    private void PlayHandsetHangup(Entity<CallablePhoneComponent> phone, bool micVariant)
    {
        PlayPhoneSound(phone, micVariant ? phone.Comp.HangupHandsetInCallSound : phone.Comp.HangupHandsetSound);
    }

    private void PlayRemoteDisconnectOnCallers(TelephoneComponent centCommTelephone)
    {
        foreach (var linked in centCommTelephone.LinkedTelephones)
        {
            if (!TryComp<CallablePhoneComponent>(linked, out var callerCallable))
                continue;

            if (linked.Comp.CurrentState is not TelephoneState.InCall and not TelephoneState.EndingCall)
                continue;

            PlayHandsetHangup((linked, callerCallable), micVariant: true);
        }
    }

    private void PlayRemoteBusyOnCallers(TelephoneComponent centCommTelephone)
    {
        foreach (var linked in centCommTelephone.LinkedTelephones)
        {
            if (!TryComp<CallablePhoneComponent>(linked, out var callerCallable))
                continue;

            PlayPhoneSound(linked, callerCallable.BusyTone);
        }
    }

    private void CloseHandsetUis(EntityUid phone)
    {
        var handsetQuery = EntityQueryEnumerator<TelephoneHandsetComponent>();
        while (handsetQuery.MoveNext(out var handsetUid, out var handset))
        {
            if (GetEntity(handset.ParentPhone) != phone)
                continue;

            _ui.CloseUi(handsetUid, CallablePhoneUiKey.Key);
        }
    }

    public void UpdateUiState(Entity<TelephoneComponent> source)
    {
        var phones = new Dictionary<NetEntity, string>();

        var query = AllEntityQuery<CallablePhoneComponent, TelephoneComponent>();
        while (query.MoveNext(out var receiverUid, out var callable, out var receiverTelephone))
        {
            if (!callable.ListedInDirectory || receiverTelephone.UnlistedNumber)
                continue;

            if (receiverUid == source.Owner)
                continue;

            var receiver = (receiverUid, receiverTelephone);

            if (!_telephone.IsSourceInRangeOfReceiver(source, receiver))
                continue;

            phones.Add(GetNetEntity(receiverUid), GetPhoneDisplayName(receiverUid));
        }

        var state = new CallablePhoneBoundInterfaceState(phones);

        var handsetQuery = EntityQueryEnumerator<TelephoneHandsetComponent>();
        while (handsetQuery.MoveNext(out var handsetUid, out var handset))
        {
            if (GetEntity(handset.ParentPhone) != source.Owner)
                continue;

            _ui.SetUiState(handsetUid, CallablePhoneUiKey.Key, state);
        }
    }

}
