using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Watchers;

/// <summary>Fires when a puddle shows up near the trainee. Usually means they spilled something.</summary>
public sealed partial class PuddleNearbyWatcher : TutorialWatcher
{
    [DataField]
    public float Range = 4f;

    /// <summary>Only count puddles holding this reagent. Null means any puddle will do.</summary>
    [DataField]
    public ProtoId<ReagentPrototype>? Reagent;

    /// <summary>
    /// Ignore anything smaller than this. Some species dribble a unit per sip while drinking,
    /// which is nothing like tipping a whole mug onto the floor.
    /// </summary>
    [DataField]
    public float MinVolume;
}
