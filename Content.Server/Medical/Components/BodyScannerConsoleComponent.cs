
namespace Content.Server.Medical.Components;

/// <summary>
/// A console that shows live health-analyzer data for whoever is buckled to a bed it's
/// device-linked to (via the network configurator, same click-two-devices-together UX as every
/// other linkable machine). The console itself has no scanner - all the actual work of resolving
/// the linked bed's occupant and sending the patient scan (the same window the handheld health
/// analyzer opens, via <see cref="Content.Server._Polonium.Medical.HealthAnalyzer.PatientScanSystem.SendScan"/>) happens in
/// <see cref="BodyScannerConsoleSystem"/>, on a periodic refresh while the BUI is open.
/// </summary>
[RegisterComponent]
public sealed partial class BodyScannerConsoleComponent : Component
{
    /// <summary>
    /// The device-link source port this console sends its bed link from. Must match the sink
    /// port granted to operating tables/medical beds (see <c>computers.yml</c>'s
    /// <c>ComputerBodyScanner</c> and <c>operating_table.yml</c>/<c>beds.yml</c>).
    /// </summary>
    public const string LinkPort = "BodyScannerSender";

    /// <summary>
    /// How often the linked bed is re-polled and the UI refreshed while open. Mirrors
    /// CryoPodComponent's UiUpdateInterval.
    /// </summary>
    [DataField]
    public TimeSpan UiUpdateInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The timestamp for the next UI update. Not networked - this is server-only bookkeeping for
    /// <see cref="BodyScannerConsoleSystem.Update"/>, nothing about it needs to reach the client.
    /// </summary>
    [DataField]
    public TimeSpan NextUiUpdateTime = TimeSpan.Zero;
}
