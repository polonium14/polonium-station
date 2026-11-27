using Content.Shared.Roles.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Horror.Components;

/// <summary>
/// Added to mind role entities to tag that they are an eldritch horror.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HorrorRoleComponent : BaseMindRoleComponent;
