using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Modules.ModSuit;

[Serializable, NetSerializable]
public enum ModSuitSealedVisuals : byte
{
    Sealed,
    Unpowered,

    /// <summary>
    /// Comma-separated overlay suffixes of the installed modules.
    /// </summary>
    Overlays,

    /// <summary>
    /// Whether a module draws itself in place of this part's own sprite.
    /// </summary>
    Hidden
}
