using Content.Shared._Polonium.Power.Generation.FissionGenerator;
using Content.Shared.Audio;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Radiation.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Power.Generation.FissionGenerator;

public sealed partial class CoriumSystem : SharedCoriumSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedRadiationSystem _radiation = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly TimeSpan BurnInterval = TimeSpan.FromSeconds(1);

    private readonly HashSet<Entity<MobStateComponent>> _onTile = new();
    private readonly HashSet<EntityUid> _burned = new();
    private TimeSpan _nextBurn;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CoriumComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CoriumComponent> ent, ref MapInitEvent args)
    {
        SetStage(ent, ent.Comp.Stage);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CoriumComponent>();
        while (query.MoveNext(out var uid, out var corium))
        {
            if (corium.Stage == CoriumStage.Solid || now < corium.NextStage)
                continue;

            SetStage((uid, corium), corium.Stage + 1);
        }

        if (now < _nextBurn)
            return;

        _nextBurn = now + BurnInterval;
        Burn();
    }

    private void Burn()
    {
        // A mob touching several hot tiles still burns once per interval.
        _burned.Clear();

        var query = EntityQueryEnumerator<CoriumComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var corium, out var xform))
        {
            if (!corium.Stages.TryGetValue(corium.Stage, out var stage) || stage.Damage is not { } damage)
                continue;

            if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
                continue;

            var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
            _onTile.Clear();
            _lookup.GetLocalEntitiesIntersecting(grid, tile, _onTile, flags: LookupFlags.Dynamic, gridComp: gridComp);

            foreach (var mob in _onTile)
            {
                if (_burned.Add(mob) && _damageable.TryChangeDamage(mob.Owner, damage, interruptsDoAfters: false, origin: uid))
                    _audio.PlayPvs(corium.BurnSound, mob);
            }
        }
    }

    private void SetStage(Entity<CoriumComponent> ent, CoriumStage stage)
    {
        ent.Comp.Stage = stage;
        Dirty(ent);

        if (!ent.Comp.Stages.TryGetValue(stage, out var data))
            return;

        ent.Comp.NextStage = _timing.CurTime + data.Duration;
        _radiation.SetIntensity(ent.Owner, data.Radiation);
        _ambient.SetAmbience(ent, data.Bubbling);

        var light = _light.EnsureLight(ent);
        _light.SetColor(ent, data.LightColor, light);
        _light.SetEnergy(ent, data.LightEnergy, light);
        _light.SetRadius(ent, data.LightRadius, light);
    }
}
