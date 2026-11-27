using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Medical.Surgery;

/// <summary>
/// Afflicted entity will occasionally botch a surgery step, whatever the tool and the conditions.
/// Use only on a status effect entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ClumsySurgeryStatusEffectComponent : Component
{
    /// <summary>
    /// How often to fail.
    /// </summary>
    [DataField]
    public float ClumsyChance = 0.5f;

    /// <summary>
    /// Sound played upon failure.
    /// </summary>
    [DataField]
    public SoundSpecifier? ClumsySound;
}
