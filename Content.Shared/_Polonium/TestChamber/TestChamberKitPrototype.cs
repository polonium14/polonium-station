using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.TestChamber;

/// <summary>
/// A set of entities the test chamber console drops next to itself, usually everything needed to check one PR in game.
/// Every PR gets its own file in Resources/Prototypes/_Polonium/TestChamber/Kits/ which is deleted once the PR is tested.
/// </summary>
/// <remarks>
/// Kits that need more than spawning (an antag role, reagents, a powered machine) can handle
/// <see cref="TestChamberKitSpawnedEvent"/> in their own system.
/// </remarks>
[Prototype]
public sealed partial class TestChamberKitPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Name shown in the console. Dev-only content, so plain text instead of a LocId.
    /// </summary>
    [DataField(required: true)]
    public string Name = string.Empty;

    /// <summary>
    /// The tested PR in full form, e.g. "polonium14/polonium-station#1125".
    /// </summary>
    [DataField]
    public string? Pr;

    /// <summary>
    /// What to check, shown in the console and sent to the chat after the kit is dropped.
    /// </summary>
    [DataField]
    public string? Description;

    /// <summary>
    /// Prototype => amount. Every prototype gets its own tile, further copies land on the same one.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<EntProtoId, int> Entities = new();
}
