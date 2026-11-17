using System.Numerics;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Shared._Shitmed.Targeting;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Polonium.UserInterface.Controls;

/// <summary>
/// The body doll shown in a <see cref="Robust.Client.UserInterface.Controls.SpriteView"/>: one client-side sprite
/// with a status layer per limb and a bleeding layer on top of each, kept between updates.
/// </summary>
public sealed class LimbDoll(IEntityManager entities)
{
    public static readonly TargetBodyPart[] Limbs =
    [
        TargetBodyPart.Head, TargetBodyPart.Chest, TargetBodyPart.LeftArm,
        TargetBodyPart.RightArm, TargetBodyPart.LeftLeg, TargetBodyPart.RightLeg,
    ];

    private EntityUid _entity;

    /// <summary>
    /// Sprite layer of each limb's status, then of each limb's bleeding, in <see cref="Limbs"/> order.
    /// </summary>
    private readonly int[] _layers = new int[Limbs.Length * 2];

    /// <summary>
    /// The sprite entity to hand to the view, spawned on first use.
    /// </summary>
    public EntityUid Entity
    {
        get
        {
            if (entities.EntityExists(_entity))
                return _entity;

            _entity = entities.Spawn("AlertSpriteView");
            if (!entities.TryGetComponent<SpriteComponent>(_entity, out var sprite))
                return _entity;

            // The view prototype may come with layers of its own, so remember where ours land.
            var sprites = entities.System<SpriteSystem>();
            for (var i = 0; i < _layers.Length; i++)
            {
                _layers[i] = sprites.AddTextureLayer((_entity, sprite), (Texture?) null);
                sprites.LayerSetScale((_entity, sprite), _layers[i], new Vector2(3f, 3f));
            }

            return _entity;
        }
    }

    /// <summary>
    /// Draws one limb at a damage severity of 0 to 6, or hides it when the severity is null.
    /// </summary>
    public void Set(TargetBodyPart part, int? severity, bool bleeding)
    {
        var index = Array.IndexOf(Limbs, part);
        if (index < 0 || !entities.TryGetComponent<SpriteComponent>(Entity, out var sprite))
            return;

        var sprites = entities.System<SpriteSystem>();
        var doll = new Entity<SpriteComponent?>(_entity, sprite);
        var name = part.ToString().ToLowerInvariant();
        var rsi = new ResPath($"/Textures/_Shitmed/Interface/Targeting/Status/{name}.rsi");

        sprites.LayerSetTexture(doll, _layers[index], sprites.Frame0(new SpriteSpecifier.Rsi(rsi, $"{name}_{severity ?? 0}")));
        sprites.LayerSetVisible(doll, _layers[index], severity != null);
        sprites.LayerSetTexture(doll, _layers[Limbs.Length + index], sprites.Frame0(new SpriteSpecifier.Rsi(rsi, $"{name}_bleed")));
        sprites.LayerSetVisible(doll, _layers[Limbs.Length + index], bleeding);
    }

    /// <summary>
    /// A limb's name as a heading: entity names are lower case.
    /// </summary>
    public static string Capitalize(string name)
    {
        return name.Length == 0 ? name : string.Concat(name[..1].ToUpperInvariant(), name[1..]);
    }

    /// <summary>
    /// Gives the button of the picked limb the outline the others only get under the cursor.
    /// </summary>
    public static void Outline(TextureButton button, TargetBodyPart part, bool picked)
    {
        var name = part == TargetBodyPart.Chest ? "torso" : part.ToString().ToLowerInvariant();
        button.TextureNormal = picked
            ? IoCManager.Resolve<IResourceCache>().GetTexture($"/Textures/_Shitmed/Interface/Targeting/Doll/{name}_hover.png")
            : null;
        button.ModulateSelfOverride = picked ? StyleNano.NanoGold : null;
    }

    /// <summary>
    /// Deletes the sprite entity when its window goes away.
    /// </summary>
    public void Release()
    {
        if (entities.EntityExists(_entity))
            entities.DeleteEntity(_entity);
    }
}
