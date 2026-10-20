using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Administration.Logs;
using Content.Shared._Polonium.Paper;
using Content.Shared._Polonium.Photography;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Flash;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Utility;

namespace Content.Server._Polonium.Photography;

/// <summary>
/// Server half. Shutter press issues a one-shot capture token to the shooter's client. On
/// answer, token and payload length are re-validated before bytes are stored (per-round, in
/// memory) and a photograph entity carrying only the storage id is spawned. The blob streams
/// to a single viewer via the BUI state, never an auto-networked field.
/// </summary>
public sealed partial class PoloniumPhotographySystem : SharedPoloniumPhotographySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private SharedFlashSystem _flash = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly SoundSpecifier ShutterSound = new SoundPathSpecifier("/Audio/Misc/camera_snap.ogg");
    private static readonly SoundSpecifier SignSound = new SoundCollectionSpecifier("PaperScribbles", AudioParams.Default.WithVariation(0.1f));

    private sealed record PendingCapture(ICommonSession Session, EntityUid User, EntityCoordinates Coords, EntProtoId Photograph, string? SubjectName);

    // Per-round state, pruned incrementally: blobs freed when their photograph terminates,
    // pending tokens when the shooter answers, fires again, or disconnects.
    private readonly Dictionary<int, byte[]> _photos = new();
    // Subject name frozen at click time (Identity name for humanoids, else entity name),
    // freed with its blob. Absent = no nameable subject.
    private readonly Dictionary<int, string> _photoNames = new();
    private readonly Dictionary<int, string> _photoShooters = new();
    private readonly Dictionary<int, PendingCapture> _pending = new();
    // Pen a user put to a photo, waiting for their caption. Cleared on sign, close or restart.
    private readonly Dictionary<(EntityUid Photo, EntityUid User), EntityUid> _signPens = new();

    private int _nextCaptureId = 1;
    private int _nextPhotoId = 1;

    public int PendingCount => _pending.Count;

    /// <summary>The oldest outstanding capture token, or null if none is open.</summary>
    public int? PendingCaptureId => _pending.Count > 0 ? _pending.Keys.First() : null;

    public int StoredCount => _photos.Count;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<SubmitPhotoEvent>(OnSubmitPhoto);
        SubscribeLocalEvent<PoloniumPhotographComponent, EntityTerminatingEvent>(OnPhotoTerminating);
        SubscribeLocalEvent<PoloniumPhotographComponent, ExaminedEvent>(OnPhotoExamined);
        SubscribeLocalEvent<PoloniumPhotographComponent, InteractUsingEvent>(OnPhotoInteractUsing);
        SubscribeLocalEvent<PoloniumPhotographComponent, PhotoSignQueryMessage>(OnPhotoSignQuery);
        SubscribeLocalEvent<PoloniumPhotographComponent, PhotoSignMessage>(OnPhotoSign);
        SubscribeLocalEvent<PoloniumPhotographComponent, BoundUIClosedEvent>(OnPhotoUiClosed);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    protected override bool StartCapture(Entity<PoloniumCameraComponent> camera, EntityUid user, EntityCoordinates targetCoords, EntityUid? target)
    {
        if (!TryComp<ActorComponent>(user, out var actor))
            return false;

        var coords = targetCoords;
        var flash = _toggle.IsActivated(camera.Owner);

        // Freeze the subject name now. Identity.Name gives the disguise-respecting identity
        // for humanoids (viewer null = not see-through), else the plain entity name; a
        // bare-tile click (no target) leaves the photo subjectless.
        string? subjectName = null;
        if (target is { } subject && !TerminatingOrDeleted(subject))
            subjectName = Identity.Name(subject, EntityManager);

        // One outstanding capture per session: drop any earlier unanswered token so
        // _pending can't accumulate.
        RemovePendingForSession(actor.PlayerSession);

        var captureId = _nextCaptureId++;
        _pending[captureId] = new PendingCapture(actor.PlayerSession, user, coords, camera.Comp.Photograph, subjectName);

        _audio.PlayPvs(ShutterSound, camera.Owner);

        if (flash)
            FireFlash(user, coords);

        RaiseNetworkEvent(new RequestPhotoCaptureEvent(captureId, GetNetCoordinates(coords), flash), actor.PlayerSession);
        return true;
    }

    /// <summary>
    /// World side of a flash shot: a short-lived point light everyone in PVS sees (matching
    /// the client capture light's params) plus an area blind around that spot. The photo's
    /// own lighting is done separately on the capturing client.
    /// </summary>
    private void FireFlash(EntityUid user, EntityCoordinates coords)
    {
        var mapCoords = _transform.ToMapCoordinates(coords);
        if (mapCoords.MapId == MapId.Nullspace)
            return;

        var burst = Spawn(null, mapCoords);
        PhotoFlash.Configure(_pointLight, burst);
        EnsureComp<TimedDespawnComponent>(burst).Lifetime = PhotographyConstants.FlashBurstLifetime;

        _flash.FlashArea(burst, user, PhotographyConstants.FlashBlindRange, TimeSpan.FromSeconds(4), displayPopup: true);
    }

    private void OnSubmitPhoto(SubmitPhotoEvent ev, EntitySessionEventArgs args)
    {
        // Token must match a pending capture that THIS session was authorized for.
        if (!_pending.TryGetValue(ev.CaptureId, out var pending) || pending.Session != args.SenderSession)
            return;

        _pending.Remove(ev.CaptureId);

        // Fixed-size, non-null payload only, rejected without decoding (a crafted null
        // would otherwise NRE the handler).
        if (ev.Data is not { } data || data.Length != PhotographyConstants.PhotoByteLength)
            return;

        if (!pending.Coords.IsValid(EntityManager))
            return;

        var photoId = _nextPhotoId++;
        _photos[photoId] = data;
        if (pending.SubjectName is { } name)
            _photoNames[photoId] = name;
        _photoShooters[photoId] = pending.Session.Name;

        _adminLog.Add(LogType.Action, LogImpact.Extreme,
            $"{pending.Session:player} took photo id {new LoggablePhotoId(photoId)} (subject: {pending.SubjectName ?? "none"})");

        var spawned = Spawn(pending.Photograph, pending.Coords);
        var photoComp = EnsureComp<PoloniumPhotographComponent>(spawned);
        photoComp.PhotoId = photoId;
        Dirty(spawned, photoComp);

        UpdatePhotoUi((spawned, photoComp));

        if (!TerminatingOrDeleted(pending.User))
            _hands.PickupOrDrop(pending.User, spawned, dropNear: true);
    }

    private void OnPhotoTerminating(Entity<PoloniumPhotographComponent> ent, ref EntityTerminatingEvent args)
    {
        _photos.Remove(ent.Comp.PhotoId);
        _photoNames.Remove(ent.Comp.PhotoId);
        _photoShooters.Remove(ent.Comp.PhotoId);
    }

    /// <summary>Metadata for every photo stored this round, ordered by id.</summary>
    public List<(int Id, string Shooter, string? Subject)> GetStoredPhotos()
    {
        var list = new List<(int, string, string?)>(_photos.Count);
        foreach (var id in _photos.Keys)
        {
            var shooter = _photoShooters.GetValueOrDefault(id, "unknown");
            var subject = _photoNames.GetValueOrDefault(id);
            list.Add((id, shooter, subject));
        }

        list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return list;
    }

    /// <summary>The raw RGB565 blob for one stored photo, or null if it's gone.</summary>
    public byte[]? GetStoredPhoto(int id) => _photos.GetValueOrDefault(id);

    /// <summary>
    /// Admin removal of an abusive photo: deletes the photograph entity carrying this id
    /// (its terminating handler frees the blob), and clears the store directly for the rare
    /// case where the blob outlived its entity. False if nothing matched.
    /// </summary>
    public bool DeleteStoredPhoto(int id)
    {
        var found = false;
        var query = EntityQueryEnumerator<PoloniumPhotographComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.PhotoId != id)
                continue;
            QueueDel(uid);
            found = true;
        }

        if (_photos.Remove(id))
        {
            _photoNames.Remove(id);
            _photoShooters.Remove(id);
            found = true;
        }

        return found;
    }

    /// <summary>Pushes blob + caption to everyone with this photograph's viewer open (and later openers).</summary>
    private void UpdatePhotoUi(Entity<PoloniumPhotographComponent> ent)
    {
        var comp = ent.Comp;
        _ui.SetUiState(ent.Owner, PhotoViewerUiKey.Key,
            new PhotoViewerBoundUserInterfaceState(_photos.GetValueOrDefault(comp.PhotoId), comp.Caption, comp.CaptionColor, comp.CaptionFont));
    }

    /// <summary>
    /// Pen (anything with a <see cref="SignatureWriterComponent"/>) on a photograph: open the
    /// viewer for that user and ask their client to show the caption input. Nothing is
    /// written yet; that waits for <see cref="PhotoSignMessage"/>.
    /// </summary>
    private void OnPhotoInteractUsing(Entity<PoloniumPhotographComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryGetCaptionStyle(args.Used, out _, out _))
            return;

        args.Handled = true;

        if (ent.Comp.Caption != null)
        {
            _popup.PopupEntity(Loc.GetString("photograph-already-signed", ("target", ent.Owner)), args.User, args.User);
            return;
        }

        if (!HasComp<ActorComponent>(args.User))
            return;

        _signPens[(ent.Owner, args.User)] = args.Used;

        // Already looking at it: show the input now. Otherwise the freshly opened viewer
        // asks for it itself (PhotoSignQueryMessage), so the start can't beat the window.
        if (_ui.IsUiOpen(ent.Owner, PhotoViewerUiKey.Key, args.User))
            SendSignStart(ent.Owner, args.User);
        else
            _ui.OpenUi(ent.Owner, PhotoViewerUiKey.Key, args.User);
    }

    private void OnPhotoSignQuery(Entity<PoloniumPhotographComponent> ent, ref PhotoSignQueryMessage args)
    {
        SendSignStart(ent.Owner, args.Actor);
    }

    private void SendSignStart(EntityUid photo, EntityUid user)
    {
        if (!_signPens.TryGetValue((photo, user), out var pen) ||
            !TryGetCaptionStyle(pen, out var color, out var font))
            return;

        // The EntityUid overload: the ICommonSession one is a no-op on the server.
        _ui.ServerSendUiMessage(photo, PhotoViewerUiKey.Key, new PhotoSignStartMessage(color, font), user);
    }

    private void OnPhotoUiClosed(Entity<PoloniumPhotographComponent> ent, ref BoundUIClosedEvent args)
    {
        _signPens.Remove((ent.Owner, args.Actor));
    }

    private void OnPhotoSign(Entity<PoloniumPhotographComponent> ent, ref PhotoSignMessage args)
    {
        var signer = args.Actor;

        // One caption per photo; a second message (double submit, two pens) is ignored.
        if (ent.Comp.Caption != null)
            return;

        if (!_signPens.Remove((ent.Owner, signer), out var pen))
            return;

        // Re-validate everything the client claims: still holding a writer and still in reach.
        if (TerminatingOrDeleted(pen) ||
            !_hands.IsHolding(signer, pen) ||
            !_interaction.InRangeUnobstructed(signer, ent.Owner) ||
            !TryGetCaptionStyle(pen, out var color, out var font))
        {
            _popup.PopupEntity(Loc.GetString("photograph-sign-failure", ("target", ent.Owner)), signer, signer, PopupType.SmallCaution);
            return;
        }

        var caption = SanitizeCaption(args.Text);
        if (caption.Length == 0)
            return;

        ent.Comp.Caption = caption;
        ent.Comp.CaptionColor = color;
        ent.Comp.CaptionFont = font;
        Dirty(ent);

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(signer):player} signed photo id {new LoggablePhotoId(ent.Comp.PhotoId)} with caption \"{caption}\"");

        _popup.PopupEntity(Loc.GetString("photograph-signed-self", ("target", ent.Owner)), signer, signer);
        _popup.PopupEntity(Loc.GetString("photograph-signed-other", ("user", signer), ("target", ent.Owner)),
            signer, Filter.PvsExcept(signer, entityManager: EntityManager), true);
        _audio.PlayPvs(SignSound, signer);

        UpdatePhotoUi(ent);
    }

    private void OnPhotoExamined(Entity<PoloniumPhotographComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        // Anyone can read the strip; player-typed, so escaped like the subject name.
        if (ent.Comp.Caption is { } caption)
        {
            args.PushMarkup(Loc.GetString("photograph-examine-caption",
                ("caption", FormattedMessage.EscapeText(caption)),
                ("color", ent.Comp.CaptionColor.ToHex())));
        }

        // Subjectless photos examine like any plain item.
        if (!_photoNames.TryGetValue(ent.Comp.PhotoId, out var name))
            return;

        // Identity names are player-typed and may contain markup characters.
        args.PushMarkup(Loc.GetString("photograph-examine-subject", ("name", FormattedMessage.EscapeText(name))));
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Disconnected)
            RemovePendingForSession(e.Session);
    }

    private void RemovePendingForSession(ICommonSession session)
    {
        List<int>? stale = null;
        foreach (var (id, pending) in _pending)
        {
            if (pending.Session != session)
                continue;
            stale ??= new List<int>();
            stale.Add(id);
        }

        if (stale == null)
            return;

        foreach (var id in stale)
            _pending.Remove(id);
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        _photos.Clear();
        _photoNames.Clear();
        _photoShooters.Clear();
        _pending.Clear();
        _signPens.Clear();
        _nextCaptureId = 1;
        _nextPhotoId = 1;
    }
}
