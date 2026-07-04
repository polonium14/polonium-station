// SPDX-FileCopyrightText: 2026 A-Loose-Goose <237446272+A-Loose-Goose@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 beck <163376292+widgetbeck@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Damian Zieliński <zientasek.pl@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Impstation.Replicator;
using Robust.Client.GameObjects;

namespace Content.Client._Impstation.Replicator;

public sealed partial class ReplicatorNestVisualsSystem : SharedReplicatorNestSystem
{
    [Dependency] private AppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorNestComponent, ComponentStartup>(OnNestStartup);
        SubscribeLocalEvent<ReplicatorNestComponent, AfterAutoHandleStateEvent>(OnNestState);
        SubscribeLocalEvent<ReplicatorNestComponent, ReplicatorNestEmbiggenedEvent>(OnEmbiggened);
    }

    private void OnNestStartup(Entity<ReplicatorNestComponent> ent, ref ComponentStartup args)
    {
        UpdateNestVisuals(ent);
    }

    private void OnNestState(Entity<ReplicatorNestComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateNestVisuals(ent);
    }

    private void OnEmbiggened(Entity<ReplicatorNestComponent> ent, ref ReplicatorNestEmbiggenedEvent args)
    {
        UpdateNestVisuals(ent);
    }

    private void UpdateNestVisuals(Entity<ReplicatorNestComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var targetLayer = ent.Comp.CurrentLevel switch
        {
            >= 3 => ReplicatorNestVisuals.Level3,
            2 => ReplicatorNestVisuals.Level2,
            _ => ReplicatorNestVisuals.Level1,
        };

        if (!sprite.LayerMapTryGet(targetLayer, out var layerIndex))
            return;

        sprite.LayerSetVisible(layerIndex, true);
        _appearance.OnChangeData(ent, sprite);
    }
}
