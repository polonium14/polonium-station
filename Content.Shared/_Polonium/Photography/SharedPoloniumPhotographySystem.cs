using Content.Shared.Charges.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Content.Shared.Verbs;
using Robust.Shared.Map;

namespace Content.Shared._Polonium.Photography;

/// <summary>
/// Shared half of the pixel-photography feature. A picture is a plain ranged interaction
/// (left-click) so it works regardless of flash; the flash is a separate
/// <see cref="ItemToggleComponent"/> layer toggled from a right-click verb. Actual capture is
/// a server-only override that issues a token to the shutter-presser's client.
/// </summary>
public abstract partial class SharedPoloniumPhotographySystem : EntitySystem
{
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private InventorySystem _inventory = default!;

    /// <summary>The inventory slot that must be empty to use the camera (its viewfinder).</summary>
    public const string EyeSlot = "eyes";

    /// <summary>Whether something is worn in the eye slot (glasses/HUD), blocking the camera.</summary>
    public bool EyesCovered(EntityUid user)
    {
        return _inventory.TryGetSlotEntity(user, EyeSlot, out _);
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PoloniumCameraComponent, BeforeRangedInteractEvent>(OnRangedInteract);
        SubscribeLocalEvent<PoloniumCameraComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<PoloniumCameraComponent, ExaminedEvent>(OnExamined);
    }

    private void OnRangedInteract(Entity<PoloniumCameraComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (args.Handled)
            return;

        // Anything worn over the eyes (glasses / HUD) blocks the shot, keeping HUD overlays out of it (hack).
        if (EyesCovered(args.User))
        {
            _popup.PopupClient(Loc.GetString("camera-eyes-covered"), ent.Owner, args.User);
            return;
        }

        var targetCoords = args.Target is { } target && !TerminatingOrDeleted(target)
            ? Transform(target).Coordinates
            : args.ClickLocation;

        // Require an unoccluded sightline via vision occlusion (opaque walls / closed doors block),
        // not physical collision, so glass and open doors let the shot through as the player sees it.
        // Gate before the throttle so a blocked click isn't punished with cooldown.
        if (!_examine.InRangeUnOccluded(args.User, targetCoords, range: PhotographyConstants.PhotoMaxRange))
        {
            _popup.PopupClient(Loc.GetString("camera-no-line-of-sight"), ent.Owner, args.User);
            return;
        }

        // Out of film? Gate before the throttle so an empty click isn't punished with cooldown.
        if (_charges.IsEmpty(ent.Owner))
        {
            _popup.PopupClient(Loc.GetString("camera-no-film"), ent.Owner, args.User);
            return;
        }

        // Shutter throttle; if still cooling down, let the interaction fall through.
        if (!_useDelay.TryResetDelay(ent.Owner, checkDelayed: true))
            return;

        // Swallow the interaction only if a capture started, so a non-player holder (no session) doesn't dead-end normal interactions.
        if (StartCapture(ent, args.User, targetCoords, args.Target))
        {
            _charges.TryUseCharge(ent.Owner);
            args.Handled = true;
        }
    }

    private void OnGetAltVerbs(Entity<PoloniumCameraComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        AlternativeVerb verb = new()
        {
            Act = () => _toggle.Toggle(ent.Owner, user),
            Text = Loc.GetString("camera-toggle-flash-verb"),
            Priority = 10,
        };
        args.Verbs.Add(verb);
    }

    private void OnExamined(Entity<PoloniumCameraComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var on = _toggle.IsActivated(ent.Owner);
        args.PushMarkup(Loc.GetString(on ? "camera-flash-on" : "camera-flash-off"));
    }

    /// <summary>
    /// Begin a capture for <paramref name="user"/>, framed on <paramref name="targetCoords"/>
    /// (resolved at click time - the photo freezes the instant it was taken). <paramref name="target"/>
    /// is the clicked entity, if any, so the server can name the subject. Returns true if a capture
    /// started. Server-only; the shared base does nothing so the client never originates a capture.
    /// </summary>
    protected virtual bool StartCapture(Entity<PoloniumCameraComponent> camera, EntityUid user, EntityCoordinates targetCoords, EntityUid? target)
    {
        return false;
    }
}
