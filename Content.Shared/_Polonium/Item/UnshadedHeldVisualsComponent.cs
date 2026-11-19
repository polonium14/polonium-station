namespace Content.Shared._Polonium.Item;

/// <summary>
/// Marks an item whose in-hand and worn sprite states have "-unshaded" twins in the same RSI.
/// The client draws each twin over its state without lighting, so that the item is always visible in dark areas.
/// </summary>
[RegisterComponent]
public sealed partial class UnshadedHeldVisualsComponent : Component;
