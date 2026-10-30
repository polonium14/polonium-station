using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Polonium.Eggperson;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Eggperson;

/// <summary>
/// Jajoczłowiek z Aquili: surowe jajko jest kruche, usmażone twarde, a rozbite pęka na kawałki.
/// </summary>
[TestOf(typeof(EggShellSystem))]
public sealed class EggShellTest : InteractionTest
{
    private FixedPoint2 Brute(EntityUid uid)
    {
        var shell = SEntMan.GetComponent<EggShellComponent>(uid);
        return EggShellSystem.SumTypes(Server.System<DamageableSystem>().GetAllDamage(uid), shell.BruteTypes);
    }

    [Test]
    public async Task FriedEggIsHarder()
    {
        var egg = SEntMan.GetEntity(await Spawn("MobEggperson"));
        var damageable = Server.System<DamageableSystem>();

        await Server.WaitPost(() => damageable.TryChangeDamage(egg, new DamageSpecifier { DamageDict = { ["Blunt"] = 10 } }));
        var raw = Brute(egg);
        Assert.That((float) raw, Is.EqualTo(15f).Within(0.05f), "A raw egg should take 1.5x brute");

        await Server.WaitPost(() => damageable.TryChangeDamage(egg, new DamageSpecifier { DamageDict = { ["Heat"] = 80 } }));
        await Server.WaitPost(() => damageable.TryChangeDamage(egg, new DamageSpecifier { DamageDict = { ["Blunt"] = 10 } }));
        var fried = Brute(egg) - raw;
        Assert.That((float) fried, Is.EqualTo(4.875f).Within(0.05f), "A well fried egg should take about 0.49x brute");
    }

    [Test]
    public async Task EggShatters()
    {
        var egg = SEntMan.GetEntity(await Spawn("MobEggperson"));
        var damageable = Server.System<DamageableSystem>();

        await Server.WaitPost(() => damageable.TryChangeDamage(egg, new DamageSpecifier { DamageDict = { ["Blunt"] = 140 } }));
        await RunTicks(5);

        Assert.That(SEntMan.Deleted(egg) || SEntMan.IsQueuedForDeletion(egg), "The egg should shatter at 200 brute");
    }
}
