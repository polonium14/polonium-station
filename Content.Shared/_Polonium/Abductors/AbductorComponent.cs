using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Abductors;

/// <summary>
/// A crew member of an abductor ship.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AbductorComponent : Component
{
    [DataField(required: true)]
    public ProtoId<AbductorCuratorLinePrototype> ArrivalLine;

    [DataField]
    public TimeSpan RecallDelay = TimeSpan.FromSeconds(8);

    [DataField]
    public EntProtoId RecallAction = "ActionAbductorRecall";

    [DataField, AutoNetworkedField]
    public EntityUid? RecallActionEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? Ship;

    /// <summary>
    /// The light shown around the abductor during a recall, so the crew can see it coming.
    /// </summary>
    [ViewVariables]
    public EntityUid? RecallEffect;
}
