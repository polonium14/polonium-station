using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Power.Generation.FissionGenerator;
using Content.Shared._Polonium.Power.Generation.FissionGenerator;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Power;

[TestFixture]
[TestOf(typeof(CoriumSystem))]
public sealed class CoriumTest : GameTest
{
    private const string CoriumProto = "Corium";
    private const string ChunkProto = "CoriumChunk";
    private const string MobProto = "MobHuman";

    private static readonly ProtoId<DamageTypePrototype> Structural = "Structural";
    private static readonly ProtoId<DamageTypePrototype> Heat = "Heat";

    public override PoolSettings PoolSettings => new() { Dirty = true };

    [Test]
    public async Task MoltenCoriumBurnsAndSolidCoriumBreaksIntoChunk()
    {
        var server = Pair.Server;
        var entMan = server.EntMan;
        var map = await Pair.CreateTestMap();
        var corium = EntityUid.Invalid;
        var mob = EntityUid.Invalid;
        EntityCoordinates coords = default;

        await server.WaitPost(() =>
        {
            var mapSystem = entMan.System<SharedMapSystem>();
            coords = mapSystem.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero);
            corium = entMan.SpawnEntity(CoriumProto, coords);
            mob = entMan.SpawnEntity(MobProto, coords);
        });

        await Pair.RunSeconds(3);

        var hit = default(DamageSpecifier);
        await server.WaitAssertion(() =>
        {
            var damageable = entMan.System<DamageableSystem>();
            Assert.That(damageable.GetAllDamage(mob).DamageDict.TryGetValue(Heat, out var heat) && heat > FixedPoint2.Zero,
                "Molten corium didn't burn the mob standing on it.");

            // Enough to break solid corium, but below the overkill threshold that deletes it without loot.
            hit = new DamageSpecifier(server.ProtoMan.Index(Structural), FixedPoint2.New(25));
            damageable.TryChangeDamage(corium, hit);
        });
        await RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(corium), "Molten corium was broken apart.");
            Assert.That(entMan.System<DamageableSystem>().GetTotalDamage(corium), Is.EqualTo(FixedPoint2.Zero));
        });

        // Skip ahead through the crust to the solid stage, one stage per tick.
        foreach (var _ in Enumerable.Range(0, 2))
        {
            await server.WaitPost(() => entMan.GetComponent<CoriumComponent>(corium).NextStage = TimeSpan.Zero);
            await RunTicksSync(1);
        }

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<CoriumComponent>(corium).Stage, Is.EqualTo(CoriumStage.Solid));
            entMan.System<DamageableSystem>().TryChangeDamage(corium, hit!);
        });
        await RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(corium), Is.False, "Solid corium didn't break.");

            var lookup = entMan.System<EntityLookupSystem>();
            var chunks = lookup.GetEntitiesInRange(coords, 1f)
                .Where(e => entMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID == ChunkProto);
            Assert.That(chunks.Count(), Is.EqualTo(1), "Breaking solid corium didn't drop exactly one chunk.");
        });
    }
}
