namespace Content.Server._Polonium.Morph;

/// <summary>
/// Objective counting crew devoured by a morph, works together with a counter condition.
/// </summary>
[RegisterComponent, Access(typeof(MorphSystem))]
public sealed partial class MorphDevourConditionComponent : Component;
