namespace Content.Shared.Mobs.Components;

[RegisterComponent]
public sealed partial class FixtureRadiusOnDeathComponent : Component
{
    [DataField]
    public float Radius = 0.35f;

    [ViewVariables]
    public Dictionary<string, float> OriginialRadius = new();
}
