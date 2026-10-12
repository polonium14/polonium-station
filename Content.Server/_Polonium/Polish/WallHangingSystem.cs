using Content.Shared._Polonium.Polish;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Wall;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Polish;

/// <summary>
/// Wieszanie krzyży i godeł na ścianie oraz zdejmowanie ich ręką.
/// </summary>
public sealed partial class WallHangingSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";

    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Before, a nie After: inaczej interakcje samej ściany (konstrukcja) przechwytują kliknięcie.
        SubscribeLocalEvent<WallHangableComponent, BeforeRangedInteractEvent>(OnBeforeInteract);
        SubscribeLocalEvent<WallHangingComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<WallHangingComponent, ExaminedEvent>(OnExamined);
    }

    private void OnBeforeInteract(EntityUid uid, WallHangableComponent comp, BeforeRangedInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } wall || !IsWall(wall))
            return;

        args.Handled = true;

        var decor = Spawn(comp.Structure, Transform(wall).Coordinates);
        _popup.PopupEntity(Loc.GetString("wall-hanging-hung", ("item", decor)), args.User, args.User);
        QueueDel(uid);
    }

    private bool IsWall(EntityUid uid)
    {
        return HasComp<WallComponent>(uid) || _tag.HasTag(uid, WallTag);
    }

    private void OnInteractHand(Entity<WallHangingComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var item = Spawn(ent.Comp.Item, Transform(ent).Coordinates);
        _popup.PopupEntity(Loc.GetString("wall-hanging-taken-down", ("item", ent.Owner)), args.User, args.User);
        _hands.PickupOrDrop(args.User, item);
        QueueDel(ent);
    }

    private void OnExamined(Entity<WallHangingComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("wall-hanging-examine", ("item", ent.Owner)));
    }
}
