// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Administration.Managers;
using Content.Client.Administration.Systems;
using Content.Shared.CCVar;
using Content.Shared.Ghost.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Polonium.Administration;

public sealed partial class NewPlayerMarkerOverlay : Overlay
{
    [Dependency] private IClientAdminManager _admin = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";
    private static readonly SpriteSpecifier.Rsi MarkerSprite = new(new ResPath("/Textures/_Polonium/Interface/Misc/new_player.rsi"), "new");

    private readonly AdminSystem _adminSystem;
    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;
    private readonly EntityQuery<SpriteComponent> _spriteQuery;
    private readonly EntityQuery<TransformComponent> _xformQuery;
    private readonly ShaderInstance _unshaded;

    private int _threshold;
    private bool _enabled;
    private Texture? _texture;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public NewPlayerMarkerOverlay()
    {
        IoCManager.InjectDependencies(this);

        _adminSystem = _entities.System<AdminSystem>();
        _sprite = _entities.System<SpriteSystem>();
        _transform = _entities.System<TransformSystem>();
        _spriteQuery = _entities.GetEntityQuery<SpriteComponent>();
        _xformQuery = _entities.GetEntityQuery<TransformComponent>();
        _unshaded = _prototypes.Index(UnshadedShader).Instance();
        ZIndex = 3;

        _cfg.OnValueChanged(CCVars.NewPlayerThreshold, value => _threshold = value, true);
        _cfg.OnValueChanged(CCVars.NewPlayerMarkerEnabled, value => _enabled = value, true);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewer = _players.LocalEntity;

        if (!_enabled || _threshold <= 0 || !_admin.IsActive() || !_entities.HasComponent<GhostComponent>(viewer))
            return;

        var texture = _texture ??= _sprite.GetFrame(MarkerSprite, TimeSpan.Zero);
        var texSize = (Vector2) texture.Size / EyeManager.PixelsPerMeter;
        var limit = TimeSpan.FromMinutes(_threshold);

        var handle = args.WorldHandle;
        var eyeRot = args.Viewport.Eye?.Rotation ?? default;
        var rotation = Matrix3Helpers.CreateRotation(-eyeRot);
        var scale = Matrix3Helpers.CreateScale(Vector2.One);

        foreach (var info in _adminSystem.PlayerList)
        {
            if (info.OverallPlaytime is not { } playtime || playtime >= limit)
                continue;

            if (info.NetEntity is not { } net || !_entities.TryGetEntity(net, out var ent) || ent == viewer)
                continue;

            var uid = ent.Value;

            // if (_entities.HasComponent<GhostComponent>(uid))
            //     continue;

            if (!_xformQuery.TryGetComponent(uid, out var xform) || xform.MapID != args.MapId)
                continue;

            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            float x;
            float y;

            if (_spriteQuery.TryGetComponent(uid, out var sprite))
            {
                var bounds = _sprite.GetLocalBounds((uid, sprite));
                if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                    continue;

                var centerX = bounds.Width > 0 ? bounds.Center.X : 0f;
                
                var top = bounds.Height > 0 ? bounds.Top : 0.9f;

                x = centerX + sprite.Offset.X - texSize.X / 2f;
                y = top + sprite.Offset.Y + 0.05f;
            }
            else
            {
                if (!Box2.CenteredAround(worldPos, new Vector2(2f, 2f)).Intersects(args.WorldAABB))
                    continue;

                x = -texSize.X / 2f;
                y = 1f;
            }

            var worldMatrix = Matrix3Helpers.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scale, worldMatrix);

            handle.SetTransform(Matrix3x2.Multiply(rotation, scaledWorld));
            handle.UseShader(_unshaded);
            handle.DrawTexture(texture, new Vector2(x, y));
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }
}
