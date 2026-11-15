using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Body;
using Content.Shared._Polonium.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared.Body;
using Content.Shared.Chat;
using Content.Shared.Eye.Blinding.Components;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Body;

/// <summary>
/// Damaged eyes blur sight, a damaged tongue lisps in Polish, damaged or missing ears garble what is heard,
/// and a damaged brain weakens all of them.
/// </summary>
[TestFixture]
public sealed class SensesTest : GameTest
{
    private OrganHealingSystem Healing => SEntMan.System<OrganHealingSystem>();

    private EntityUid Organ(EntityUid body, string category)
    {
        return SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
            .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == category);
    }

    private void Wear(EntityUid body, string category, float fraction)
    {
        var organ = Organ(body, category);
        var integrity = SEntMan.GetComponent<OrganIntegrityComponent>(organ);
        Healing.DamageOrgan((organ, integrity), integrity.IntegrityCap * (1f - fraction));
    }

    private void Heal(EntityUid body, string category)
    {
        var organ = Organ(body, category);
        var integrity = SEntMan.GetComponent<OrganIntegrityComponent>(organ);
        Healing.HealOrgan((organ, integrity), integrity.IntegrityCap);
    }

    private ListenerHearsMessageEvent Hear(EntityUid listener, ChatChannel channel, float distance, string message)
    {
        var heard = new ListenerHearsMessageEvent(channel, distance, message, $"Someone says, \"{message}\"");
        SEntMan.EventBus.RaiseLocalEvent(listener, ref heard);
        return heard;
    }

    [Test]
    public async Task DamagedEyesBlurAndHeal()
    {
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Wear(body, "Eyes", 0.5f);
        });

        await Pair.RunTicksSync(3);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<BlindableComponent>(body).MinDamage, Is.Zero, "Above 35% the eyes see fine.");
            Wear(body, "Eyes", 0.7f); // 50% - 30% = 20% left
        });

        await Pair.RunTicksSync(3);

        await Server.WaitAssertion(() =>
        {
            var blindable = SEntMan.GetComponent<BlindableComponent>(body);
            Assert.Multiple(() =>
            {
                Assert.That(blindable.MinDamage, Is.InRange(1, blindable.MaxDamage - 1), "Blurry, not blind.");
                Assert.That(blindable.IsBlind, Is.False);
            });

            Heal(body, "Eyes");
        });

        await Pair.RunTicksSync(3);

        await Server.WaitAssertion(() =>
        {
            var blindable = SEntMan.GetComponent<BlindableComponent>(body);
            Assert.That(blindable.MinDamage, Is.Zero);
            Assert.That(blindable.EyeDamage, Is.Zero, "Healed eyes see sharp again.");
        });
    }

    [Test]
    public async Task DamagedBrainWeakensSenses()
    {
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Wear(body, "Brain", 0.3f);
        });

        await Pair.RunTicksSync(3);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<BlindableComponent>(body).MinDamage, Is.GreaterThan(0), "Healthy eyes, damaged brain: blurry.");
                Assert.That(SEntMan.GetComponent<HearingLossComponent>(body).Garble, Is.GreaterThan(0f));
                Assert.That(SEntMan.GetComponent<PolishLispComponent>(body).Chance, Is.EqualTo(0.7f).Within(0.02f));
            });
        });
    }

    [Test]
    public async Task DamagedTongueLispsInPolish()
    {
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(SEntMan.HasComponent<PolishLispComponent>(body), Is.False, "A healthy tongue does not lisp.");
            Wear(body, "Tongue", 0.001f);
        });

        await Pair.RunTicksSync(3);

        await Server.WaitAssertion(() =>
        {
            var lisp = SEntMan.GetComponent<PolishLispComponent>(body);
            Assert.That(lisp.Chance, Is.GreaterThan(0.99f));

            var accented = SEntMan.System<PolishLispSystem>().Accentuate("Czy słyszysz mnie? Rzeka, żaba, dżem. SZYBKO!", (body, lisp));
            Assert.That(accented, Is.EqualTo("Cy słysys mnie? Zeka, zaba, dzem. SYBKO!"));
        });
    }

    [Test]
    public async Task EarsGarbleWhatIsHeard()
    {
        var map = await Pair.CreateTestMap();
        var deaf = EntityUid.Invalid;
        var hard = EntityUid.Invalid;
        var healthy = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            deaf = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            hard = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            healthy = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(SEntMan.System<BodyDebugSystem>().TryKillOrgan(deaf, Organ(deaf, "Ears")));
            Wear(hard, "Ears", 0.1f);
        });

        await Pair.RunTicksSync(3);

        await Server.WaitAssertion(() =>
        {
            var message = new string('a', 400);
            int Lost(ListenerHearsMessageEvent heard) => heard.Message.Count(c => c == '~');

            var close = Hear(deaf, ChatChannel.Local, 1f, message);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<HearingLossComponent>(deaf).Deaf);
                Assert.That(Lost(close), Is.InRange(140, 260), "Right next to a deaf listener half the letters get through.");
                Assert.That(close.WrappedMessage, Does.Contain(close.Message), "The chat line shows the garbled text.");
                Assert.That(Lost(Hear(deaf, ChatChannel.Local, 4f, message)), Is.EqualTo(400), "From 4 tiles on nothing.");
                Assert.That(Lost(Hear(deaf, ChatChannel.Radio, 0f, message)), Is.InRange(140, 260), "Radio half-way.");
                Assert.That(Lost(Hear(hard, ChatChannel.Local, 5f, message)), Is.InRange(1, 399), "Hard of hearing: some letters lost.");
                Assert.That(Lost(Hear(healthy, ChatChannel.Local, 5f, message)), Is.Zero);
                Assert.That(Lost(Hear(deaf, ChatChannel.Emotes, 1f, message)), Is.Zero, "Emotes are seen, not heard.");
            });
        });
    }
}
