using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.MaskHailer;

/// <summary>
/// Lets the wearer of a mask play one of its recorded voice commands through an action.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(MaskHailerSystem))]
public sealed partial class MaskHailerComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<MaskHailerSetPrototype> Set;

    [DataField]
    public EntProtoId Action = "ActionMaskHailer";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    /// <summary>
    /// Pause shared by all phrases of the mask.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(4);
}
