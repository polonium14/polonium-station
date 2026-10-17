using System.Linq;
using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Server._Polonium.Audio;
using Content.Shared._Impstation.Replicator;
using Content.Shared._Polonium.Audio;
using Content.Shared._Polonium.Replicator;
using Content.Shared.Audio;
using Content.Shared.Camera;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Server.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Polonium.Replicator;

public sealed partial class ReplicatorColossusSystem
{
    [Dependency] private SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AudioPreloadSystem _audioPreload = default!;
    [Dependency] private ReplicatorColossusGunSystem _colossusGun = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;

    private readonly List<EntityUid> _victims = new();

    private bool _assimilated;
    private bool _colossusDestroyed;

    private void InitializeAssimilation()
    {
        SubscribeLocalEvent<ReplicatorColossusAssimilationComponent, ReplicatorColossusBeaconActionEvent>(OnBeaconAction);
        SubscribeLocalEvent<ReplicatorColossusAssimilationComponent, ReplicatorColossusBeaconDoAfterEvent>(OnBeaconDoAfter);
        SubscribeLocalEvent<ReplicatorColossusBeaconComponent, DamageChangedEvent>(OnBeaconDamaged);
        SubscribeLocalEvent<ReplicatorColossusBeaconComponent, DestructionEventArgs>(OnBeaconDestroyed);
        SubscribeLocalEvent<ReplicatorColossusBeaconComponent, ComponentShutdown>(OnBeaconShutdown);
        SubscribeLocalEvent<ReplicatorColossusBeaconComponent, ExaminedEvent>(OnBeaconExamined);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
    }

    private void UpdateAssimilation(TimeSpan now)
    {
        var beacons = EntityQueryEnumerator<ReplicatorColossusBeaconComponent>();
        while (beacons.MoveNext(out var uid, out var beacon))
        {
            if (beacon.Released)
                continue;

            if (!beacon.Ready)
                UpdateBeacon((uid, beacon), now);
            else if (beacon.ReleaseTime is not { } release)
                TryLink((uid, beacon), now);
            else if (now >= release)
                Release((uid, beacon), now);
        }

        var disarmed = EntityQueryEnumerator<ReplicatorColossusDisarmedComponent>();
        while (disarmed.MoveNext(out var uid, out var comp))
        {
            if (now < comp.EndTime)
                continue;

            RemCompDeferred<ReplicatorColossusDisarmedComponent>(uid);
            _popup.PopupEntity(Loc.GetString("replicator-colossus-rearmed"), uid, uid, PopupType.Medium);
        }

        var waves = EntityQueryEnumerator<ReplicatorAssimilationWaveComponent, TransformComponent>();
        while (waves.MoveNext(out var uid, out var wave, out var xform))
        {
            UpdateWave((uid, wave), xform, now);
        }
    }

    #region Beacon

    private void OnBeaconAction(Entity<ReplicatorColossusAssimilationComponent> ent, ref ReplicatorColossusBeaconActionEvent args)
    {
        if (args.Handled || GetBeaconSite(ent, args.Target) is not { } location)
            return;

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.BeaconDelay,
            new ReplicatorColossusBeaconDoAfterEvent(GetNetCoordinates(location)), ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DamageThreshold = ent.Comp.BeaconDamageThreshold,
            NeedHand = false,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            args.Handled = true;
    }

    private void OnBeaconDoAfter(Entity<ReplicatorColossusAssimilationComponent> ent, ref ReplicatorColossusBeaconDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        if (GetBeaconSite(ent, GetCoordinates(args.Location)) is not { } location)
            return;

        var beacon = Spawn(ent.Comp.Beacon, location);
        var comp = EnsureComp<ReplicatorColossusBeaconComponent>(beacon);
        comp.Colossus = ent;
        comp.ReadyTime = _timing.CurTime + comp.ChargeTime;
        ent.Comp.BeaconEntity = beacon;
        _appearance.SetData(beacon, ReplicatorColossusBeaconVisuals.Stage, comp.Stage);
        _audio.PlayPvs(comp.DeploySound, beacon);

        if (comp.Music is { } music)
            comp.ResolvedMusic = _audioPreload.Preload(music, GetMusicFilter(), PreloadAudioKind.StationEventMusic);

        Announce("replicator-colossus-beacon-announcement",
            ("location", GetLocation(beacon)),
            ("minutes", (int) Math.Ceiling(comp.ChargeTime.TotalMinutes)));
    }

    private EntityCoordinates? GetBeaconSite(Entity<ReplicatorColossusAssimilationComponent> ent, EntityCoordinates target)
    {
        string? reason = null;
        Entity<MapGridComponent> grid = default;
        TileRef tile = default;

        if (ent.Comp.BeaconEntity is { } existing && !TerminatingOrDeleted(existing))
            reason = "replicator-colossus-beacon-fail-exists";
        else if (!_construction.TryGetFloor(target, out grid, out tile))
            reason = "replicator-construction-fail-no-floor";
        else if (!_construction.IsHiveFloor(ent, tile))
            reason = "replicator-construction-fail-not-hive-floor";
        else if (_turf.IsTileBlocked(tile, ObstacleLayers))
            reason = "replicator-construction-fail-occupied";

        if (reason == null)
            return _map.GridTileToLocal(grid, grid, tile.GridIndices);

        _popup.PopupEntity(Loc.GetString(reason), ent, ent, PopupType.SmallCaution);
        return null;
    }

    private void UpdateBeacon(Entity<ReplicatorColossusBeaconComponent> ent, TimeSpan now)
    {
        var left = ent.Comp.ReadyTime - now;
        if (left <= TimeSpan.Zero)
        {
            ent.Comp.Ready = true;
            _appearance.SetData(ent, ReplicatorColossusBeaconVisuals.State, ReplicatorColossusBeaconState.Ready);
            if (_pointLight.TryGetLight(ent, out var light))
                ent.Comp.ChargingLightColor ??= light.Color;

            _pointLight.SetColor(ent, ent.Comp.ReadyLightColor);
            _audio.PlayPvs(ent.Comp.ReadySound, ent);
            if (ent.Comp.ReadyAmbience is { } ambience)
            {
                ent.Comp.ChargingAmbience ??= CompOrNull<AmbientSoundComponent>(ent)?.Sound;
                _ambientSound.SetSound(ent, ambience);
            }

            Announce("replicator-colossus-beacon-ready-announcement", ("location", GetLocation(ent)));

            if (ent.Comp.Colossus is { } colossus && !TerminatingOrDeleted(colossus))
                _popup.PopupEntity(Loc.GetString("replicator-colossus-beacon-ready"), colossus, colossus, PopupType.Large);

            return;
        }

        if (!ent.Comp.MusicStarted && ent.Comp.Music is { } music && left <= ent.Comp.MusicLead)
        {
            ent.Comp.MusicStarted = true;

            var filter = GetMusicFilter();
            RaiseNetworkEvent(new StopStationEventMusic(StationEventMusicType.ReplicatorBeacon), filter);
            RaiseNetworkEvent(new StationEventMusicEvent(ent.Comp.ResolvedMusic ?? _audio.ResolveSound(music),
                    StationEventMusicType.ReplicatorBeacon,
                    music.Params),
                filter);
        }

        var stage = Math.Clamp((int) ((1 - left / ent.Comp.ChargeTime) * ent.Comp.Stages), 0, ent.Comp.Stages - 1);
        if (stage != ent.Comp.Stage)
        {
            ent.Comp.Stage = stage;
            _appearance.SetData(ent, ReplicatorColossusBeaconVisuals.Stage, stage);

            if (ent.Comp.StageSound is { } sound)
                _audio.PlayPvs(sound, ent, sound.Params.WithPitchScale(1f + stage * ent.Comp.StagePitchStep));
        }

        if (ent.Comp.WarningsGiven >= ent.Comp.Warnings.Count || left > ent.Comp.Warnings[ent.Comp.WarningsGiven])
            return;

        ent.Comp.WarningsGiven++;
        Announce("replicator-colossus-beacon-warning",
            ("location", GetLocation(ent)),
            ("minutes", (int) Math.Ceiling(left.TotalMinutes)));
    }

    private void OnBeaconDestroyed(Entity<ReplicatorColossusBeaconComponent> ent, ref DestructionEventArgs args)
    {
        Announce("replicator-colossus-beacon-destroyed-announcement");

        if (ent.Comp.Colossus is { } colossus && !TerminatingOrDeleted(colossus))
            _popup.PopupEntity(Loc.GetString("replicator-colossus-beacon-lost"), colossus, colossus, PopupType.LargeCaution);
    }

    private void OnBeaconShutdown(Entity<ReplicatorColossusBeaconComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.ReleaseTime != null)
            EndLink(ent, true);

        if (ent.Comp.MusicStarted)
            RaiseNetworkEvent(new StopStationEventMusic(StationEventMusicType.ReplicatorBeacon), Filter.Broadcast());
    }

    private Filter GetMusicFilter()
    {
        return Filter.Empty().AddWhere(_gameTicker.UserHasJoinedGame);
    }

    private void OnBeaconExamined(Entity<ReplicatorColossusBeaconComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Ready)
        {
            args.PushMarkup(Loc.GetString("replicator-colossus-beacon-examine-ready"));
            return;
        }

        var left = ent.Comp.ReadyTime - _timing.CurTime;
        if (left < TimeSpan.Zero)
            left = TimeSpan.Zero;

        args.PushMarkup(Loc.GetString("replicator-colossus-beacon-examine-charging", ("time", left.ToString(@"mm\:ss"))));
    }

    #endregion

    #region Release

    private void TryLink(Entity<ReplicatorColossusBeaconComponent> ent, TimeSpan now)
    {
        if (ent.Comp.Colossus is not { } colossus
            || TerminatingOrDeleted(colossus)
            || !_mobState.IsAlive(colossus)
            || HasComp<ReplicatorColossusLaunchComponent>(colossus)
            || !_transform.InRange(Transform(colossus).Coordinates, Transform(ent).Coordinates, ent.Comp.LinkRange))
            return;

        ent.Comp.ReleaseTime = now + ent.Comp.ReleaseDuration;
        ent.Comp.ReleaseDamage = 0;

        EnsureComp<ReplicatorColossusLinkedComponent>(colossus);
        _colossusGun.Drain(colossus, int.MaxValue);

        if (ent.Comp.ReleaseEffect is { } effect)
            ent.Comp.ReleaseEffectEntity = Spawn(effect, Transform(colossus).Coordinates);

        if (ent.Comp.LinkBeam is { } beam)
        {
            var joint = EnsureComp<JointVisualsComponent>(ent);
            joint.Sprite = beam;
            joint.Target = ent.Comp.ReleaseEffectEntity ?? colossus;
            joint.OffsetA = ent.Comp.LinkBeamOffset;
            joint.OffsetB = ent.Comp.ReleaseEffectEntity == null ? Vector2.Zero : ent.Comp.ReleaseEffectOffset;
            Dirty(ent, joint);
        }

        ent.Comp.ReleaseSoundEntity = _audio.PlayGlobal(ent.Comp.ReleaseSound, Filter.Broadcast(), true)?.Entity;
        _appearance.SetData(ent, ReplicatorColossusBeaconVisuals.State, ReplicatorColossusBeaconState.Firing);
        _popup.PopupEntity(Loc.GetString("replicator-colossus-link-start"), colossus, colossus, PopupType.LargeCaution);

        Announce("replicator-colossus-assimilate-announcement",
            ("location", GetLocation(ent)),
            ("seconds", (int) ent.Comp.ReleaseDuration.TotalSeconds));
    }

    private void EndLink(Entity<ReplicatorColossusBeaconComponent> ent, bool disarm)
    {
        ent.Comp.ReleaseTime = null;

        if (ent.Comp.ReleaseEffectEntity is { } effect)
            QueueDel(effect);

        ent.Comp.ReleaseEffectEntity = null;
        _audio.Stop(ent.Comp.ReleaseSoundEntity);
        ent.Comp.ReleaseSoundEntity = null;

        if (!TerminatingOrDeleted(ent))
            RemComp<JointVisualsComponent>(ent);

        if (ent.Comp.Colossus is not { } colossus || TerminatingOrDeleted(colossus))
            return;

        RemCompDeferred<ReplicatorColossusLinkedComponent>(colossus);
        if (!disarm)
            return;

        _colossusGun.Drain(colossus, int.MaxValue);
        EnsureComp<ReplicatorColossusDisarmedComponent>(colossus).EndTime = _timing.CurTime + ent.Comp.InterruptDisarm;
        _popup.PopupEntity(Loc.GetString("replicator-colossus-link-broken"), colossus, colossus, PopupType.LargeCaution);
    }

    private void OnBeaconDamaged(Entity<ReplicatorColossusBeaconComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.ReleaseTime == null || !args.DamageIncreased || args.DamageDelta is not { } delta)
            return;

        ent.Comp.ReleaseDamage += delta.GetTotal();
        if (ent.Comp.ReleaseDamage >= ent.Comp.InterruptDamage)
            Interrupt(ent);
    }

    private void Interrupt(Entity<ReplicatorColossusBeaconComponent> ent)
    {
        EndLink(ent, true);

        var rollback = ent.Comp.InterruptRollback;
        ent.Comp.Ready = false;
        ent.Comp.ReadyTime = _timing.CurTime + rollback;
        ent.Comp.WarningsGiven = ent.Comp.Warnings.Count(warning => warning >= rollback);

        _appearance.SetData(ent, ReplicatorColossusBeaconVisuals.State, ReplicatorColossusBeaconState.Charging);
        if (ent.Comp.ChargingLightColor is { } color)
            _pointLight.SetColor(ent, color);

        if (ent.Comp.ChargingAmbience is { } ambience)
            _ambientSound.SetSound(ent, ambience);

        Announce("replicator-colossus-assimilate-interrupted-announcement",
            ("location", GetLocation(ent)),
            ("minutes", (int) Math.Ceiling(rollback.TotalMinutes)));
    }

    private bool IsDisarmed(EntityUid uid)
    {
        return HasComp<ReplicatorColossusLinkedComponent>(uid) || HasComp<ReplicatorColossusDisarmedComponent>(uid);
    }

    private void Release(Entity<ReplicatorColossusBeaconComponent> ent, TimeSpan now)
    {
        EndLink(ent, false);
        ent.Comp.Released = true;

        var beaconXform = Transform(ent);
        if (beaconXform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var uid = Spawn(ent.Comp.Wave, beaconXform.Coordinates);
        var wave = EnsureComp<ReplicatorAssimilationWaveComponent>(uid);
        var origin = _transform.GetWorldPosition(beaconXform);

        var tiles = _map.GetAllTiles(gridUid, grid);
        while (tiles.MoveNext(out var tile))
        {
            var indices = tile.Value.GridIndices;
            var distance = Vector2.Distance(_map.GridTileToWorldPos(gridUid, grid, indices), origin);
            wave.Tiles.Add((distance, indices));
            wave.MaxRadius = MathF.Max(wave.MaxRadius, distance + 1f);
        }

        wave.Tiles.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        wave.Grid = gridUid;
        wave.Nest = CompOrNull<ReplicatorComponent>(ent.Comp.Colossus)?.MyNest;
        wave.StartTime = now;
        Dirty(uid, wave);
        _pvsOverride.AddGlobalOverride(uid);

        _audio.PlayGlobal(wave.Sound, Filter.Broadcast(), true);
        Announce("replicator-colossus-assimilate-released-announcement");

        var cameras = EntityQueryEnumerator<CameraRecoilComponent, TransformComponent>();
        while (cameras.MoveNext(out var camera, out var recoil, out var xform))
        {
            if (xform.GridUid == gridUid)
                _cameraRecoil.KickCamera(camera, _random.NextAngle().ToWorldVec() * 3f, recoil);
        }
    }

    #endregion

    #region Wave

    private void UpdateWave(Entity<ReplicatorAssimilationWaveComponent> ent, TransformComponent xform, TimeSpan now)
    {
        if (!TryComp<MapGridComponent>(ent.Comp.Grid, out var grid))
        {
            QueueDel(ent);
            return;
        }

        var radius = (float) (now - ent.Comp.StartTime).TotalSeconds * ent.Comp.Speed;
        while (ent.Comp.NextTile < ent.Comp.Tiles.Count && ent.Comp.Tiles[ent.Comp.NextTile].Distance <= radius)
        {
            AssimilateTile(ent, (ent.Comp.Grid, grid), ent.Comp.Tiles[ent.Comp.NextTile].Indices, now);
            ent.Comp.NextTile++;
        }

        var origin = _transform.GetWorldPosition(xform);
        _victims.Clear();

        var mobs = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (mobs.MoveNext(out var mob, out _, out var mobXform))
        {
            if (mobXform.GridUid == ent.Comp.Grid
                && !HasComp<ReplicatorComponent>(mob)
                && Vector2.Distance(_transform.GetWorldPosition(mobXform), origin) <= radius)
            {
                _victims.Add(mob);
            }
        }

        foreach (var victim in _victims)
        {
            if (HasComp<HumanoidProfileComponent>(victim) && !_mobState.IsDead(victim))
                Rebuild(ent, victim);
            else
                Spawn(ent.Comp.Remains, _transform.GetMapCoordinates(victim));

            QueueDel(victim);
        }

        if (radius < ent.Comp.MaxRadius)
            return;

        QueueDel(ent);
        _assimilated = true;
        _roundEnd.EndRound();
    }

    private void Rebuild(Entity<ReplicatorAssimilationWaveComponent> ent, EntityUid victim)
    {
        var replicator = Spawn(ent.Comp.Replicator, _transform.GetMapCoordinates(victim));

        if (ent.Comp.Nest is { } nest && TryComp<ReplicatorNestComponent>(nest, out var nestComp))
        {
            EnsureComp<ReplicatorComponent>(replicator).MyNest = nest;
            nestComp.SpawnedMinions.Add(replicator);
        }

        if (!_mind.TryGetMind(victim, out var mind, out _))
            return;

        _mind.TransferTo(mind, replicator);
        _popup.PopupEntity(Loc.GetString("replicator-colossus-assimilated"), replicator, replicator, PopupType.LargeCaution);
    }

    private void AssimilateTile(Entity<ReplicatorAssimilationWaveComponent> ent, Entity<MapGridComponent> grid, Vector2i indices, TimeSpan now)
    {
        if (!ConvertToHive(grid, indices, ent.Comp.Wall, ent.Comp.ReinforcedWall, ent.Comp.ReinforcedThreshold, ent.Comp.Tile, out var location))
            return;

        if (ent.Comp.ConvertEffect is { } effect && _random.Prob(ent.Comp.ConvertEffectChance))
            Spawn(effect, location);

        if (now < ent.Comp.NextConvertSound)
            return;

        ent.Comp.NextConvertSound = now + ent.Comp.ConvertSoundInterval;
        _audio.PlayPvs(ent.Comp.ConvertSound, location);
    }

    #endregion

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _assimilated = false;
        _colossusDestroyed = false;
    }

    private void OnRoundEndText(RoundEndTextAppendEvent args)
    {
        if (_assimilated)
            args.AddLine(Loc.GetString("replicator-round-end-assimilated"));
        else if (_colossusDestroyed)
            args.AddLine(Loc.GetString("replicator-round-end-colossus-destroyed"));
    }

    private string GetLocation(EntityUid uid)
    {
        return FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(uid));
    }

    private void Announce(string message, params (string, object)[] args)
    {
        _chat.DispatchGlobalAnnouncement(Loc.GetString(message, args),
            Loc.GetString("comms-console-announcement-title-centcom"),
            colorOverride: Color.Red);
    }
}
