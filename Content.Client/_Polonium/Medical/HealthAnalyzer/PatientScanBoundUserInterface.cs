using Content.Shared._Polonium.Medical.HealthAnalyzer;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Medical.HealthAnalyzer;

/// <summary>
/// The health analyzer, the MedTek cartridge and the body scanner console open this instead of the upstream window:
/// one view of the whole patient, filled from <see cref="HealthAnalyzerScanMessage"/>.
/// </summary>
[UsedImplicitly]
public sealed class PatientScanBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private PatientScanWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PatientScanWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (message is HealthAnalyzerScanMessage scan)
            _window?.Populate(scan);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // The window spawns a client-side doll entity.
        if (disposing)
            _window?.ReleaseDoll();
    }
}
