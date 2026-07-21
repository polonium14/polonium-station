namespace Content.Client._Shitmed.Medical.Surgery;

/// <summary>
/// Minimal concrete subclass so SharedSurgerySystem actually gets instantiated by Robust's
/// EntitySystem loader (abstract systems are never auto-registered). No client-specific
/// overrides needed yet — the surgery BUI/window (Goob's SurgeryBui.cs/SurgeryWindow.xaml)
/// is a separate, not-yet-started phase.
/// </summary>
public sealed class SurgerySystem : Content.Shared._Shitmed.Medical.Surgery.SharedSurgerySystem;
