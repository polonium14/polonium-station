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
using Content.Shared.Hands.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Speech;
using Content.Shared.Telephone;
using Content.Shared.UserInterface;
using Robust.Server.Player;
using Robust.Shared.Utility;
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
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    private readonly HashSet<EntityUid> _centCommAwaitingPickup = new();
    private readonly HashSet<EntityUid> _centCommActiveCalls = new();
    private readonly Dictionary<EntityUid, NetUserId> _centCommAnsweringAdmin = new();
    private readonly Dictionary<EntityUid, EntityUid> _centCommRingingCaller = new();

    private readonly HashSet<EntityUid> _ghostCallerPending = new();
    private readonly HashSet<EntityUid> _ghostCallerActiveCalls = new();
    private readonly Dictionary<EntityUid, NetUserId> _ghostCallerAdmin = new();

    /// <summary>
    /// Admins with an open chat window for a callable phone line.
    /// </summary>
    private readonly Dictionary<NetEntity, HashSet<ICommonSession>> _openAdminChats = new();

    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private float _cordCheckTimer;
    private float _updateTimer;
    private const float CordCheckTime = 0.25f;
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
        SubscribeLocalEvent<TelephoneHandsetComponent, DroppedEvent>(OnHandsetDropped);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
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

        SubscribeLocalEvent<CallablePhoneComponent, TransformSpeakerNameEvent>(OnAdminPhoneTransformSpeakerName);
        SubscribeLocalEvent<CallablePhoneComponent, ItemSlotEjectAttemptEvent>(OnHandsetEjectAttempt);
    }

    public override bool ShouldUseAnonymousAdminCallerName(EntityUid phone, CallablePhoneComponent callable)
    {
        if (callable.IsCentComm)
            return true;

        return _ghostCallerActiveCalls.Contains(phone);
    }

    private void OnAdminPhoneTransformSpeakerName(Entity<CallablePhoneComponent> entity, ref TransformSpeakerNameEvent args)
    {
        if (!string.IsNullOrWhiteSpace(entity.Comp.AdminImpersonationName))
        {
            args.VoiceName = entity.Comp.AdminImpersonationName;
            return;
        }

        if (!ShouldUseAnonymousAdminCallerName(entity.Owner, entity.Comp))
            return;

        args.VoiceName = Loc.GetString("callable-phone-admin-unknown-caller");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _cordCheckTimer += frameTime;
        if (_cordCheckTimer >= CordCheckTime)
        {
            _cordCheckTimer -= CordCheckTime;
            UpdateHandsetCordChecks();
        }

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

        RefreshAllCallablePhoneDirectories();
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

        if (IsCentCommHandsetLockedForNonAdmin(phone, callable, args.User))
        {
            var phoneEnt = phone;
            Timer.Spawn(TimeSpan.Zero, () =>
            {
                if (!Exists(phoneEnt) || !TryComp<CallablePhoneComponent>(phoneEnt, out var comp))
                    return;

                TryReturnHandsetToBase((phoneEnt, comp));
            });
            return;
        }

        var wasRemoteCentCommSession = callable.IsCentComm &&
                                       _centCommActiveCalls.Contains(phone) &&
                                       callable.HandsetHolder == null;

        callable.HandsetHolder = args.User;
        Dirty(phone, callable);

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

        if (wasRemoteCentCommSession)
            TransferCentCommCallToIc(phone, args.User);

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

        if (callable.HandsetHolder != args.User)
            return;

        var phoneEnt = phone;
        var user = args.User;

        Timer.Spawn(TimeSpan.Zero, () =>
        {
            if (!Exists(phoneEnt) || !TryComp<CallablePhoneComponent>(phoneEnt, out var comp))
                return;

            if (UserHoldingPhoneHandset(phoneEnt, user))
            {
                comp.HandsetHolder = user;
                Dirty(phoneEnt, comp);
                return;
            }

            if (comp.HandsetHolder != user)
                return;

            if (TryComp<TelephoneComponent>(phoneEnt, out var telephone))
                UpdateHandsetRelay(phoneEnt, telephone, null);

            comp.HandsetHolder = null;
            Dirty(phoneEnt, comp);
            StopHandsetHolderAudio((phoneEnt, comp));
        });
    }

    private void OnHandsetDropped(Entity<TelephoneHandsetComponent> handset, ref DroppedEvent args)
    {
        if (ShouldExemptHandsetReturn(args.User))
            return;

        var phone = GetEntity(handset.Comp.ParentPhone);
        if (!TryComp<CallablePhoneComponent>(phone, out var callable))
            return;

        TryReturnHandsetToBase((phone, callable));
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (ShouldExemptHandsetReturn(args.Target))
            return;

        var query = EntityQueryEnumerator<CallablePhoneComponent>();
        while (query.MoveNext(out var phone, out var callable))
        {
            if (callable.HandsetHolder != args.Target)
                continue;

            TryReturnHandsetToBase((phone, callable));
        }
    }

    private void UpdateHandsetCordChecks()
    {
        var query = AllEntityQuery<CallablePhoneComponent>();
        while (query.MoveNext(out var phone, out var callable))
        {
            if (IsHandsetInCradle(phone))
                continue;

            var handset = GetOffHookHandset(phone, callable.HandsetHolder);
            if (handset == null)
                continue;

            var holder = ResolveHandsetHolder(phone, callable, handset.Value);
            if (holder == null)
            {
                TryReturnHandsetToBase((phone, callable));
                continue;
            }

            if (ShouldExemptHandsetReturn(holder.Value))
                continue;

            if (!IsWithinHandsetCordRange(phone, holder.Value, callable))
                TryReturnHandsetToBase((phone, callable));
        }
    }

    private EntityUid? ResolveHandsetHolder(EntityUid phone, CallablePhoneComponent callable, EntityUid handset)
    {
        if (callable.HandsetHolder != null && Exists(callable.HandsetHolder))
        {
            if (Hands.IsHolding(callable.HandsetHolder.Value, handset))
                return callable.HandsetHolder;

            if (UserHoldingPhoneHandset(phone, callable.HandsetHolder.Value))
                return callable.HandsetHolder;
        }

        var holderQuery = AllEntityQuery<HandsComponent>();
        while (holderQuery.MoveNext(out var uid, out var hands))
        {
            if (!Hands.IsHolding(uid, handset, out _, hands))
                continue;

            if (callable.HandsetHolder != uid)
            {
                callable.HandsetHolder = uid;
                Dirty(phone, callable);
            }

            return uid;
        }

        if (callable.HandsetHolder != null)
        {
            callable.HandsetHolder = null;
            Dirty(phone, callable);
        }

        return null;
    }

    private bool ShouldExemptHandsetReturn(EntityUid entity)
    {
        return TryGetGhostCallerSession(entity, out _);
    }

    private bool TryReturnHandsetToBase(Entity<CallablePhoneComponent> phone)
    {
        if (IsHandsetInCradle(phone))
            return false;

        if (!_itemSlots.TryGetSlot(phone, CallablePhoneComponent.HandsetSlotId, out var slot) ||
            slot.ContainerSlot == null ||
            slot.HasItem)
        {
            return false;
        }

        var handset = GetOffHookHandset(phone, phone.Comp.HandsetHolder);
        if (handset == null || !Exists(handset))
            return false;

        var holderQuery = AllEntityQuery<HandsComponent>();
        while (holderQuery.MoveNext(out var holderUid, out var hands))
        {
            if (!Hands.IsHolding(holderUid, handset, out _, hands))
                continue;

            return Hands.TryDropIntoContainer(
                holderUid,
                handset.Value,
                slot.ContainerSlot,
                checkActionBlocker: false,
                hands);
        }

        if (_containers.TryGetContainingContainer(handset.Value, out var container) &&
            container.Owner != phone.Owner)
        {
            _containers.Remove(handset.Value, container);
        }

        return _itemSlots.TryInsert(
            phone,
            CallablePhoneComponent.HandsetSlotId,
            handset.Value,
            user: null,
            excludeUserAudio: true);
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

        var wasAwaitingPickup = _centCommAwaitingPickup.Contains(entity.Owner);
        _centCommRingingCaller.TryGetValue(entity.Owner, out var ringingCaller);

        _centCommAwaitingPickup.Remove(entity.Owner);
        _centCommRingingCaller.Remove(entity.Owner);
        _centCommAnsweringAdmin.Remove(entity.Owner);

        if (wasAwaitingPickup && ringingCaller != EntityUid.Invalid)
            ApplyCentCommTimeoutRejection(entity.Owner, ringingCaller);

        if (_centCommActiveCalls.Remove(entity.Owner))
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

        if (!TryResolveCallablePhoneReceiver(args.Receiver, source.Comp, out var receiverUid, out var receiverCallable, out var receiverTelephone))
        {
            _popup.PopupEntity(Loc.GetString("callable-phone-call-invalid"), source, args.Actor);
            return;
        }

        var sourceEnt = (source.Owner, sourceTelephone);
        var receiverEnt = (receiverUid.Value, receiverTelephone);

        if (_telephone.IsTelephoneEngaged(receiverEnt) || IsHandsetOffHook(receiverUid.Value))
        {
            BeginBusyCallAudio(source);
            return;
        }

        var callOptions = new TelephoneCallOptions { IgnoreRange = true };

        StartOutboundCallWithDialDelay(
            source,
            sourceEnt,
            args.Receiver,
            args.Actor,
            callOptions);
    }

    private void StartOutboundCallWithDialDelay(
        Entity<CallablePhoneComponent> source,
        Entity<TelephoneComponent> sourceEnt,
        NetEntity receiverNet,
        EntityUid user,
        TelephoneCallOptions? callOptions)
    {
        if (!TryResolveCallablePhoneReceiver(receiverNet, source.Comp, out var receiverUid, out var receiverCallable, out var receiverTelephone))
        {
            _popup.PopupEntity(Loc.GetString("callable-phone-call-invalid"), source, user);
            return;
        }

        var receiverEnt = (receiverUid.Value, receiverTelephone);

        if (_telephone.IsTelephoneEngaged(receiverEnt) || IsHandsetOffHook(receiverUid.Value))
        {
            BeginBusyCallAudio(source);
            return;
        }

        if (!TryGetDialSoundDelay(source.Comp.DialSound, out var delay))
        {
            FinalizeOutboundCall(source, sourceEnt, receiverEnt, receiverUid.Value, receiverCallable, user, callOptions);
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

            if (!TryResolveCallablePhoneReceiver(receiverNet, source.Comp, out var resolvedReceiverUid, out var resolvedCallable, out var resolvedTelephone))
            {
                _popup.PopupEntity(Loc.GetString("callable-phone-call-invalid"), source, user);
                return;
            }

            var resolvedReceiverEnt = (resolvedReceiverUid.Value, resolvedTelephone);

            if (_telephone.IsTelephoneEngaged(resolvedReceiverEnt) || IsHandsetOffHook(resolvedReceiverUid.Value))
            {
                StartBusyToneLoop(source);
                return;
            }

            FinalizeOutboundCall(source, sourceEnt, resolvedReceiverEnt, resolvedReceiverUid.Value, resolvedCallable, user, callOptions);
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
        _centCommRingingCaller.Remove(phone);

        foreach (var linked in telephone.LinkedTelephones)
        {
            _centCommRingingCaller[phone] = linked;
            break;
        }

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

    private bool IsCentCommHandsetLockedForNonAdmin(EntityUid phone, CallablePhoneComponent callable, EntityUid? user)
    {
        if (!callable.IsCentComm)
            return false;

        if (user != null && _adminManager.IsAdmin(user.Value, includeDeAdmin: true))
            return false;

        if (_centCommAwaitingPickup.Contains(phone))
            return true;

        return _centCommActiveCalls.Contains(phone) && callable.HandsetHolder == null;
    }

    private void OnHandsetEjectAttempt(Entity<CallablePhoneComponent> entity, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Cancelled || args.Slot.ID != CallablePhoneComponent.HandsetSlotId)
            return;

        if (!IsCentCommHandsetLockedForNonAdmin(entity, entity.Comp, args.User))
            return;

        args.Cancelled = true;

        if (args.User != null)
            _popup.PopupEntity(Loc.GetString("callable-phone-centcomm-handset-locked"), entity, args.User.Value);
    }

    private void ForceCloseAdminChats(EntityUid phone)
    {
        var netEntity = GetNetEntity(phone);

        if (!_openAdminChats.TryGetValue(netEntity, out var sessions))
            return;

        var ev = new CallablePhoneAdminChatForceCloseEvent(netEntity);

        foreach (var session in sessions.ToArray())
            RaiseNetworkEvent(ev, session);

        _openAdminChats.Remove(netEntity);
    }

    private void TransferCentCommCallToIc(EntityUid phone, EntityUid admin)
    {
        NotifyAdminChatLog(phone, Loc.GetString("callable-phone-centcomm-admin-took-ic", ("admin", Name(admin))));
        ForceCloseAdminChats(phone);
        _centCommAnsweringAdmin.Remove(phone);
        _centCommActiveCalls.Remove(phone);
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

        return Loc.GetString("callable-phone-admin-unknown-caller");
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
        var name = callable.AdminImpersonationName ?? Loc.GetString("callable-phone-admin-unknown-caller");

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

        var name = callable.AdminImpersonationName ?? Loc.GetString("callable-phone-admin-unknown-caller");
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
            DeclineCentCommCall(phone.Value, msg.RejectionReason);
            return;
        }

        AcceptCentCommCall(args.SenderSession, phone.Value);
    }

    private void DeclineCentCommCall(EntityUid phone, string? rejectionReason = null)
    {
        if (!TryComp<TelephoneComponent>(phone, out var telephone))
            return;

        if (telephone.CurrentState != TelephoneState.Ringing && !_centCommAwaitingPickup.Contains(phone))
            return;

        var reason = ResolveCentCommRejectionReason(rejectionReason);
        NotifyCallersOfCentCommRejection(phone, telephone, reason);
        PlayRemoteBusyOnCallers(telephone);
        _centCommAwaitingPickup.Remove(phone);
        _centCommRingingCaller.Remove(phone);
        _telephone.EndTelephoneCalls((phone, telephone));
    }

    private string ResolveCentCommRejectionReason(string? rejectionReason)
    {
        if (string.IsNullOrWhiteSpace(rejectionReason))
            return Loc.GetString("callable-phone-centcomm-call-declined-default-reason");

        var sanitized = SharedChatSystem.SanitizeAnnouncement(
            rejectionReason.Trim(),
            CentCommCallPickupResponseEvent.MaxRejectionReasonLength,
            maxNewlines: 0);

        return string.IsNullOrWhiteSpace(sanitized)
            ? Loc.GetString("callable-phone-centcomm-call-declined-default-reason")
            : sanitized;
    }

    private void NotifyCallersOfCentCommRejection(EntityUid centCommPhone, TelephoneComponent centCommTelephone, string reason)
    {
        foreach (var linked in centCommTelephone.LinkedTelephones)
        {
            NotifyCallerOfCentCommRejection(centCommPhone, linked, reason);
        }
    }

    private void ApplyCentCommTimeoutRejection(EntityUid centCommPhone, EntityUid callerPhone)
    {
        NotifyCentCommTimeoutRejection(centCommPhone, callerPhone);
        PlayBusyToneOnCaller(callerPhone);
    }

    private void NotifyCallerOfCentCommRejection(EntityUid centCommPhone, EntityUid callerPhone, string reason)
    {
        if (!TryComp<CallablePhoneComponent>(callerPhone, out var callerCallable))
            return;

        var holder = callerCallable.HandsetHolder;
        if (holder == null)
            return;

        if (!_playerManager.TryGetSessionByEntity(holder.Value, out var session))
            return;

        var escapedReason = FormattedMessage.EscapeText(reason);
        var wrapped = Loc.GetString("callable-phone-centcomm-call-declined-chat", ("reason", escapedReason));
        var plain = Loc.GetString("callable-phone-centcomm-call-declined-chat-plain", ("reason", reason));

        _chatManager.ChatMessageToOne(
            ChatChannel.Local,
            plain,
            wrapped,
            centCommPhone,
            hideChat: false,
            session.Channel);
    }

    private void NotifyCentCommTimeoutRejection(EntityUid centCommPhone, EntityUid callerPhone)
    {
        if (!TryComp<CallablePhoneComponent>(callerPhone, out var callerCallable))
            return;

        var holder = callerCallable.HandsetHolder;
        if (holder == null)
            return;

        if (!_playerManager.TryGetSessionByEntity(holder.Value, out var session))
            return;

        var wrapped = Loc.GetString("callable-phone-centcomm-call-declined-timeout-chat");
        var plain = Loc.GetString("callable-phone-centcomm-call-declined-timeout-chat-plain");

        _chatManager.ChatMessageToOne(
            ChatChannel.Local,
            plain,
            wrapped,
            centCommPhone,
            hideChat: false,
            session.Channel);
    }

    private void PlayBusyToneOnCaller(EntityUid callerPhone)
    {
        if (!TryComp<CallablePhoneComponent>(callerPhone, out var callerCallable))
            return;

        var caller = (callerPhone, callerCallable);
        StopCallWaitingLoop(caller);
        StopDialToneLoop(caller);
        StartBusyToneLoop(caller);
    }

    private void AcceptCentCommCall(ICommonSession admin, EntityUid phone)
    {
        if (!TryComp<TelephoneComponent>(phone, out var telephone))
            return;

        if (TryComp<CallablePhoneComponent>(phone, out var callable) && callable.HandsetHolder != null)
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
                _centCommRingingCaller.Remove(phone);
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

        if (entity.Comp.IsCentComm && entity.Comp.HandsetHolder != null)
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
            PlayBusyToneOnCaller(linked);
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

    private void RefreshAllCallablePhoneDirectories()
    {
        var query = AllEntityQuery<CallablePhoneComponent, TelephoneComponent>();
        while (query.MoveNext(out var uid, out _, out var telephone))
        {
            UpdateUiState((uid, telephone));
        }
    }

}
