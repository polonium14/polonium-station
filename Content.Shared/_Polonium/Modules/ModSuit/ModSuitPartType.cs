using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Modules.ModSuit;

[Serializable, NetSerializable]
public enum ModSuitPartType : byte
{
    Helmet,
    Chestplate,
    Control,
    Gauntlets,
    Boots,
    Other
}

public static class ModSuitPartTypeHelpers
{
    public static ModSuitPartType SlotToPart(string slot)
    {
        return slot switch
        {
            "head" => ModSuitPartType.Helmet,
            "outerClothing" => ModSuitPartType.Chestplate,
            "back" => ModSuitPartType.Control,
            "gloves" => ModSuitPartType.Gauntlets,
            "shoes" => ModSuitPartType.Boots,
            _ => ModSuitPartType.Other
        };
    }

    public static string PartToSlot(ModSuitPartType type)
    {
        return type switch
        {
            ModSuitPartType.Helmet => "head",
            ModSuitPartType.Chestplate => "outerClothing",
            ModSuitPartType.Control => "back",
            ModSuitPartType.Gauntlets => "gloves",
            ModSuitPartType.Boots => "shoes",
            _ => throw new NotImplementedException($"Tried to convert {nameof(type)} to an unimplemented slot.")
        };
    }
}
