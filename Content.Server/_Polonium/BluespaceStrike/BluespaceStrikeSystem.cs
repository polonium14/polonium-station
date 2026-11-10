// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._Polonium.BluespaceStrike;
using Content.Shared.Administration;
using Content.Shared.Administration.Logs;
using Content.Shared.AlertLevel;
using Content.Shared.Database;
using Content.Shared.Explosion.Components;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.BluespaceStrike;

public sealed partial class BluespaceStrikeSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;

    private static readonly EntProtoId StrikePrototype = "BluespaceStrike";

    private readonly HashSet<BluespaceStrikeEui> _openEuis = new();
    private readonly List<(EntityUid Uid, float Radius, ExplosiveComponent Explosive)> _toDetonate = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BluespaceStrikeComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
    }

    public void RegisterEui(BluespaceStrikeEui eui) => _openEuis.Add(eui);

    public void UnregisterEui(BluespaceStrikeEui eui) => _openEuis.Remove(eui);

    private void OnAlertLevelChanged(ref AlertLevelChangedEvent args)
    {
        foreach (var eui in _openEuis)
        {
            eui.StateDirty();
        }
    }

    private void OnShutdown(Entity<BluespaceStrikeComponent> ent, ref ComponentShutdown args)
    {
        foreach (var marker in ent.Comp.Markers)
        {
            QueueDel(marker);
        }

        ent.Comp.Markers.Clear();
    }

    /// <summary>
    /// Builds the explosion preview of a strike with the given full-strength radius.
    /// </summary>
    public BluespaceStrikeEuiMsg.PreviewData? GeneratePreview(MapCoordinates epicenter, float radius)
    {
        if (radius is not > 0 || !TryGetStrikeExplosive(out var explosive))
            return null;

        var totalIntensity = GetTotalIntensity(explosive, radius);
        var request = new SpawnExplosionEuiMsg.PreviewRequest(
            epicenter,
            explosive.ExplosionType,
            totalIntensity,
            explosive.IntensitySlope,
            explosive.MaxIntensity);

        if (_explosion.GenerateExplosionPreview(request) is not { } explosion)
            return null;

        return new BluespaceStrikeEuiMsg.PreviewData(explosion, explosive.IntensitySlope, totalIntensity);
    }

    /// <summary>
    /// Spawns a strike that explodes after <paramref name="delaySeconds"/>.
    /// </summary>
    public EntityUid? ScheduleStrike(
        MapCoordinates epicenter,
        float radius,
        float delaySeconds,
        bool showMarkersAndSound = true,
        EntityUid? user = null)
    {
        if (radius is not > 0
            || !float.IsFinite(delaySeconds)
            || !_map.MapExists(epicenter.MapId)
            || !TryGetStrikeExplosive(out var explosive))
            return null;

        delaySeconds = Math.Clamp(delaySeconds,
            BluespaceStrikeComponent.MinDelaySeconds,
            BluespaceStrikeComponent.MaxDelaySeconds);

        var strike = Spawn(StrikePrototype, epicenter);
        var comp = Comp<BluespaceStrikeComponent>(strike);
        comp.DetonateAt = _timing.CurTime + TimeSpan.FromSeconds(delaySeconds);
        comp.Radius = Math.Min(radius, BluespaceStrikeComponent.MaxRadius);
        Dirty(strike, comp);

        if (showMarkersAndSound)
        {
            SpawnMarkers(comp, epicenter);
            StartAirRaid((strike, comp));
        }

        var totalIntensity = GetTotalIntensity(explosive, comp.Radius);
        if (user != null)
        {
            _adminLog.Add(LogType.Explosion, LogImpact.Extreme,
                $"{ToPrettyString(user.Value):user} scheduled bluespace strike at {epicenter} radius={comp.Radius} delay={delaySeconds}s intensity={totalIntensity} warn={showMarkersAndSound}");
        }
        else
        {
            _adminLog.Add(LogType.Explosion, LogImpact.Extreme,
                $"Bluespace strike scheduled at {epicenter} radius={comp.Radius} delay={delaySeconds}s intensity={totalIntensity} warn={showMarkersAndSound}");
        }

        return strike;
    }

    private bool TryGetStrikeExplosive([NotNullWhen(true)] out ExplosiveComponent? explosive)
    {
        return ProtoMan.Index(StrikePrototype).TryComp(out explosive, EntityManager.ComponentFactory);
    }

    /// <summary>
    /// The intensity is capped near the epicenter, so the explosion keeps full strength up to
    /// <paramref name="radius"/> and then falls off over another MaxIntensity / IntensitySlope tiles.
    /// </summary>
    private float GetTotalIntensity(ExplosiveComponent explosive, float radius)
    {
        radius = Math.Min(radius, BluespaceStrikeComponent.MaxRadius);
        var falloff = explosive.MaxIntensity / explosive.IntensitySlope;
        return _explosion.RadiusToIntensity(radius + falloff, explosive.IntensitySlope, explosive.MaxIntensity);
    }

    private void SpawnMarkers(BluespaceStrikeComponent comp, MapCoordinates epicenter)
    {
        var radiusInt = (int) MathF.Ceiling(comp.Radius);
        var radiusSq = comp.Radius * comp.Radius;

        for (var x = -radiusInt; x <= radiusInt; x++)
        {
            for (var y = -radiusInt; y <= radiusInt; y++)
            {
                if (x * x + y * y > radiusSq)
                    continue;

                comp.Markers.Add(Spawn(comp.MarkerPrototype, epicenter.Offset(x, y)));
            }
        }
    }

    private void StartAirRaid(Entity<BluespaceStrikeComponent> ent)
    {
        var maxDistance = MathF.Max(15f, ent.Comp.Radius * 2.5f);
        _audio.PlayPvs(
            ent.Comp.AirRaidSound,
            ent,
            AudioParams.Default.WithLoop(true).WithVolume(-5f).WithMaxDistance(maxDistance));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        _toDetonate.Clear();
        var query = EntityQueryEnumerator<BluespaceStrikeComponent, ExplosiveComponent>();
        while (query.MoveNext(out var uid, out var strike, out var explosive))
        {
            // Spawned by hand rather than by ScheduleStrike.
            if (strike.Radius <= 0)
                continue;

            if (!strike.Falling && now >= strike.DetonateAt - strike.FallDuration)
            {
                strike.Falling = true;
                _pointLight.SetEnabled(uid, true);
            }

            if (now >= strike.DetonateAt)
                _toDetonate.Add((uid, strike.Radius, explosive));
        }

        foreach (var (uid, radius, explosive) in _toDetonate)
        {
            _explosion.TriggerExplosive(uid, explosive, totalIntensity: GetTotalIntensity(explosive, radius));
        }
    }
}
