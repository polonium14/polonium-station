using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Medical.BedInternals;

/// <summary>
/// A bed (or table) that can feed a gas tank in one of its item slots to whoever is buckled to it.
/// The tank is connected to the patient's <see cref="Content.Shared.Body.Components.InternalsComponent"/>, through the
/// patient's own breath mask or the one the bed keeps in <see cref="MaskContainer"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BedInternalsSystem))]
public sealed partial class BedInternalsComponent : Component
{
    /// <summary>
    /// ID of the <see cref="Content.Shared.Containers.ItemSlots.ItemSlotsComponent"/> slot that holds the gas tank.
    /// </summary>
    [DataField(required: true)]
    public string GasSlot = default!;

    /// <summary>
    /// ID of the locked item slot where the bed keeps its breath mask while nobody wears it.
    /// </summary>
    [DataField(required: true)]
    public string MaskContainer = default!;

    /// <summary>
    /// Whether the bed currently supplies its tank to the buckled patients.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField]
    public SoundSpecifier ToggleSound = new SoundPathSpecifier("/Audio/Effects/internals.ogg");
}
