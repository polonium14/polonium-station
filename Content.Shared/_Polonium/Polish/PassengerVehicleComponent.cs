using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Polish;

/// <summary>
/// Pojazd z <c>ContainerVehicle</c>, do którego wsiada kilka osób: pierwsza prowadzi, reszta jedzie jako pasażerowie.
/// Pasażer wysiada, próbując się ruszyć albo przez czasownik. Silnik warczy podczas jazdy.
/// Port <c>/obj/vehicle/sealed/car</c> z NSV13 (Maluch).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PassengerVehicleComponent : Component
{
    /// <summary>
    /// Kontener z kierowcą i pasażerami (ten sam co w <c>ContainerVehicle</c>).
    /// </summary>
    [DataField(required: true)]
    public string ContainerId = string.Empty;

    /// <summary>
    /// Ile osób mieści pojazd razem z kierowcą.
    /// </summary>
    [DataField]
    public int MaxOccupants = 5;

    /// <summary>
    /// Dźwięk silnika odtwarzany podczas jazdy.
    /// </summary>
    [DataField]
    public SoundSpecifier? EngineSound;

    /// <summary>
    /// Długość dźwięku silnika - wcześniej się go nie powtarza.
    /// </summary>
    [DataField]
    public TimeSpan EngineSoundLength = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public TimeSpan NextEngineSound;
}
