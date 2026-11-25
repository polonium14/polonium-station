namespace Content.Shared._Polonium.Medical.HealthAnalyzer;

/// <summary>
/// Raised on a health analyzer when its window should refresh. Handled means the patient scan was sent instead of
/// the upstream analyzer state.
/// </summary>
[ByRefEvent]
public record struct HealthAnalyzerUpdateUiEvent(bool Handled = false);
