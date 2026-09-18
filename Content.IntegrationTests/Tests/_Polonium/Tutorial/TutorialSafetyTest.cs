using Content.IntegrationTests.Fixtures;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Tutorial;

/// <summary>
/// The nets that keep a run survivable. A trainee is handed real tools on a station nobody is going
/// to repair for them, so the tutorial refuses damage they should not be able to do - and the room
/// cast has to stay on its feet whatever is pointed at it.
/// </summary>
public sealed class TutorialSafetyTest : GameTest
{
    private const string Wall = "WallSolid";
    private const string Trainee = "MobHuman";
    private const string Slime = "TutorialSlime";
    private static readonly ProtoId<DamageTypePrototype> BluntDamage = "Blunt";

    [Test]
    public async Task TraineeCannotDamageAStructure()
    {
        var map = await Pair.CreateTestMap();
        var entMan = Server.ResolveDependency<IEntityManager>();
        var proto = Server.ResolveDependency<IPrototypeManager>();
        var damageable = entMan.System<DamageableSystem>();

        await Server.WaitAssertion(() =>
        {
            entMan.AddComponent<TutorialMapComponent>(map.MapUid);

            var wall = entMan.SpawnEntity(Wall, map.GridCoords);
            var trainee = entMan.SpawnEntity(Trainee, map.GridCoords);
            entMan.AddComponent<TutorialSessionComponent>(trainee);

            damageable.TryChangeDamage(wall, Blunt(proto, 50), origin: trainee);

            Assert.That(IsDamaged(entMan, damageable, wall), Is.False,
                "a trainee managed to damage a wall, the structure guard let it through");
        });
    }

    /// <summary>
    /// The control. The guard has to be tied to the session and not to the map, or the same wall
    /// would be indestructible for the maintenance crew of every other round.
    /// </summary>
    [Test]
    public async Task AnyoneElseStillDamagesStructures()
    {
        var map = await Pair.CreateTestMap();
        var entMan = Server.ResolveDependency<IEntityManager>();
        var proto = Server.ResolveDependency<IPrototypeManager>();
        var damageable = entMan.System<DamageableSystem>();

        await Server.WaitAssertion(() =>
        {
            var wall = entMan.SpawnEntity(Wall, map.GridCoords);
            var passerby = entMan.SpawnEntity(Trainee, map.GridCoords);

            damageable.TryChangeDamage(wall, Blunt(proto, 50), origin: passerby);

            Assert.That(IsDamaged(entMan, damageable, wall), Is.True,
                "the structure guard is catching damage from someone who is not in the tutorial");
        });
    }

    /// <summary>
    /// Room cast with <c>preventDeath</c> has to survive anything, including a magazine emptied into
    /// it, and has to still be alive a while later once bleeding and a crit have had their go: a
    /// corpse in the middle of a lesson leaves the step with nothing left to teach.
    /// </summary>
    [Test]
    public async Task ProtectedCastSurvivesLethalDamage()
    {
        var map = await Pair.CreateTestMap();
        var entMan = Server.ResolveDependency<IEntityManager>();
        var proto = Server.ResolveDependency<IPrototypeManager>();
        var damageable = entMan.System<DamageableSystem>();
        var mobs = entMan.System<MobStateSystem>();

        EntityUid urist = default;

        await Server.WaitAssertion(() =>
        {
            entMan.AddComponent<TutorialMapComponent>(map.MapUid);
            urist = entMan.SpawnEntity(Trainee, map.GridCoords);

            // the same thing a spawn or a claim does to a mob it hands a part to
            entMan.EnsureComponent<TutorialNpcComponent>(urist).PreventDeath = true;
        });

        await Server.WaitRunTicks(5);

        await Server.WaitAssertion(() =>
        {
            for (var shot = 0; shot < 20; shot++)
                damageable.TryChangeDamage(urist, Blunt(proto, 100), ignoreResistances: true);

            Assert.That(mobs.IsDead(urist), Is.False, "a protected tutorial NPC died on the spot");
        });

        // ten seconds of whatever a body in crit does to itself
        await Server.WaitRunTicks(300);

        await Server.WaitAssertion(() =>
        {
            Assert.That(mobs.IsDead(urist), Is.False,
                "a protected tutorial NPC survived the hits and then died on its own afterwards");
        });
    }

    /// <summary>
    /// The slime is the one lesson prop that is meant to die - the step ends when it does. If it were
    /// ever marked protected like the rest of the cast, the room 14 exam could not be passed.
    /// </summary>
    [Test]
    public async Task LessonSlimeCanBeKilled()
    {
        var map = await Pair.CreateTestMap();
        var entMan = Server.ResolveDependency<IEntityManager>();
        var proto = Server.ResolveDependency<IPrototypeManager>();
        var damageable = entMan.System<DamageableSystem>();
        var mobs = entMan.System<MobStateSystem>();

        EntityUid slime = default;

        await Server.WaitAssertion(() =>
        {
            entMan.AddComponent<TutorialMapComponent>(map.MapUid);
            slime = entMan.SpawnEntity(Slime, map.GridCoords);

            Assert.That(entMan.HasComponent<MobStateComponent>(slime), Is.True, "the slime is not a mob at all");
        });

        await Server.WaitRunTicks(5);

        await Server.WaitAssertion(() =>
        {
            for (var shot = 0; shot < 20 && !mobs.IsDead(slime); shot++)
                damageable.TryChangeDamage(slime, Blunt(proto, 100), ignoreResistances: true);

            Assert.That(mobs.IsDead(slime), Is.True,
                "the lesson slime cannot be killed, so the step that ends with its death never ends");
        });
    }

    /// <summary>
    /// Nothing a trainee is handed may open a hole in the hull. The pane is a structure like any
    /// other, but it is the one every trainee tries first.
    /// </summary>
    [Test]
    public async Task TraineeCannotBreakAWindow()
    {
        var map = await Pair.CreateTestMap();
        var entMan = Server.ResolveDependency<IEntityManager>();
        var proto = Server.ResolveDependency<IPrototypeManager>();
        var damageable = entMan.System<DamageableSystem>();

        await Server.WaitAssertion(() =>
        {
            entMan.AddComponent<TutorialMapComponent>(map.MapUid);

            var window = entMan.SpawnEntity("Window", map.GridCoords);
            var trainee = entMan.SpawnEntity(Trainee, map.GridCoords);
            entMan.AddComponent<TutorialSessionComponent>(trainee);

            damageable.TryChangeDamage(window, Blunt(proto, 200), origin: trainee);

            Assert.That(IsDamaged(entMan, damageable, window), Is.False,
                "a trainee put a crack in a window, which is a hole in the hull one step later");
        });
    }

    private static bool IsDamaged(IEntityManager entMan, DamageableSystem damageable, EntityUid uid)
    {
        var comp = entMan.GetComponent<DamageableComponent>(uid);
        return damageable.TryGetDamageGreaterThan((uid, comp), FixedPoint2.Zero, out _);
    }

    private static DamageSpecifier Blunt(IPrototypeManager proto, int amount)
    {
        return new DamageSpecifier(proto.Index(BluntDamage), FixedPoint2.New(amount));
    }
}
