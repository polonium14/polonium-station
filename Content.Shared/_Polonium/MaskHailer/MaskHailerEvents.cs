using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.MaskHailer;

public sealed partial class MaskHailerActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed class MaskHailerPhraseMessage(int index, int variant) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
    public readonly int Variant = variant;
}

[Serializable, NetSerializable]
public enum MaskHailerUiKey : byte
{
    Key,
}
