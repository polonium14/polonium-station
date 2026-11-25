using Content.Shared._Polonium.Medical.HealthAnalyzer;
using Content.Shared.Medical.HealthAnalyzer;

namespace Content.Client._Polonium.Medical.HealthAnalyzer;

/// <summary>
/// The server builds and sends the patient scan, so the client does not set the upstream analyzer state.
/// </summary>
public sealed class ClientPatientScanSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HealthAnalyzerComponent, HealthAnalyzerUpdateUiEvent>(OnUpdateUi);
    }

    private void OnUpdateUi(Entity<HealthAnalyzerComponent> analyzer, ref HealthAnalyzerUpdateUiEvent args)
    {
        args.Handled = true;
    }
}
