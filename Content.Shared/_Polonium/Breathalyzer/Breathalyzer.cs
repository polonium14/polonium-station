using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Breathalyzer;

[Serializable, NetSerializable]
public enum BreathalyzerState : byte
{
    Idle,
    Blowing,
    Analyzing,
    Result,
}

[Serializable, NetSerializable]
public enum BreathalyzerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum BreathalyzerVisuals : byte
{
    Screen,
    Dirty,
}

[Serializable, NetSerializable]
public enum BreathalyzerVisualLayers : byte
{
    Screen,
    Dirty,
}

[Serializable, NetSerializable]
public sealed partial class BreathalyzerDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed class BreathalyzerPrintMessage : BoundUserInterfaceMessage;
