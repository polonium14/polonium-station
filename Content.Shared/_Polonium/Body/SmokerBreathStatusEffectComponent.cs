using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Body;

/// <summary>
/// The body has smoked a moment ago: its oxygen saturation reads a little lower.
/// Use only on a status effect entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SmokerBreathStatusEffectComponent : Component;
