// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Medical.PatientIntake;

/// <summary>
/// Tags an NPC as the issued patient of a patient intake machine's active case.
/// Removing it turns the body into an ordinary one. Only <see cref="Machine"/> is networked,
/// so the client can predict which body the tray accepts; the rest is server bookkeeping.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PatientIntakePatientComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid Machine;

    /// <summary>
    /// Organ categories the body had before issues were applied.
    /// </summary>
    [ViewVariables]
    public HashSet<ProtoId<OrganCategoryPrototype>> ExpectedParts = new();

    /// <summary>
    /// Gear issued with the patient; all of it must be in the tray on return.
    /// </summary>
    [ViewVariables]
    public List<EntityUid> Gear = new();

    /// <summary>
    /// Organs that must have been swapped for another one (specific requests).
    /// </summary>
    [ViewVariables]
    public Dictionary<ProtoId<OrganCategoryPrototype>, EntityUid> Replace = new();

    [ViewVariables]
    public List<ProtoId<PatientIntakeIssuePrototype>> Issues = new();

    /// <summary>
    /// When this patient's tray was first opened. Each mass casualty patient gets its own clock.
    /// </summary>
    [ViewVariables]
    public TimeSpan? ClockStart;

    /// <summary>
    /// Frozen until the tray is first opened.
    /// </summary>
    [ViewVariables]
    public bool Frozen;
}
