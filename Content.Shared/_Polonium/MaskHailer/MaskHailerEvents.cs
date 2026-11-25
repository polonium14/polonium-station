using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.MaskHailer;

public sealed partial class MaskHailerActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed class MaskHailerPhraseMessage(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}

[Serializable, NetSerializable]
public enum MaskHailerUiKey : byte
{
    Key,
}
