// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Access;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Actions;

/// <summary>Slaps extra access tags onto the player's ID card.</summary>
public sealed partial class GrantAccessAction : TutorialAction
{
    [DataField(required: true)]
    public HashSet<ProtoId<AccessLevelPrototype>> Tags = new();
}
