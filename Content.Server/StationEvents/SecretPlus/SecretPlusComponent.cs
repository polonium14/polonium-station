// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Ilya246 <57039557+Ilya246@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Ilya246 <ilyukarno@gmail.com>
// SPDX-FileCopyrightText: 2026 Damian Zieliński <zientasek.pl@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Goobstation.StationEvents;
using Content.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.StationEvents.SecretPlus;

/// <summary>
///   Basic metric-based event scheduler.
///   Maintains a "chaos score", which is a number used to pick what events are rolled.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause, Access(typeof(SecretPlusSystem))]
public sealed partial class SecretPlusComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan TimeNextEvent;

    [DataField]
    public TimeSpan EventIntervalMin;

    [DataField]
    public TimeSpan EventIntervalMax;

    [DataField]
    public float ChaosScore = 0;

    [DataField]
    public float MinStartingChaos;

    [DataField]
    public float MaxStartingChaos;

    [DataField]
    public float LivingChaosChange;

    [DataField]
    public float DeadChaosChange;

    [ViewVariables]
    public float ChaosChangeVariation = 1f;

    [DataField]
    public float ChaosChangeVariationMin = 1f;

    [DataField]
    public float ChaosChangeVariationMax = 1f;

    [DataField]
    public float ChaosChangeVariationExponent = 2f;

    [DataField]
    public float ChaosOffset = 50f;

    [DataField]
    public float ChaosExponent = 1.1f;

    [DataField]
    public float ChaosMatching = 1.8f;

    [DataField]
    public float ChaosThreshold = 20f;

    [DataField]
    public float SpeedRamping = 0f;

    [DataField]
    public bool NoRoundstartAntags = false;

    [DataField]
    public bool IgnoreTimings = false;

    [DataField]
    public bool IgnoreIncompatible = false;

    [DataField]
    public HashSet<ProtoId<EventTypePrototype>> DisallowedEvents = new();

    [ViewVariables]
    public List<SelectedEvent> SelectedEvents = new();

    [DataField]
    public ProtoId<WeightedRandomPrototype> PrimaryAntagsWeightTable = "SecretPlusPrimary";

    [DataField]
    public float PrimaryAntagChaosBias = 2f;

    [DataField]
    public ProtoId<WeightedRandomPrototype> RoundStartAntagsWeightTable = "SecretPlus";
}
