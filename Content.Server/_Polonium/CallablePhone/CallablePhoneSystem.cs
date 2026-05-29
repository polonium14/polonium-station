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
using Content.Shared.Speech;
using Content.Shared.Telephone;
using Content.Shared.UserInterface;
using Robust.Server.Player;
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
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private readonly HashSet<EntityUid> _centCommAwaitingPickup = new();
    private readonly HashSet<EntityUid> _centCommActiveCalls = new();
    private readonly Dictionary<EntityUid, NetUserId> _centCommAnsweringAdmin = new();

    private readonly HashSet<EntityUid> _ghostCallerPending = new();
    private readonly HashSet<EntityUid> _ghostCallerActiveCalls = new();
    private readonly Dictionary<EntityUid, NetUserId> _ghostCallerAdmin = new();

    /// <summary>
    /// Admins with an open chat window for a callable phone line.
    /// </summary>
    private readonly Dictionary<NetEntity, HashSet<ICommonSession>> _openAdminChats = new();

    private float _updateTimer = 1f;
    private const float UpdateTime = 1f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CallablePhoneComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CallablePhoneComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CallablePhoneComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<CallablePhoneComponent, EntRemovedFromContainerMessage>(OnRemoved);

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
        SubscribeLocalEvent<CallablePhoneComponent, TelephoneMessageReceivedEvent>(OnCallablePhoneMessageReceived);

        SubscribeNetworkEvent<CentCommCallPickupResponseEvent>(OnCentCommPickupResponse);
        SubscribeNetworkEvent<CallablePhoneAdminChatSendMessageEvent>(OnAdminChatSendMessage);
        SubscribeNetworkEvent<CallablePhoneAdminChatCloseEvent>(OnAdminChatClose);
        SubscribeNetworkEvent<CallablePhoneAdminChatSetImpersonationNameEvent>(OnAdminChatSetImpersonationName);
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
        StopHandsetHolderAudio(entity);

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

        StopHandsetHolderAudio(entity);
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
            _telephone.AnswerTelephone((phone, telephone), args.User);

            if (callable.IsCentComm)
            {
                _centCommAwaitingPickup.Remove(phone);
                PlayRemoteDisconnectOnCallers(telephone);
            }
        }

        UpdateHandsetRelay(phone, telephone, args.User);

        UpdateUiState((phone, telephone));

        if (telephone.CurrentState == TelephoneState.Idle)
        {
            _ui.TryOpenUi(handset.Owner, CallablePhoneUiKey.Key, args.User);
            StartDialToneLoop((phone, callable));
        }
        else if (telephone.CurrentState == TelephoneState.Calling)
        {
            StartCallWaitingLoop((phone, callable));
        }
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
                UpdateHandsetRelay(phone, telephone, null);
            }

            callable.HandsetHolder = null;
            Dirty(phone, callable);
        }

        StopHandsetHolderAudio((phone, callable));
    }

    private void OnTelephoneStateChange(Entity<CallablePhoneComponent> entity, ref TelephoneStateChangeEvent args)
    {
        if (args.OldState == TelephoneState.Calling)
            StopCallWaitingLoop(entity);

        if (args.OldState == TelephoneState.Calling && args.NewState == TelephoneState.Idle)
            ClearGhostCallerPending(entity.Owner);

        if (args.NewState != TelephoneState.Idle)
            CloseHandsetUis(entity);

        if (args.NewState == TelephoneState.Idle && entity.Comp.HandsetHolder != null)
            StartDialToneLoop(entity);
        else
            StopDialToneLoop(entity);
    }

    private void OnCallCommenced(Entity<CallablePhoneComponent> entity, ref TelephoneCallCommencedEvent args)
    {
        StopCallWaitingLoop(entity);

        if (!TryComp<TelephoneComponent>(entity, out var telephone))
            return;

        UpdateHandsetRelay(entity, telephone, entity.Comp.HandsetHolder);
        TryOpenGhostCallerDeviceChat(entity);
    }

    private void OnCallEnded(Entity<CallablePhoneComponent> entity, ref TelephoneCallEndedEvent args)
    {
        StopCallWaitingLoop(entity);
        ClearHandsetMicrophones(entity);
        EndGhostCallerDeviceChat(entity.Owner);
        ClearAdminImpersonation(entity);

        if (!entity.Comp.IsCentComm)
            return;

        _centCommAwaitingPickup.Remove(entity);
        _centCommAnsweringAdmin.Remove(entity);

        if (_centCommActiveCalls.Remove(entity))
        {
            NotifyAdminChatLog(entity, Loc.GetString("callable-phone-centcomm-call-ended"));
            SetAdminChatInputEnabled(entity, false);
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

        StopBusyToneLoop(source);
        StopCallWaitingLoop(source);
        StopDialToneLoop(source);

        if (!TryComp<TelephoneComponent>(source, out var sourceTelephone))
            return;

        var receiverUid = GetEntity(args.Receiver);

        if (!TryComp<CallablePhoneComponent>(receiverUid, out var receiverCallable) ||
            !CanSourceDialReceiver(source.Comp, receiverCallable) ||
            !TryComp<TelephoneComponent>(receiverUid, out var receiverTelephone))
        {
            _popup.PopupEntity(Loc.GetString("callable-phone-call-invalid"), source, args.Actor);
            return;
        }

        var sourceEnt = (source.Owner, sourceTelephone);
        var receiverEnt = (receiverUid, receiverTelephone);

        if (_telephone.IsTelephoneEngaged(receiverEnt) || IsHandsetOffHook(receiverUid))
        {
            BeginBusyCallAudio(source);
            return;
        }

        var callOptions = new TelephoneCallOptions { IgnoreRange = true };

        StartOutboundCallWithDialDelay(
            source,
            sourceEnt,
            receiverEnt,
            receiverUid,
            receiverCallable,
            args.Actor,
            callOptions);
    }

    private void StartOutboundCallWithDialDelay(
        Entity<CallablePhoneComponent> source,
        Entity<TelephoneComponent> sourceEnt,
        Entity<TelephoneComponent> receiverEnt,
        EntityUid receiverUid,
        CallablePhoneComponent receiverCallable,
        EntityUid user,
        TelephoneCallOptions? callOptions)
    {
        if (!TryGetDialSoundDelay(source.Comp.DialSound, out var delay))
        {
            FinalizeOutboundCall(source, sourceEnt, receiverEnt, receiverUid, receiverCallable, user, callOptions);
            return;
        }

        PlayDialSound(source);
        var generation = source.Comp.CallWaitingDelayGeneration;

        Timer.Spawn(delay, () =>
        {
            if (!Exists(source) || source.Comp.CallWaitingDelayGeneration != generation)
                return;

            if (source.Comp.HandsetHolder == null || !UserHoldingPhoneHandset(source, user))
                return;

            if (_telephone.IsTelephoneEngaged(receiverEnt) || IsHandsetOffHook(receiverUid))
            {
                StartBusyToneLoop(source);
                return;
            }

            FinalizeOutboundCall(source, sourceEnt, receiverEnt, receiverUid, receiverCallable, user, callOptions);
        });
    }

    private void FinalizeOutboundCall(
        Entity<CallablePhoneComponent> source,
        Entity<TelephoneComponent> sourceEnt,
        Entity<TelephoneComponent> receiverEnt,
        EntityUid receiverUid,
        CallablePhoneComponent receiverCallable,
        EntityUid user,
        TelephoneCallOptions? callOptions)
    {
        _telephone.CallTelephone(sourceEnt, receiverEnt, user, callOptions);

        if (!_telephone.IsTelephoneEngaged(sourceEnt))
        {
            _popup.PopupEntity(Loc.GetString("callable-phone-call-failed"), source, user);
            return;
        }

        StartCallWaitingLoop(source);

        if (TryGetGhostCallerSession(user, out var ghostSession))
        {
            _ghostCallerPending.Add(source.Owner);
            _ghostCallerAdmin[source.Owner] = ghostSession.UserId;
        }

        if (receiverCallable.IsCentComm)
            BeginCentCommCall(receiverUid, receiverEnt.Comp);
    }

    private bool TryGetDialSoundDelay(SoundSpecifier? dialSound, out TimeSpan delay)
    {
        delay = default;

        if (dialSound == null)
            return false;

        var resolvedDial = _audio.ResolveSound(dialSound);
        if (ResolvedSoundSpecifier.IsNullOrEmpty(resolvedDial))
            return false;

        delay = _audio.GetAudioLength(resolvedDial);
        return true;
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

    private void OpenAdminChat(ICommonSession admin, EntityUid uid, bool inputEnabled = true)
    {
        if (!Exists(uid))
            return;

        var netEntity = GetNetEntity(uid);
        var openEvent = new CallablePhoneAdminChatOpenEvent(netEntity, admin.Name, inputEnabled);

        RegisterAdminChat(admin, netEntity);
        RaiseNetworkEvent(openEvent, admin);
    }

    private void NotifyAdminChatLog(EntityUid uid, string message)
    {
        NotifyAdminChatListeners(uid, string.Empty, message, incoming: false, isLog: true);
    }

    private void SetAdminChatInputEnabled(EntityUid uid, bool enabled)
    {
        var netEntity = GetNetEntity(uid);

        if (!_openAdminChats.ContainsKey(netEntity))
            return;

        var ev = new CallablePhoneAdminChatSetInputEnabledEvent(netEntity, enabled);

        foreach (var session in _openAdminChats[netEntity].ToArray())
        {
            RaiseNetworkEvent(ev, session);
        }
    }

    private bool IsAdminInOpenChat(ICommonSession admin, EntityUid uid)
    {
        return _openAdminChats.TryGetValue(GetNetEntity(uid), out var sessions) && sessions.Contains(admin);
    }

    private void NotifyAdminChatListeners(EntityUid uid, string sender, string message, bool incoming, bool isLog = false)
    {
        var netEntity = GetNetEntity(uid);

        if (!_openAdminChats.TryGetValue(netEntity, out var sessions))
            return;

        var chatMessage = new CallablePhoneAdminChatTextMessageEvent(netEntity, sender, message, incoming, isLog);

        foreach (var session in sessions.ToArray())
        {
            RaiseNetworkEvent(chatMessage, session);
        }
    }

    private void RegisterAdminChat(ICommonSession session, NetEntity entity)
    {
        if (!_openAdminChats.TryGetValue(entity, out var sessions))
        {
            sessions = new HashSet<ICommonSession>();
            _openAdminChats[entity] = sessions;
        }

        sessions.Add(session);
    }

    private void UnregisterAdminChat(ICommonSession session, NetEntity entity)
    {
        if (!_openAdminChats.TryGetValue(entity, out var sessions))
            return;

        sessions.Remove(session);

        if (sessions.Count == 0)
            _openAdminChats.Remove(entity);
    }

    private bool IsCentCommCallActive(EntityUid uid)
    {
        return TryComp<TelephoneComponent>(uid, out var telephone)
            && telephone.CurrentState == TelephoneState.InCall;
    }

    private void OnAdminChatSendMessage(CallablePhoneAdminChatSendMessageEvent msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.IsAdmin(args.SenderSession))
            return;

        if (!TryGetEntity(msg.Phone, out var uid) ||
            !TryComp<CallablePhoneComponent>(uid, out var callable))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(msg.Message))
            return;

        if (!IsAdminChatCallActive(uid.Value, callable))
            return;

        var message = msg.Message.Trim();

        if (IsGhostCallerAdmin(uid.Value, args.SenderSession))
        {
            ReplyThroughGhostCallerPhone(args.SenderSession, uid.Value, callable, message);
            NotifyAdminChatListeners(uid.Value, GetAdminChatDisplayName(uid.Value, args.SenderSession, callable), message, incoming: false);
            return;
        }

        if (!callable.IsCentComm)
            return;

        ReplyThroughCentCommPhone(args.SenderSession, uid.Value, callable, message);
        NotifyAdminChatListeners(uid.Value, GetAdminChatDisplayName(uid.Value, args.SenderSession, callable), message, incoming: false);
    }

    private void OnAdminChatSetImpersonationName(CallablePhoneAdminChatSetImpersonationNameEvent msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.IsAdmin(args.SenderSession))
            return;

        if (!TryGetEntity(msg.Phone, out var uid) || !TryComp<CallablePhoneComponent>(uid, out var callable))
            return;

        if (!IsAdminInOpenChat(args.SenderSession, uid.Value))
            return;

        var trimmed = msg.Name.Trim();
        callable.AdminImpersonationName = string.IsNullOrWhiteSpace(trimmed) ? null : trimmed[..Math.Min(trimmed.Length, 32)];

        var logMessage = callable.AdminImpersonationName == null
            ? Loc.GetString("callable-phone-impersonation-cleared")
            : Loc.GetString("callable-phone-impersonation-applied", ("name", callable.AdminImpersonationName));

        NotifyAdminChatLog(uid.Value, logMessage);
    }

    private string GetAdminChatDisplayName(EntityUid phone, ICommonSession session, CallablePhoneComponent callable)
    {
        if (!string.IsNullOrWhiteSpace(callable.AdminImpersonationName))
            return callable.AdminImpersonationName;

        return session.Name;
    }

    private void ClearAdminImpersonation(Entity<CallablePhoneComponent> entity)
    {
        entity.Comp.AdminImpersonationName = null;
    }

    private bool IsAdminChatCallActive(EntityUid uid, CallablePhoneComponent callable)
    {
        if (_ghostCallerActiveCalls.Contains(uid))
            return true;

        if (callable.IsCentComm)
            return IsCentCommCallActive(uid);

        return false;
    }

    private bool IsGhostCallerAdmin(EntityUid phone, ICommonSession session)
    {
        return _ghostCallerAdmin.TryGetValue(phone, out var adminId) && adminId == session.UserId;
    }

    private void ReplyThroughCentCommPhone(ICommonSession admin, EntityUid uid, CallablePhoneComponent callable, string message)
    {
        var name = callable.AdminImpersonationName ?? Loc.GetString(callable.AdminChatPrefix);

        if (TryComp<TelephoneComponent>(uid, out var telephone) && _telephone.IsTelephoneEngaged((uid, telephone)))
            _telephone.RelayTelephoneMessage(uid, message, (uid, telephone), skipCentCommReceivers: true);

        _audio.PlayPvs("/Audio/Items/ring.ogg", uid, AudioParams.Default.WithVolume(-8f));

        _adminLogger.Add(
            LogType.AdminMessage,
            LogImpact.Low,
            $"{admin.Name} spoke through {ToPrettyString(uid)} as {name}: {message}");
    }

    private void ReplyThroughGhostCallerPhone(ICommonSession admin, EntityUid uid, CallablePhoneComponent callable, string message)
    {
        if (TryComp<TelephoneComponent>(uid, out var telephone) && _telephone.IsTelephoneEngaged((uid, telephone)))
            _telephone.RelayTelephoneMessage(uid, message, (uid, telephone));

        var name = callable.AdminImpersonationName ?? admin.Name;

        _adminLogger.Add(
            LogType.AdminMessage,
            LogImpact.Low,
            $"{admin.Name} spoke through {ToPrettyString(uid)} as {name}: {message}");
    }

    private void OnAdminChatClose(CallablePhoneAdminChatCloseEvent msg, EntitySessionEventArgs args)
    {
        UnregisterAdminChat(args.SenderSession, msg.Phone);
        OnAdminChatClosed(msg, args);
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

    private void OnAdminChatClosed(CallablePhoneAdminChatCloseEvent msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.IsAdmin(args.SenderSession, includeDeAdmin: true))
            return;

        if (!TryGetEntity(msg.Phone, out var phone))
            return;

        if (TryEndGhostCallerCallOnChatClose(phone.Value, args.SenderSession))
            return;

        if (!_centCommAnsweringAdmin.TryGetValue(phone.Value, out var answeringAdmin) ||
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

    private bool TryEndGhostCallerCallOnChatClose(EntityUid phone, ICommonSession session)
    {
        if (!_ghostCallerActiveCalls.Contains(phone))
            return false;

        if (!IsGhostCallerAdmin(phone, session))
            return false;

        if (!TryComp<TelephoneComponent>(phone, out var telephone))
            return false;

        _telephone.EndTelephoneCalls((phone, telephone));
        return true;
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

        if (IsAdminInOpenChat(admin, phone))
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

                OpenAdminChat(admin, phone);
                NotifyAdminChatLog(phone, Loc.GetString("callable-phone-centcomm-call-started"));
            }
            else if (telephone.CurrentState == TelephoneState.InCall)
            {
                _centCommActiveCalls.Add(phone);
                OpenAdminChat(admin, phone);
                NotifyAdminChatLog(
                    phone,
                    Loc.GetString("callable-phone-centcomm-admin-joined", ("admin", admin.Name)));
            }

            return;
        }

        _centCommActiveCalls.Add(phone);
        OpenAdminChat(admin, phone);
        NotifyAdminChatLog(
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

    private void OnCallablePhoneMessageReceived(Entity<CallablePhoneComponent> entity, ref TelephoneMessageReceivedEvent args)
    {
        if (!entity.Comp.IsCentComm && !_ghostCallerActiveCalls.Contains(entity.Owner))
            return;

        var nameEv = new TransformSpeakerNameEvent(args.MessageSource, Name(args.MessageSource));
        RaiseLocalEvent(args.MessageSource, nameEv);

        NotifyAdminChatListeners(entity, nameEv.VoiceName, args.Message, incoming: true);
    }

    private bool TryGetGhostCallerSession(EntityUid user, out ICommonSession session)
    {
        session = default!;

        if (!HasComp<GhostComponent>(user))
            return false;

        if (TryComp<GhostComponent>(user, out var ghost) && !ghost.CanGhostInteract)
            return false;

        if (!TryComp<ActorComponent>(user, out var actor))
            return false;

        if (!_adminManager.IsAdmin(actor.PlayerSession, includeDeAdmin: true))
            return false;

        session = actor.PlayerSession;
        return true;
    }

    private void TryOpenGhostCallerDeviceChat(Entity<CallablePhoneComponent> entity)
    {
        if (!_ghostCallerPending.Remove(entity.Owner))
            return;

        if (!_ghostCallerAdmin.TryGetValue(entity.Owner, out var adminId) ||
            !_playerManager.TryGetSessionById(adminId, out var session))
        {
            ClearGhostCallerPending(entity.Owner);
            return;
        }

        _ghostCallerActiveCalls.Add(entity.Owner);
        OpenAdminChat(session, entity.Owner);
        NotifyAdminChatLog(entity.Owner, Loc.GetString("callable-phone-centcomm-call-started"));
    }

    private void ClearGhostCallerPending(EntityUid phone)
    {
        if (_ghostCallerActiveCalls.Contains(phone))
            return;

        _ghostCallerPending.Remove(phone);
        _ghostCallerAdmin.Remove(phone);
    }

    private void EndGhostCallerDeviceChat(EntityUid phone)
    {
        var wasActive = _ghostCallerActiveCalls.Remove(phone);
        _ghostCallerPending.Remove(phone);
        _ghostCallerAdmin.Remove(phone);

        if (!wasActive)
            return;

        NotifyAdminChatLog(phone, Loc.GetString("callable-phone-centcomm-call-ended"));
        SetAdminChatInputEnabled(phone, false);
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

    private void PlayHolderPhoneSound(EntityUid holder, SoundSpecifier? sound, AudioParams? audioParams = null)
    {
        if (sound == null)
            return;

        _audio.PlayGlobal(sound, holder, audioParams ?? AudioParams.Default);
    }

    private void PlayDialSound(Entity<CallablePhoneComponent> entity)
    {
        var holder = entity.Comp.HandsetHolder;
        if (holder == null || !Exists(holder))
            return;

        PlayHolderPhoneSound(holder.Value, entity.Comp.DialSound);
    }

    private void BeginBusyCallAudio(Entity<CallablePhoneComponent> entity)
    {
        StopCallWaitingLoop(entity);
        StopBusyToneLoop(entity);
        StopDialToneLoop(entity);

        if (entity.Comp.DialSound == null)
        {
            StartBusyToneLoop(entity);
            return;
        }

        PlayDialSound(entity);
        if (!TryGetDialSoundDelay(entity.Comp.DialSound, out var delay))
        {
            StartBusyToneLoop(entity);
            return;
        }

        var generation = entity.Comp.CallWaitingDelayGeneration;

        Timer.Spawn(delay, () =>
        {
            if (!Exists(entity) || entity.Comp.CallWaitingDelayGeneration != generation)
                return;

            if (entity.Comp.HandsetHolder == null)
                return;

            StartBusyToneLoop(entity);
        });
    }

    private void StartCallWaitingLoop(Entity<CallablePhoneComponent> entity)
    {
        if (entity.Comp.CallWaitingTone == null || entity.Comp.CallWaitingStream != null)
            return;

        var holder = entity.Comp.HandsetHolder;
        if (holder == null || !Exists(holder))
            return;

        entity.Comp.CallWaitingStream = _audio.PlayGlobal(
            entity.Comp.CallWaitingTone,
            holder.Value,
            AudioParams.Default.WithLoop(true))?.Entity;
    }

    private void StopCallWaitingLoop(Entity<CallablePhoneComponent> entity)
    {
        entity.Comp.CallWaitingDelayGeneration++;
        entity.Comp.CallWaitingStream = _audio.Stop(entity.Comp.CallWaitingStream);
    }

    private void StartBusyToneLoop(Entity<CallablePhoneComponent> entity)
    {
        if (entity.Comp.BusyTone == null || entity.Comp.BusyToneStream != null)
            return;

        var holder = entity.Comp.HandsetHolder;
        if (holder == null || !Exists(holder))
            return;

        entity.Comp.BusyToneStream = _audio.PlayGlobal(
            entity.Comp.BusyTone,
            holder.Value,
            AudioParams.Default.WithLoop(true))?.Entity;
    }

    private void StopBusyToneLoop(Entity<CallablePhoneComponent> entity)
    {
        entity.Comp.BusyToneStream = _audio.Stop(entity.Comp.BusyToneStream);
    }

    private void StopHandsetHolderAudio(Entity<CallablePhoneComponent> entity)
    {
        StopDialToneLoop(entity);
        StopCallWaitingLoop(entity);
        StopBusyToneLoop(entity);
    }

    private void StartDialToneLoop(Entity<CallablePhoneComponent> entity)
    {
        if (entity.Comp.DialTone == null || entity.Comp.DialToneStream != null)
            return;

        var holder = entity.Comp.HandsetHolder;
        if (holder == null || !Exists(holder))
            return;

        if (!TryComp<TelephoneComponent>(entity, out var telephone) || telephone.CurrentState != TelephoneState.Idle)
            return;

        if (entity.Comp.BusyToneStream != null || entity.Comp.CallWaitingStream != null)
            return;

        entity.Comp.DialToneStream = _audio.PlayGlobal(
            entity.Comp.DialTone,
            holder.Value,
            AudioParams.Default.WithLoop(true))?.Entity;
    }

    private void StopDialToneLoop(Entity<CallablePhoneComponent> entity)
    {
        entity.Comp.DialToneStream = _audio.Stop(entity.Comp.DialToneStream);
    }

    private void PlayHandsetHangup(Entity<CallablePhoneComponent> phone, bool micVariant, EntityUid? holder = null)
    {
        var sound = micVariant ? phone.Comp.HangupHandsetInCallSound : phone.Comp.HangupHandsetSound;
        holder ??= micVariant ? phone.Comp.HandsetHolder : null;

        if (holder != null)
            PlayHolderPhoneSound(holder.Value, sound);
        else
            PlayPhoneSound(phone, sound);
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

            var caller = (linked, callerCallable);
            StopCallWaitingLoop(caller);
            StopDialToneLoop(caller);
            StartBusyToneLoop(caller);
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

        if (!TryComp<CallablePhoneComponent>(source.Owner, out var sourceCallable))
            return;

        var query = AllEntityQuery<CallablePhoneComponent, TelephoneComponent>();
        while (query.MoveNext(out var receiverUid, out var callable, out var receiverTelephone))
        {
            if (receiverTelephone.UnlistedNumber)
                continue;

            if (receiverUid == source.Owner)
                continue;

            if (!CanSourceSeeInDirectory(sourceCallable, callable))
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
