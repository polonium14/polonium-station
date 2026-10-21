using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Hearing;

/// <summary>
/// Clothing that, while switched on (<see cref="Content.Shared.Item.ItemToggle.Components.ItemToggleComponent"/>),
/// stops its wearer from hearing what people around them say. Radio and emotes still get through.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SpeechMufflingComponent : Component
{
    /// <summary>
    /// Slots the item has to be worn in to muffle speech. Headphones around the neck do nothing.
    /// </summary>
    [DataField]
    public SlotFlags Slots = SlotFlags.HEAD | SlotFlags.EARS;

    [DataField]
    public LocId ExamineText = "speech-muffling-examine";
}
