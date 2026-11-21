using Content.Shared.Research.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Lathe;

/// <summary>
/// Tells clients which item a lathe is printing and how long it takes. Present only while the lathe prints.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, UnsavedComponent]
public sealed partial class LatheProgressComponent : Component
{
    [AutoNetworkedField]
    public ProtoId<LatheRecipePrototype> Recipe;

    [AutoNetworkedField]
    public TimeSpan StartTime;

    [AutoNetworkedField]
    public TimeSpan Length;
}
