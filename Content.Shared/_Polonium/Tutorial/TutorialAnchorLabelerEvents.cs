using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Tutorial;

[Serializable, NetSerializable]
public enum TutorialAnchorLabelerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class TutorialAnchorLabelerAnchorChangedMessage(string anchor) : BoundUserInterfaceMessage
{
    public string Anchor { get; } = anchor;
}
