using Content.Shared.Kitchen.Components;

namespace Content.Shared.Kitchen.EntitySystems;

public abstract partial class SharedReagentGrinderSystem
{
    /// <summary>
    /// Puts the automatic mode of the grinder into the given position. Asking for the position it is already in changes nothing.
    /// </summary>
    public void SetAutoMode(Entity<ReagentGrinderComponent?> ent, GrinderAutoMode mode)
    {
        if (!Resolve(ent, ref ent.Comp) || !Enum.IsDefined(mode) || ent.Comp.AutoMode == mode)
            return;

        ent.Comp.AutoMode = mode;
        Dirty(ent);
        UpdateUi(ent.Owner);
    }
}
