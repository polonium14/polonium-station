using Content.Shared.Medical.HealthAnalyzer;

namespace Content.Client.Medical.HealthAnalyzer;

/// <inheritdoc/>
public sealed partial class ClientHealthAnalyzerSystem : HealthAnalyzerSystem
{
    // Polonium: the Shitmed readout is built and sent by the server
    protected override void UpdateUi(Entity<HealthAnalyzerComponent> analyzer)
    {
    }
}
