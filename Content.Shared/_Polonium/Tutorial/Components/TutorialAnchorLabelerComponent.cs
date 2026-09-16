using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Tutorial.Components;

/// <summary>
/// Debug glue gun for TutorialAnchor. Click an ent to stamp the typed id, blank to peel it off.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class TutorialAnchorLabelerComponent : Component
{
    [DataField, AutoNetworkedField]
    public string AssignedAnchor = string.Empty;

    [DataField, AutoNetworkedField]
    public int MaxAnchorChars = 64;
}
