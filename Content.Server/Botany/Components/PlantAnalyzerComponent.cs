// SPDX-FileCopyrightText: 2025 Mehnix <56132549+Mehnix@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.BaseAnalyzer;
using Content.Server.Botany.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Botany.Components;

/// <inheritdoc/>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(PlantAnalyzerSystem))]
public sealed partial class PlantAnalyzerComponent : BaseAnalyzerComponent
{
    /// <inheritdoc/>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public override TimeSpan NextUpdate { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// When will the analyzer be ready to print again?
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan PrintReadyAt = TimeSpan.Zero;

    /// <summary>
    /// How often can the analyzer print?
    /// </summary>
    [DataField]
    public TimeSpan PrintCooldown = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The sound that's played when the analyzer prints off a report.
    /// </summary>
    [DataField]
    public SoundSpecifier SoundPrint = new SoundPathSpecifier("/Audio/Machines/short_print_and_rip.ogg", AudioParams.Default.WithVariation(0.25f).WithVolume(3f).WithRolloffFactor(2.8f).WithMaxDistance(4.5f));

    /// <summary>
    /// What the machine will print.
    /// </summary>
    [DataField]
    public EntProtoId MachineOutput = "PlantAnalyzerReportPaper";
}
