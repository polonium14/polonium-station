using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Abductors;

public sealed partial class AbductorRecallActionEvent : InstantActionEvent;

public sealed partial class AbductorSendPadActionEvent : InstantActionEvent;

public sealed partial class AbductorSendSelfActionEvent : InstantActionEvent;

public sealed partial class AbductorRetrieveActionEvent : InstantActionEvent;

public sealed partial class AbductorSetReleaseActionEvent : InstantActionEvent;

public sealed partial class AbductorExitObservationActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class AbductorRecallDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class AbductorCuffDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class AbductorMarkDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class AbductorExperimentDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Raised on an abductor who has just put a tracker on a specimen.
/// </summary>
[ByRefEvent]
public readonly record struct AbductorSpecimenMarkedEvent(EntityUid Specimen);

/// <summary>
/// Raised on the abductor who bought the cow from the ship's console.
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class AbductorCowDeliveryEvent : EntityEventArgs;

[Serializable, NetSerializable]
public enum AbductorChamberVisuals : byte
{
    Occupied,
}
