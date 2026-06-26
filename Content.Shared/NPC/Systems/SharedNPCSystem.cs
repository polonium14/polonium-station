// SPDX-FileCopyrightText: 2024 Ed <96445749+TheShuEd@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
// SPDX-FileCopyrightText: 2026 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.NPC.Systems;

public abstract partial class SharedNPCSystem : EntitySystem
{
    /// <summary>
    /// Returns whether the given entity is an NPC.
    /// </summary>
    /// <param name="uid">Entity UID to check.</param>
    /// <returns><c>true</c> if the entity is an NPC, otherwise <c>false</c>.</returns>
    public abstract bool IsNpc(EntityUid uid);
}
