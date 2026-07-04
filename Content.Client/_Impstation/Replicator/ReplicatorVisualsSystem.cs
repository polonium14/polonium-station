// SPDX-FileCopyrightText: 2026 A-Loose-Goose <237446272+A-Loose-Goose@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 beck <163376292+widgetbeck@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Damian Zieliński <zientasek.pl@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Impstation.Replicator;
using Content.Shared.CombatMode;
using Robust.Client.GameObjects;

namespace Content.Client._Impstation.Replicator;

public sealed class ReplicatorVisualsSystem : SharedReplicatorSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<ReplicatorComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!TryComp<CombatModeComponent>(ent, out var combat))
            return;

        if (!args.Sprite.LayerMapTryGet(ReplicatorVisuals.Combat, out var layer))
            return;

        args.Sprite.LayerSetVisible(layer, combat.IsInCombatMode);
    }
}
