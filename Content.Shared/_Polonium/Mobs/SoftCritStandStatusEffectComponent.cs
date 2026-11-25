using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Mobs;

/// <summary>
/// A mob in soft crit can stand up and walk while it has this effect (epinephrine in the blood).
/// Use only on a status effect entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SoftCritStandStatusEffectComponent : Component;
