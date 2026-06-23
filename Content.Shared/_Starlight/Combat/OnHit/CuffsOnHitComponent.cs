// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Damage.Components;

[RegisterComponent]
public sealed partial class CuffsOnHitComponent : Component
{
    [DataField("proto")]
    public ProtoId<EntityPrototype>? HandcuffProtorype;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(1);

    [DataField("sound")]
    public SoundSpecifier? Sound;
}
[ByRefEvent]
public record struct CuffsOnHitAttemptEvent(bool Cancelled);

[Serializable, NetSerializable]
public sealed partial class CuffsOnHitDoAfter : SimpleDoAfterEvent { }
