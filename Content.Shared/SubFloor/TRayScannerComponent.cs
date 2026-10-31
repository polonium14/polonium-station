using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.SubFloor;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TRayScannerComponent : Component
{
    /// <summary>
    ///     Current mode of operation, defines which subfloor entities are shown.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TrayScannerMode Mode = TrayScannerMode.All;

    /// <summary>
    ///     Radius in which the scanner will reveal entities. Centered on the <see cref="LastLocation"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = 4f;

    // Polonium start
    /// <summary>
    ///     Whether the scanner only works while its item toggle is activated.
    ///     Disabled for scanners added to items whose toggle drives something else, like the R.I.G. helmet light.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool RequireToggle = true;
    // Polonium end

    [DataField]
    public SoundSpecifier SoundSwitchMode = new SoundPathSpecifier("/Audio/Machines/quickbeep.ogg");
}

[Serializable, NetSerializable]
public enum TrayScannerMode
{
    All,
    Piping,
    Wiring
}
