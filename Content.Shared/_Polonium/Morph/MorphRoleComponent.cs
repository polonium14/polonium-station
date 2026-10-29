using Content.Shared.Roles.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Morph;

/// <summary>
/// Added to mind role entities to tag that they are a morph.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MorphRoleComponent : BaseMindRoleComponent;
