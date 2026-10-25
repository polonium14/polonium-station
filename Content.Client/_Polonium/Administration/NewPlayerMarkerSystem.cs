// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Administration.Managers;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;

namespace Content.Client._Polonium.Administration;

public sealed partial class NewPlayerMarkerSystem : EntitySystem
{
    [Dependency] private IClientAdminManager _admin = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IOverlayManager _overlays = default!;

    private NewPlayerMarkerOverlay _marker = default!;

    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();

        _marker = new NewPlayerMarkerOverlay();
        _admin.AdminStatusUpdated += SyncOverlay;

        Subs.CVar(_cfg, CCVars.NewPlayerMarkerEnabled, value =>
        {
            _enabled = value;
            SyncOverlay();
        }, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _admin.AdminStatusUpdated -= SyncOverlay;
        _overlays.RemoveOverlay(_marker);
    }

    private void SyncOverlay()
    {
        if (_enabled && _admin.IsActive())
        {
            _overlays.AddOverlay(_marker);

            return;
        }

        _overlays.RemoveOverlay(_marker);
    }
}
