// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Audio;
namespace Content.Shared.Damage.Components;
[RegisterComponent]
public sealed partial class InjectOnHitComponent : Component
{
    [DataField("reagents")]
    public List<ReagentQuantity> Reagents;

    [DataField("limit")]
    public float? ReagentLimit;
    [DataField("sound")]
    public SoundSpecifier? Sound;
}
[ByRefEvent]
public record struct InjectOnHitAttemptEvent(bool Cancelled, EntityUid? Attacker = null);
