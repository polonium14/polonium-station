namespace Content.Shared._Polonium.Body;

/// <summary>
/// Marks an organ as a prosthesis. It has no behaviour of its own; things like patient requests check for it.
/// </summary>
[RegisterComponent]
public sealed partial class ProstheticComponent : Component;
