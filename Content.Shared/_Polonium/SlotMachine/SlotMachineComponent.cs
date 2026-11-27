// Ported from Goob-Station (https://github.com/Goob-Station/Goob-Station/pull/4929), AGPL-3.0-or-later.

using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.SlotMachine;

/// <summary>
/// A machine that takes spesos from its money slot and, after spinning, pays out a weighted random prize.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SlotMachineSystem))]
public sealed partial class SlotMachineComponent : Component
{
    /// <summary>
    /// How many spesos one spin takes from the money slot.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int SpinCost = 250;

    /// <summary>
    /// The item slot that holds the inserted cash.
    /// </summary>
    [DataField]
    public string MoneySlot = "money";

    /// <summary>
    /// Prizes picked from by weight when a spin ends.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<SlotMachinePrizePrototype>> Prizes = new();

    /// <summary>
    /// How long a spin takes before the prize is paid out.
    /// </summary>
    [DataField]
    public TimeSpan SpinDuration = TimeSpan.FromSeconds(3.8);

    /// <summary>
    /// Played when a spin starts.
    /// </summary>
    [DataField]
    public SoundSpecifier SpinSound = new SoundPathSpecifier("/Audio/_Polonium/Machines/SlotMachine/slotmachine_spin.ogg");

    /// <summary>
    /// Whether a spin is in progress. Blocks starting another one.
    /// </summary>
    [AutoNetworkedField]
    public bool IsSpinning;

    /// <summary>
    /// Who started the current spin, for the admin log.
    /// </summary>
    [ViewVariables]
    public EntityUid? Spinner;
}

[Serializable, NetSerializable]
public enum SlotMachineVisuals : byte
{
    Spinning,
}

[Serializable, NetSerializable]
public sealed partial class SlotMachineDoAfterEvent : SimpleDoAfterEvent;
