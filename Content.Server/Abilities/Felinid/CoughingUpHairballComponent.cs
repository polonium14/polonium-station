namespace Content.Server.Abilities.Felinid;

[RegisterComponent]
public sealed partial class CoughingUpHairballComponent : Component
{
    [DataField]
    public float Accumulator;

    [DataField]
    public TimeSpan CoughUpTime = TimeSpan.FromSeconds(2.15); // length of hairball.ogg
}
