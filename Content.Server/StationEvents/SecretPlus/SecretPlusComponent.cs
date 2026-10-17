// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Ilya246 <57039557+Ilya246@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Ilya246 <ilyukarno@gmail.com>
// SPDX-FileCopyrightText: 2026 Damian Zieliński <zientasek.pl@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Goobstation.StationEvents;
using Content.Shared.EntityTable.EntitySelectors;
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

    /// <summary>
    /// Rules selected by this scheduler whose player requirements are checked at round start.
    /// </summary>
    [ViewVariables]
    public List<EntityUid> RoundstartRules = new();

    [DataField]
    public ProtoId<WeightedRandomPrototype> PrimaryAntagsWeightTable = "SecretPlusPrimaryMid"; // Polonium

    [DataField]
    public float PrimaryAntagChaosBias = 2f;

    [DataField]
    public ProtoId<WeightedRandomPrototype> RoundStartAntagsWeightTable = "SecretPlusSupport"; // Polonium

    // POLONIUM START
    /// <summary>
    /// Chance of a round with no roundstart antags. The budget goes to events instead.
    /// </summary>
    [DataField]
    public float NoRoundstartAntagsChance;

    /// <summary>
    /// Picks from the primary table, keyed by the player count they kick in at.
    /// 1.5 is one pick and a 50% shot at a second.
    /// </summary>
    [DataField]
    public Dictionary<int, float> PrimaryAntagPicks = new() { { 0, 1f } };

    [DataField]
    public bool GuaranteedPrimaryAntags;

    [DataField]
    public float PrimaryAntagChaosShare = 1f;

    /// <summary>
    /// Roundstart rules that can't share a round. The value is the player count where they finally can.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, int> ExclusiveRules = new();

    /// <summary>
    /// Ghost role antags. Rolled together with the normal events, but capped and spaced out.
    /// </summary>
    [DataField]
    public EntityTableSelector? GhostAntagRules;

    /// <summary>
    /// Cap on ghost antags per round, keyed by player count.
    /// </summary>
    [DataField]
    public Dictionary<int, int> GhostAntagLimit = new();

    [DataField]
    public TimeSpan GhostAntagInterval;

    [ViewVariables]
    public int GhostAntagsStarted;

    [ViewVariables]
    public TimeSpan LastGhostAntag;

    /// <summary>
    /// Event player requirements count everyone connected, not just the living crew.
    /// </summary>
    [DataField]
    public bool EventsCountAllPlayers;

    /// <summary>
    /// Event speed multiplier, keyed by player count. At 1.5 events come one and a half times as often.
    /// </summary>
    [DataField]
    public Dictionary<int, float> EventSpeedByPlayers = new();

    /// <summary>
    /// From this point of the round late joiners can still become roundstart antags, if the crew has grown since the start.
    /// </summary>
    [DataField]
    public TimeSpan LateAntagsFrom;

    [DataField]
    public TimeSpan LateAntagsUntil;

    [ViewVariables]
    public bool LateAntagsClosed;

    [ViewVariables]
    public int StartingPlayers;
    // POLONIUM END
}
