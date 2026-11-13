namespace Content.Server._Polonium.TestChamber;

/// <summary>
/// Marks entities dropped from a test chamber kit, so the console knows what to clean up.
/// </summary>
[RegisterComponent]
public sealed partial class TestChamberSpawnedComponent : Component;
