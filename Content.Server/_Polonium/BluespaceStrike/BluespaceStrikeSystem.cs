// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server.AlertLevel;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._Polonium.BluespaceStrike;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.BluespaceStrike;

public sealed partial class BluespaceStrikeSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private SharedMapSystem _map = default!;

    private readonly HashSet<BluespaceStrikeEui> _openEuis = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BluespaceStrikeComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
    }

    public void RegisterEui(BluespaceStrikeEui eui) => _openEuis.Add(eui);

    public void UnregisterEui(BluespaceStrikeEui eui) => _openEuis.Remove(eui);

    private void OnAlertLevelChanged(AlertLevelChangedEvent args)
    {
        foreach (var eui in _openEuis)
        {
            eui.StateDirty();
        }
    }

    private void OnShutdown(Entity<BluespaceStrikeComponent> ent, ref ComponentShutdown args)
    {
        CleanupStrike(ent);
    }

    public EntityUid? ScheduleStrike(
        MapCoordinates epicenter,
        float radius,
        float delaySeconds,
        bool showMarkersAndSound = true,
        EntityUid? user = null)
    {
        if (radius <= 0)
            return null;

        delaySeconds = Math.Clamp(delaySeconds,
            BluespaceStrikeComponent.MinDelaySeconds,
            BluespaceStrikeComponent.MaxDelaySeconds);

        var slope = BluespaceStrikeComponent.DefaultSlope;
        var maxIntensity = BluespaceStrikeComponent.DefaultMaxIntensity;
        var totalIntensity = _explosion.RadiusToIntensity(radius, slope, maxIntensity);

        if (totalIntensity <= 0)
            return null;

        if (!_map.MapExists(epicenter.MapId))
            return null;

        // Ensure strike stays at the ordered coordinates
        var strike = Spawn(BluespaceStrikeComponent.ControllerPrototype, epicenter);
        var comp = EnsureComp<BluespaceStrikeComponent>(strike);
        var now = _timing.CurTime;
        var delay = TimeSpan.FromSeconds(delaySeconds);
        var fallDuration = TimeSpan.FromSeconds(BluespaceStrikeComponent.FallDurationSeconds);

        comp.Epicenter = epicenter;
        comp.DetonateAt = now + delay;
        comp.SpawnFallingAt = now + delay - fallDuration;
        if (comp.SpawnFallingAt < now)
            comp.SpawnFallingAt = now;

        comp.Radius = radius;
        comp.TotalIntensity = totalIntensity;
        comp.IntensitySlope = slope;
        comp.MaxIntensity = maxIntensity;
        Dirty(strike, comp);

        if (showMarkersAndSound)
        {
            SpawnMarkers(strike, comp, epicenter);
            StartAirRaid(strike, comp);
        }

        if (user != null)
        {
            _adminLog.Add(LogType.Explosion, LogImpact.Extreme,
                $"{ToPrettyString(user.Value):user} scheduled bluespace strike at {epicenter} radius={radius} delay={delaySeconds}s intensity={totalIntensity} warn={showMarkersAndSound}");
        }
        else
        {
            _adminLog.Add(LogType.Explosion, LogImpact.Extreme,
                $"Bluespace strike scheduled at {epicenter} radius={radius} delay={delaySeconds}s intensity={totalIntensity} warn={showMarkersAndSound}");
        }

        return strike;
    }

    private void SpawnMarkers(EntityUid strike, BluespaceStrikeComponent comp, MapCoordinates epicenter)
    {
        var radiusInt = (int)MathF.Ceiling(comp.Radius);
        for (var x = -radiusInt; x <= radiusInt; x++)
        {
            for (var y = -radiusInt; y <= radiusInt; y++)
            {
                if (x * x + y * y > comp.Radius * comp.Radius)
                    continue;

                var pos = epicenter.Position + new Vector2(x, y);
                var marker = Spawn(BluespaceStrikeComponent.MarkerPrototype, new MapCoordinates(pos, epicenter.MapId));
                comp.Markers.Add(marker);
            }
        }

        Dirty(strike, comp);
    }

    private void StartAirRaid(EntityUid strike, BluespaceStrikeComponent comp)
    {
        var maxDistance = MathF.Max(15f, comp.Radius * 2.5f);
        var audio = _audio.PlayPvs(
            comp.AirRaidSound,
            strike,
            AudioParams.Default.WithLoop(true).WithVolume(-5f).WithMaxDistance(maxDistance));

        if (audio != null)
            comp.AudioStream = audio.Value.Entity;

        Dirty(strike, comp);
    }

    private void SpawnFallingVisual(Entity<BluespaceStrikeComponent> ent)
    {
        if (ent.Comp.FallingSpawned)
            return;

        ent.Comp.FallingSpawned = true;
        var incoming = Spawn(BluespaceStrikeComponent.IncomingPrototype, ent.Comp.Epicenter);
        ent.Comp.IncomingVisual = incoming;

        if (TryComp(incoming, out BluespaceStrikeIncomingComponent? incomingComp))
        {
            incomingComp.FallDuration = TimeSpan.FromSeconds(BluespaceStrikeComponent.FallDurationSeconds);
            Dirty(incoming, incomingComp);
        }

        Dirty(ent);
    }

    private void Detonate(Entity<BluespaceStrikeComponent> ent)
    {
        var mapCoords = ent.Comp.Epicenter;
        var intensity = ent.Comp.TotalIntensity;
        var slope = ent.Comp.IntensitySlope;
        var max = ent.Comp.MaxIntensity;

        CleanupStrike(ent);
        RemComp<BluespaceStrikeComponent>(ent.Owner);

        _explosion.QueueExplosion(
            mapCoords,
            BluespaceStrikeComponent.ExplosionType,
            intensity,
            slope,
            max,
            null);

        QueueDel(ent.Owner);
    }

    private void CleanupStrike(Entity<BluespaceStrikeComponent> ent)
    {
        if (ent.Comp.AudioStream is { } stream)
        {
            _audio.Stop(stream);
            ent.Comp.AudioStream = null;
        }

        foreach (var marker in ent.Comp.Markers)
        {
            if (!Deleted(marker))
                QueueDel(marker);
        }

        ent.Comp.Markers.Clear();

        if (ent.Comp.IncomingVisual is { } incoming && !Deleted(incoming))
            QueueDel(incoming);

        ent.Comp.IncomingVisual = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var toDetonate = new List<EntityUid>();
        var query = EntityQueryEnumerator<BluespaceStrikeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.FallingSpawned && now >= comp.SpawnFallingAt)
                SpawnFallingVisual((uid, comp));

            if (now >= comp.DetonateAt)
                toDetonate.Add(uid);
        }

        foreach (var uid in toDetonate)
        {
            if (TryComp(uid, out BluespaceStrikeComponent? comp))
                Detonate((uid, comp));
        }
    }
}
