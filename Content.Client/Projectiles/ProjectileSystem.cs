// SPDX-FileCopyrightText: 2023 AJCM-git <60196617+AJCM-git@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Tayrtahn <tayrtahn@gmail.com>
// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
// SPDX-FileCopyrightText: 2026 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Starlight.Weapons.Ranged;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using TimedDespawnComponent = Robust.Shared.Spawners.TimedDespawnComponent;

namespace Content.Client.Projectiles;

public sealed partial class ProjectileSystem : SharedProjectileSystem
{
    [Dependency] private AnimationPlayerSystem _player = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private ImpactVisualsSystem _impactVisuals = default!; // Polonium
    [Dependency] private SharedTransformSystem _xform = default!; // Polonium

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<ImpactEffectEvent>(OnProjectileImpact);
    }

    private void OnProjectileImpact(ImpactEffectEvent ev)
    {
        var coords = GetCoordinates(ev.Coordinates);

        if (Deleted(coords.EntityId))
            return;

        // Polonium start: bullet holes, sparks and the hit wobble (port from Starlight)
        if (TryGetEntity(ev.Target, out var target) && Exists(target))
            _impactVisuals.PlayImpact(coords, ev.WorldAngle - _xform.GetWorldRotation(coords.EntityId), target.Value);
        // Polonium end

        var ent = Spawn(ev.Prototype, coords);

        if (TryComp<SpriteComponent>(ent, out var sprite))
        {
            sprite[EffectLayers.Unshaded].AutoAnimated = false;
            _sprite.LayerMapTryGet((ent, sprite), EffectLayers.Unshaded, out var layer, false);
            var state = _sprite.LayerGetRsiState((ent, sprite), layer);
            var lifetime = 0.5f;

            if (TryComp<TimedDespawnComponent>(ent, out var despawn))
                lifetime = despawn.Lifetime;

            var anim = new Animation()
            {
                Length = TimeSpan.FromSeconds(lifetime),
                AnimationTracks =
                {
                    new AnimationTrackSpriteFlick()
                    {
                        LayerKey = EffectLayers.Unshaded,
                        KeyFrames =
                        {
                            new AnimationTrackSpriteFlick.KeyFrame(state.Name, 0f),
                        }
                    }
                }
            };

            _player.Play(ent, anim, "impact-effect");
        }
    }
}
