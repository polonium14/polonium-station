using Content.Shared.Research.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Lathe;

/// <summary>
/// Tells clients which item a lathe is printing, how long it takes and which item last came out.
/// Added with the first print and kept afterwards.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, UnsavedComponent]
public sealed partial class LatheProgressComponent : Component
{
    /// <summary>
    /// The item in work, or null while the lathe stands still.
    /// </summary>
    [AutoNetworkedField]
    public ProtoId<LatheRecipePrototype>? Recipe;

    [AutoNetworkedField]
    public TimeSpan StartTime;

    [AutoNetworkedField]
    public TimeSpan Length;

    /// <summary>
    /// When the last item that came out of the lathe began to print. An item cut short never gets here.
    /// </summary>
    [AutoNetworkedField]
    public TimeSpan Finished;
}
