// SPDX-FileCopyrightText: 2025 August Eymann <august.eymann@gmail.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 gluesniffler <linebarrelerenthusiast@gmail.com>
// SPDX-FileCopyrightText: 2025 Polonium Space <admin@ss14.pl>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Alert;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Movement.Sprinting;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SprinterComponent : Component
{
    /// <summary>
    ///     Is the entity currently sprinting?
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool IsSprinting = false;

    /// <summary>
    ///     Current sprint capacity of the entity.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public float CurrentSprintCapacity;

    /// <summary>
    ///     How long can the entity sprint for?
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables]
    public float SprintCapacity = 1f;

    /// <summary>
    ///     Can the entity sprint?
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public bool CanSprint = true;

    /// <summary>
    ///     How much sprint bar is drained per update cycle?
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables]
    public float SprintCapacityDrainRate = 0.01f;

    /// <summary>
    ///     How much do we multiply stamina drains while there's a StaminaModifierComponent?
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float SprintCapacityDrainMultiplier = 6f;

    /// <summary>
    ///     How much sprint bar is regenerated per update cycle?
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables]
    public float SprintCapacityRegenRate = 0.01f;

    /// <summary>
    ///     How much do we multiply stamina regeneration while there's a StaminaModifierComponent?
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float SprintCapacityRegenMultiplier = 4f;

    /// <summary>
    ///     How much do we multiply sprint speed?
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables]
    public float SprintSpeedMultiplier = 1.35f;

    /// <summary>
    ///     How often the component updates its state.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public TimeSpan UpdateRate = TimeSpan.FromSeconds(0.33f);

    /// <summary>
    ///     When did we last sprint?
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public TimeSpan LastSprint = TimeSpan.Zero;

    /// <summary>
    ///     How many seconds to wait after stamina depletes before regeneration starts.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables]
    public float DelaySecondsAfterDeplete = 3f;

    /// <summary>
    ///     When did we last deplete our sprint?
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public TimeSpan LastDepleted = TimeSpan.Zero;

    /// <summary>
    ///     Speed modifier applied when stamina is depleted.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float DepletedSpeedModifier = 0.65f;

    /// <summary>
    ///     The threshold above which sprinting is allowed again.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables]
    public float SprintThreshold = 0.2f;

    /// <summary>
    ///     What string do we use to tag stamina drain?
    /// </summary>
    [DataField]
    public string StaminaDrainKey = "sprint";

    /// <summary>
    ///     What entity do we use for sprinting visuals?
    /// </summary>
    [DataField]
    public EntProtoId SprintAnimation = "SprintAnimation";

    /// <summary>
    ///     When did we last step?
    /// </summary>
    [ViewVariables]
    public TimeSpan LastStep = TimeSpan.Zero;

    /// <summary>
    ///     When is the next update tick for this component.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, ViewVariables]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    /// <summary>
    ///     What entity do we use for stepping visuals?
    /// </summary>
    [DataField]
    public EntProtoId StepAnimation = "SmallSprintAnimation";

    /// <summary>
    ///     What sound do we play when we start sprinting?
    /// </summary>
    [DataField]
    public SoundSpecifier SprintStartupSound = new SoundPathSpecifier("/Audio/_Goobstation/Effects/Sprinting/sprint_puff.ogg");

    /// <summary>
    ///     What sound do we play when stamina is exhausted?
    /// </summary>
    [DataField]
    public SoundSpecifier ExhaustedSound = new SoundPathSpecifier("/Audio/_Polonium/Effects/Sprinting/sprintExhausted.ogg");

    /// <summary>
    ///     Alert to show when sprinting.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> SprintAlert = "Sprint";

    /// <summary>
    ///     How long do we have to wait between spawning step visuals?
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables]
    public TimeSpan TimeBetweenSteps = TimeSpan.FromSeconds(0.3);
}

[Serializable, NetSerializable]
public sealed class SprintToggleEvent(bool isSprinting) : EntityEventArgs
{
    public bool IsSprinting = isSprinting;
}

[Serializable, NetSerializable]
public sealed class SprintStartEvent : EntityEventArgs;

[Serializable, NetSerializable]
public record struct SprintCapacityDepletedEvent;

[Serializable, NetSerializable]
public record struct SprintCapacityRecoveredEvent;

[ByRefEvent]
public sealed class SprintAttemptEvent : CancellableEntityEventArgs;
