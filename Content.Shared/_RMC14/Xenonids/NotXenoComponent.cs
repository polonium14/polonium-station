namespace Content.Shared.Mobs.Components;

[RegisterComponent]
public sealed partial class NotXenoComponent : Component
{
    /// mostly empty component to split xenos from all other mobs.
    [DataField]
    public bool IsNotXeno = true;
}
