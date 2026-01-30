// SPDX-FileCopyrightText: 2022 Fishfish458 <47410468+Fishfish458@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Rane <60792108+Elijahrane@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 fishfish458 <fishfish458>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <drsmugleaf@gmail.com>
// SPDX-FileCopyrightText: 2023 Kara <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2023 keronshb <54602815+keronshb@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 nmajask <nmajask@gmail.com>
// SPDX-FileCopyrightText: 2024 Fildrance <fildrance@gmail.com>
// SPDX-FileCopyrightText: 2024 Milon <plmilonpl@gmail.com>
// SPDX-FileCopyrightText: 2024 PJBot <pieterjan.briers+bot@gmail.com>
// SPDX-FileCopyrightText: 2024 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2024 Rainfey <rainfey0+github@gmail.com>
// SPDX-FileCopyrightText: 2024 Tadeo <td12233a@gmail.com>
// SPDX-FileCopyrightText: 2024 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Mehnix <56132549+Mehnix@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Minerva <218184747+mnva0@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Tay <td12233a@gmail.com>
// SPDX-FileCopyrightText: 2025 mnva <218184747+mnva0@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 pa.pecherskij <pa.pecherskij@interfax.ru>
// SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.Audio;
using Content.Server.BaseAnalyzer;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Medical.Components;

/// <inheritdoc/>
[RegisterComponent, AutoGenerateComponentPause]

[Access(typeof(HealthAnalyzerSystem), typeof(CryoPodSystem))]
public sealed partial class HealthAnalyzerComponent : BaseAnalyzerComponent
{

    /// <summary>
    /// Shitmed Change: The body part that is currently being scanned.
    /// </summary>
    [DataField]
    public EntityUid? CurrentBodyPart;

    /// <summary>
    /// The maximum range in tiles at which the analyzer can receive continuous updates, a value of null will be infinite range
    /// </summary>

    public override TimeSpan NextUpdate { get; set; } = TimeSpan.Zero;
}
