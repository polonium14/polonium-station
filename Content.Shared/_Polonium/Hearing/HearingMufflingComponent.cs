using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Hearing;

/// <summary>
/// Clothing that, while switched on (<see cref="Content.Shared.Item.ItemToggle.Components.ItemToggleComponent"/>),
/// cuts its wearer off from the world: speech around them can't be made out and every sound in the world gets quiet.
/// Radio text, emotes, interface sounds and global music still get through.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HearingMufflingComponent : Component
{
    /// <summary>
    /// Slots the item has to be worn in to muffle anything. Headphones around the neck do nothing.
    /// </summary>
    [DataField]
    public SlotFlags Slots = SlotFlags.HEAD | SlotFlags.EARS;

    /// <summary>
    /// Decibels added to every sound in the world while muffled. -20 dB is about a tenth of the volume.
    /// </summary>
    [DataField]
    public float VolumeModifier = -20f;

    [DataField]
    public LocId ExamineText = "hearing-muffling-examine";
}
