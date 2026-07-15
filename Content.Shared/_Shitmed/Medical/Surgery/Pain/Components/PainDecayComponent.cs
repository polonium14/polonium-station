using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Medical.Surgery.Pain.Components;

// Tracks pain decay state for a nerve system
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PainDecayComponent : Component
{
    // The initial pain value when decay started
    [DataField, AutoNetworkedField]
    public FixedPoint2 InitialPain { get; set; }

    // The time when decay started
    [DataField, AutoNetworkedField]
    public TimeSpan StartTime { get; set; }

    // The duration it should take for pain to decay to zero
    [DataField, AutoNetworkedField]
    public TimeSpan DecayDuration { get; set; }

    // The nerve system this decay is associated with
    [DataField, AutoNetworkedField]
    public EntityUid NerveSystemUid { get; set; }
}
