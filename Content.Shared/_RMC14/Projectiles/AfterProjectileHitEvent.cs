// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;

namespace Content.Shared._RMC14.Projectiles;

[ByRefEvent]
public record struct AfterProjectileHitEvent(Entity<ProjectileComponent> Projectile, EntityUid Target);
