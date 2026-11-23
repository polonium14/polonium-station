using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;

/// <summary>
/// The bleeders of the part's open incision are clamped, so it only seeps. Separate from
/// <see cref="BleedersClampedComponent"/>, which marks clamped wounds and comes off when those are stitched.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class IncisionClampedComponent : Component;
