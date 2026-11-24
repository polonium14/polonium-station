using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Body;

/// <summary>
/// Marks a status effect under which the body breathes liquid, as with perfluorodecalin: it takes in no air from its
/// surroundings, and the reagent that gave the effect supplies the oxygen instead.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LiquidBreathingStatusEffectComponent : Component;
