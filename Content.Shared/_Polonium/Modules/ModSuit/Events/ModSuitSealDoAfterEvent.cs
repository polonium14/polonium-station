using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Modules.ModSuit.Events;

/// <summary>
/// Raised on a R.I.G. part when another player finishes sealing or unsealing it on its wearer.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ModSuitSealDoAfterEvent : DoAfterEvent
{
    public bool ShouldSeal;

    public ModSuitSealDoAfterEvent(bool shouldSeal)
    {
        ShouldSeal = shouldSeal;
    }

    public override DoAfterEvent Clone() => this;
}
