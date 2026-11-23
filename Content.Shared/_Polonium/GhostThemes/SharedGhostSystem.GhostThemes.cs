// ReSharper disable CheckNamespace
using Content.Shared.Ghost.Components;

namespace Content.Shared.Ghost.Systems;

// Polonium: ghost themes recolor the default ghost through its own color.
public abstract partial class SharedGhostSystem
{
    /// <summary>
    /// Sets the color of the default ghost sprite.
    /// </summary>
    public void SetColor(Entity<GhostComponent?> entity, Color color)
    {
        if (!Resolve(entity, ref entity.Comp))
            return;

        if (entity.Comp.Color == color)
            return;

        entity.Comp.Color = color;
        Dirty(entity);
    }
}
