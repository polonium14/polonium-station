// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Explosion;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Vector2 = System.Numerics.Vector2;

namespace Content.Client.Explosion;

/// <summary>
/// Listens for <see cref="ExplosionShockwaveEvent"/> from the server
/// and manages the shockwave distortion overlay lifecycle.
/// </summary>
public sealed class ExplosionShockwaveSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    private ExplosionShockwaveOverlay? _overlay;

    public readonly List<ShockwaveInstance> ActiveWaves = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ExplosionShockwaveEvent>(OnShockwave);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        RemoveOverlay();
        ActiveWaves.Clear();
    }

    private void OnShockwave(ExplosionShockwaveEvent ev)
    {
        ActiveWaves.Add(new ShockwaveInstance
        {
            MapId = ev.MapId,
            EpicenterWorld = ev.EpicenterWorld,
            ServerStartSeconds = ev.ServerStartSeconds,
            MaxRadiusTiles = ev.MaxRadiusTiles,
            DurationSeconds = ev.DurationSeconds,
            Intensity = ev.Intensity,
            Flash = ev.Flash,
        });

        EnsureOverlay();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (ActiveWaves.Count == 0)
            return;

        var now = _timing.CurTime.TotalSeconds;
        ActiveWaves.RemoveAll(w => now - w.ServerStartSeconds >= w.DurationSeconds);

        if (ActiveWaves.Count == 0)
            RemoveOverlay();
    }

    private void EnsureOverlay()
    {
        if (_overlay != null)
            return;

        _overlay = new ExplosionShockwaveOverlay(this, _protoManager, _timing);
        _overlays.AddOverlay(_overlay);
    }

    private void RemoveOverlay()
    {
        if (_overlay == null)
            return;

        _overlays.RemoveOverlay(_overlay);
        _overlay = null;
    }

    public sealed class ShockwaveInstance
    {
        public MapId MapId;
        public Vector2 EpicenterWorld;
        public double ServerStartSeconds;
        public float MaxRadiusTiles;
        public float DurationSeconds;
        public float Intensity;
        public bool Flash;
    }
}
