using Content.Server.BaseAnalyzer;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Medical.Components;

/// <inheritdoc/>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(HealthAnalyzerSystem), typeof(CryoPodSystem))]
public sealed partial class HealthAnalyzerComponent : BaseAnalyzerComponent
{
    /// <summary>
    /// If the last state of the health analyzer was active (e.g. they are in range of the patient).
    /// </summary>
    [DataField]
    public bool IsAnalyzerActive = false;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public override TimeSpan NextUpdate { get; set; } = TimeSpan.Zero;
}
