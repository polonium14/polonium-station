using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Horror.Components;

/// <summary>
/// Surgery step that pulls an eldritch horror out of the patient's brain.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HorrorRemovalStepComponent : Component;
