using Content.Server._Shitmed.PartStatus; // Polonium
using Content.Shared.Body; // Polonium
using Content.Shared.Medical.HealthAnalyzer;
using Content.Shared.MedicalScanner;
using Content.Shared.Temperature.Components;

namespace Content.Server.Medical.HealthAnalyzer;

/// <inheritdoc/>
public sealed partial class ServerHealthAnalyzerSystem : HealthAnalyzerSystem
{
    [Dependency] private EntityQuery<TemperatureComponent> _temperatureQuery;
    [Dependency] private PartStatusSystem _partStatus = default!; // Polonium

    public override HealthAnalyzerUiState GetHealthAnalyzerUiState(EntityUid? target, bool scanMode)
    {
        var state = base.GetHealthAnalyzerUiState(target, scanMode);

        if (_temperatureQuery.TryComp(target, out var temp))
            state.Temperature = temp.Temperature;

        // Polonium: Shitmed part statuses for the cryo pod readout
        if (target is { } entity && TryComp<BodyComponent>(entity, out var bodyComp) && bodyComp.Organs is not null)
            state.PartStatuses = _partStatus.GetPartStatusDescriptions(entity);

        return state;
    }
}
