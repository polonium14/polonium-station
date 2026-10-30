namespace Content.Shared._Polonium.Modules.Events;

[ByRefEvent]
public struct ModuleContainerGetBasePowerDrawRate(float baseRate)
{
    public readonly float OriginalBaseRate = baseRate;
    public float BaseRate = baseRate;
}
