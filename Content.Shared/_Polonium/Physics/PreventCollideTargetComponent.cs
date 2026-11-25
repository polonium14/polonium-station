namespace Content.Shared._Polonium.Physics;

[RegisterComponent, UnsavedComponent, Access(typeof(PreventCollideTargetSystem))]
public sealed partial class PreventCollideTargetComponent : Component
{
    [ViewVariables]
    public HashSet<EntityUid> Colliders = new();
}
