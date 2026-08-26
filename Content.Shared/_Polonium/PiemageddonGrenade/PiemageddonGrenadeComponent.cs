// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.PiemageddonGrenade;

/// <summary>
/// A cluster grenade that scatters its fill into the world when triggered.
/// The fill can be another PiemageddonGrenade, which lets the grenade chain into more of itself.
/// </summary>
[RegisterComponent]
public sealed partial class PiemageddonGrenadeComponent : Component
{
    /// <summary>
    /// What the grenade scatters when it goes off.
    /// </summary>
    [DataField]
    public EntProtoId? FillPrototype;

    /// <summary>
    /// How many entities to scatter.
    /// </summary>
    [DataField]
    public int Count = 3;

    /// <summary>
    /// Distance each scattered entity is thrown.
    /// </summary>
    [DataField]
    public float Distance = 4f;

    /// <summary>
    /// Speed each scattered entity is thrown at.
    /// </summary>
    [DataField]
    public float Velocity = 6f;

    /// <summary>
    /// Whether scattered entities that carry a timer trigger should be armed to go off.
    /// </summary>
    [DataField]
    public bool TriggerContents = true;

    /// <summary>
    /// Delay before a scattered entity's timer fires.
    /// </summary>
    [DataField]
    public float DelayBeforeTriggerContents = 1f;

    /// <summary>
    /// Trigger key that fires the grenade.
    /// </summary>
    [DataField]
    public string TriggerKey = "timer";

    /// <summary>
    /// Set once the grenade has been triggered, so scattering can happen on the next frame update.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool IsTriggered;
}
