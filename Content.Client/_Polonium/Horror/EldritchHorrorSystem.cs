using Content.Client.Alerts;
using Content.Shared._Polonium.Horror;
using Content.Shared._Polonium.Horror.Components;
using Content.Shared.Alert.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._Polonium.Horror;

public sealed partial class EldritchHorrorSystem : SharedEldritchHorrorSystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IEyeManager _eye = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EldritchHorrorComponent, GetGenericAlertCounterAmountEvent>(OnGetCounterAmount);
        SubscribeLocalEvent<EldritchHorrorComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<HorrorSoulLocatorAlertComponent, UpdateAlertSpriteEvent>(OnUpdateLocator);
    }

    private void OnGetCounterAmount(Entity<EldritchHorrorComponent> ent, ref GetGenericAlertCounterAmountEvent args)
    {
        if (args.Handled || args.Alert.ID != ent.Comp.ChemicalAlert)
            return;

        args.Amount = ent.Comp.Chemicals;
    }

    /// <summary>
    /// A hiding horror lies low, drawn under items and tables.
    /// </summary>
    private void OnHandleState(Entity<EldritchHorrorComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.SetDrawDepth((ent, sprite), (int) (ent.Comp.Hiding ? Content.Shared.DrawDepth.DrawDepth.FloorObjects : Content.Shared.DrawDepth.DrawDepth.SmallMobs));
    }

    /// <summary>
    /// Turns the locator arrow towards the hunted soul, relative to the eye like a pinpointer.
    /// </summary>
    private void OnUpdateLocator(Entity<HorrorSoulLocatorAlertComponent> ent, ref UpdateAlertSpriteEvent args)
    {
        // while the horror drives its host, the player looks through the host's eyes
        if (!TryComp<EldritchHorrorComponent>(args.ViewerEnt, out var horror)
            && !(TryComp<EldritchHorrorInfestedComponent>(args.ViewerEnt, out var infested)
                 && TryComp(infested.Horror, out horror)))
            return;

        var state = horror.SoulDistance switch
        {
            HorrorSoulDistance.Reached => "pinondirect",
            HorrorSoulDistance.Close => "pinonclose",
            HorrorSoulDistance.Medium => "pinonmedium",
            HorrorSoulDistance.Far => "pinonfar",
            _ => "pinonnull",
        };

        var rotate = horror.SoulDistance is HorrorSoulDistance.Close or HorrorSoulDistance.Medium or HorrorSoulDistance.Far;
        _sprite.LayerSetRsiState(args.SpriteViewEnt.AsNullable(), HorrorLocatorLayers.Arrow, state);
        _sprite.LayerSetRotation(args.SpriteViewEnt.AsNullable(), HorrorLocatorLayers.Arrow,
            rotate ? horror.SoulArrow + _eye.CurrentEye.Rotation : Angle.Zero);
    }
}
