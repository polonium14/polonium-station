// SPDX-FileCopyrightText: 2022 Moony <moonheart08@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
// SPDX-FileCopyrightText: 2026 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Administration.UI.SpawnExplosion;
using Content.Client.Eui;
using Content.Shared._Polonium.BluespaceStrike;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.Client._Polonium.BluespaceStrike;

[UsedImplicitly]
public sealed partial class BluespaceStrikeEui : BaseEui
{
    [Dependency] private EntityManager _entManager = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;

    private readonly BluespaceStrikeWindow _window;
    private ExplosionDebugOverlay? _debugOverlay;

    public BluespaceStrikeEui()
    {
        IoCManager.InjectDependencies(this);
        _window = new BluespaceStrikeWindow(this);
        _window.OnClose += SendClosedMessage;
    }

    public override void Opened()
    {
        base.Opened();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.OnClose -= SendClosedMessage;
        _window.Close();
        ClearOverlay();
    }

    public void SendClosedMessage()
    {
        SendMessage(new CloseEuiMessage());
    }

    public void ClearOverlay()
    {
        if (_overlayManager.HasOverlay<ExplosionDebugOverlay>())
            _overlayManager.RemoveOverlay<ExplosionDebugOverlay>();
        _debugOverlay = null;
    }

    public void RequestPreviewData(MapCoordinates epicenter, float radius)
    {
        SendMessage(new BluespaceStrikeEuiMsg.PreviewRequest(epicenter, radius));
    }

    public void ConfirmStrike(MapCoordinates epicenter, float radius, float delaySeconds, bool showMarkersAndSound)
    {
        SendMessage(new BluespaceStrikeEuiMsg.Confirm(epicenter, radius, delaySeconds, showMarkersAndSound));
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not BluespaceStrikeEuiState strikeState)
            return;

        _window.SetEpsilonWarning(strikeState.IsEpsilon);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        if (msg is not BluespaceStrikeEuiMsg.PreviewData data)
            return;

        if (_debugOverlay == null)
        {
            _debugOverlay = new();
            _overlayManager.AddOverlay(_debugOverlay);
        }

        var tiles = new Dictionary<EntityUid, Dictionary<int, List<Vector2i>>>();
        _debugOverlay.Tiles.Clear();

        foreach (var (nent, det) in data.Explosion.Tiles)
        {
            tiles[_entManager.GetEntity(nent)] = det;
        }

        _debugOverlay.Tiles = tiles;
        _debugOverlay.SpaceTiles = data.Explosion.SpaceTiles;
        _debugOverlay.Intensity = data.Explosion.Intensity;
        _debugOverlay.Slope = data.Slope;
        _debugOverlay.TotalIntensity = data.TotalIntensity;
        _debugOverlay.Map = data.Explosion.Epicenter.MapId;
        _debugOverlay.SpaceMatrix = data.Explosion.SpaceMatrix;
        _debugOverlay.SpaceTileSize = data.Explosion.SpaceTileSize;
    }
}
