using Content.Shared._ES.Sparks.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Physics;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Throwing;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._ES.Sparks;

// Polonium: ported from Starlight without the ignition and tile fires, the sparks are only visual
public sealed partial class ESSparksSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    [Dependency] private EntityQuery<TransformComponent> _transformQuery = default!;

    public static readonly EntProtoId DefaultSparks = "ESEffectSparks";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ESSparkOnHitComponent, DamageChangedEvent>(OnDamaged);
    }

    private void OnDamaged(Entity<ESSparkOnHitComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta is null)
            return;

        if (args.DamageDelta.GetTotal() < ent.Comp.Threshold)
            return;

        DoSparks(ent, user: args.Origin);
    }

    /// <summary>
    /// Variant of <see cref="DoSparks(EntityUid, int, Nullable{EntProtoId}, Nullable{EntityUid}, bool)"/> that takes
    /// the configuration from a base component rather than being passed in as args
    /// </summary>
    /// <param name="ent">Entity that the sparks are originating from. Additionally, holds YAML configuration for spark effect</param>
    /// <param name="user">A "user" who triggered the sparks</param>
    /// <param name="cooldown">If true, will check the cooldown on <see cref="ESSparkCooldownComponent"/> before spawning sparks</param>
    public void DoSparks<T>(
        Entity<T> ent,
        EntityUid? user = null,
        bool cooldown = true)
        where T : ESBaseSparkConfigurationComponent
    {
        if (_net.IsClient || !_random.Prob(ent.Comp.Prob))
            return;

        SharedApcPowerReceiverComponent? powerReceiver = null;
        if (_powerReceiver.ResolveApc(ent, ref powerReceiver) &&
            (!_powerReceiver.IsPowered((ent, powerReceiver)) || powerReceiver.Load <= 0))
            return;

        DoSparks(ent,
            number: ent.Comp.Count,
            ent.Comp.SparkPrototype,
            user: user,
            cooldown: cooldown);
    }

    /// <summary>
    /// Spawns sparks originating from a target entity
    /// </summary>
    /// <param name="source">Entity that the sparks are originating from</param>
    /// <param name="number">Number of sparks to spawn</param>
    /// <param name="sparksPrototype">Spark prototype to use. Defaults to <see cref="DefaultSparks"/></param>
    /// <param name="user">A "user" who triggered the sparks</param>
    /// <param name="cooldown">If true, will check the cooldown on <see cref="ESSparkCooldownComponent"/> before spawning sparks</param>
    [PublicAPI]
    public void DoSparks(
        EntityUid source,
        int number = 4,
        EntProtoId? sparksPrototype = null,
        EntityUid? user = null,
        bool cooldown = true)
    {
        if (_net.IsClient)
            return;

        // track last spark time
        var comp = EnsureComp<ESSparkCooldownComponent>(source);
        if (cooldown && _timing.CurTime - comp.LastSparkTime < comp.SparkDelay)
            return;
        comp.LastSparkTime = _timing.CurTime;

        DoSparks(Transform(source).Coordinates, number, sparksPrototype, user, source);
    }

    /// <summary>
    /// Spawns sparks at a given set of coordinates
    /// </summary>
    /// <param name="coordinates">Where the sparks should spawn</param>
    /// <param name="number">Number of sparks to spawn</param>
    /// <param name="sparksPrototype">Spark prototype to use. Defaults to <see cref="DefaultSparks"/></param>
    /// <param name="user">A "user" who triggered the sparks</param>
    /// <param name="ignored">An entity whose collision will be ignored by the sparks</param>
    [PublicAPI]
    public void DoSparks(
        EntityCoordinates coordinates,
        int number = 4,
        EntProtoId? sparksPrototype = null,
        EntityUid? user = null,
        EntityUid? ignored = null)
    {
        if (_net.IsClient)
            return;

        sparksPrototype ??= DefaultSparks;

        var angleDelta = (Angle) (MathF.Tau / number);
        var angle = _random.NextAngle();
        for (var i = 0; i < number; i++)
        {
            var sparks = Spawn(sparksPrototype, _transform.ToMapCoordinates(coordinates), rotation: angle);
            angle += angleDelta;

            // Moff start - Guard against intermittent test failure where throwing `throw`s :^) because of a lack of
            // transform component on the sparks. I have no idea how a newly spawned entity can sometimes have a transform and sometimes not.
            if (!_transformQuery.HasComp(sparks))
            {
                Log.Warning($"Skipping throwing sparks for {ToPrettyString(sparks)}, as it lacks a {nameof(TransformComponent)}");
                continue;
            }
            // Moff end

            _throwing.TryThrow(sparks, angle.ToVec(), 2f, animated: false);

            if (ignored is { } ignoredUid && !TerminatingOrDeleted(ignoredUid))
            {
                var preventCollide = EnsureComp<PreventCollideComponent>(sparks);
                preventCollide.Uid = ignoredUid;
                Dirty(sparks, preventCollide);
            }
        }
    }
}
