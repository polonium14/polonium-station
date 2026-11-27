using Content.Server.Nutrition.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Smoking;

// Polonium: a partial of the upstream system, so its namespace follows the upstream one.
namespace Content.Server.Nutrition.EntitySystems;

public sealed partial class SmokingSystem
{
    /// <summary>
    /// Lights an unlit smokable the way a lighter does: a pipe first burns what is in its bowl, and without anything
    /// there it stays unlit.
    /// </summary>
    public void TryLight(Entity<SmokableComponent> ent)
    {
        if (ent.Comp.State != SmokableState.Unlit)
            return;

        if (TryComp<SmokingPipeComponent>(ent, out var pipe) && !TryTransferReagents((ent, pipe), ent))
            return;

        SetSmokableState(ent, SmokableState.Lit, ent);
    }
}
