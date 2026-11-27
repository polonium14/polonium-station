using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Whether humanoids swing their arms and legs while walking. Each walking humanoid costs an extra render pass.
    /// </summary>
    public static readonly CVarDef<bool> WalkAnimationEnabled =
        CVarDef.Create("walk_animation.enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
