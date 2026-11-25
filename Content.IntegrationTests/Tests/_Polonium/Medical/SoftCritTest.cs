using System;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests.Helpers;
using Content.Server._Polonium.Body;
using Content.Server._Polonium.Medical.Cpr;
using Content.Server.Chat.Systems;
using Content.Shared._Polonium.Mobs;
using Content.Shared.ActionBlocker;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Cuffs;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Reflection;

namespace Content.IntegrationTests.Tests._Polonium.Medical;

/// <summary>
/// A human between 100 and 130 damage is in soft crit: critical, but conscious, able to crawl and whisper, but suffocating,
/// without the succumb actions. From 130 on it is in hard crit. Epinephrine lets a soft crit patient stand.
/// </summary>
[TestFixture]
public sealed class SoftCritTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";
    private static readonly EntProtoId SoftCritStand = "StatusEffectSoftCritStand";

    private MobStateSystem MobState => SEntMan.System<MobStateSystem>();
    private ActionBlockerSystem Blocker => SEntMan.System<ActionBlockerSystem>();

    private void Poisoned(EntityUid patient, int amount)
    {
        // Poison leaves no wound to bleed from, so the damage stays where it is set.
        var poison = Server.ResolveDependency<IPrototypeManager>().Index(Poison);
        SEntMan.System<DamageableSystem>().TryChangeDamage(patient, new DamageSpecifier(poison, FixedPoint2.New(amount)), ignoreResistances: true);
    }

    private int GrantedActions(EntityUid patient)
    {
        return SEntMan.GetComponent<MobStateActionsComponent>(patient).GrantedActions.Count;
    }

    [Test]
    public async Task SoftThenHardCrit()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var patient = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var item = SEntMan.SpawnAtPosition("Crowbar", map.GridCoords);
            Poisoned(patient, 110);

            Assert.Multiple(() =>
            {
                Assert.That(MobState.IsCritical(patient), "110 damage is crit.");
                Assert.That(MobState.IsSoftCritical(patient), "Below 130 it is soft crit.");
                Assert.That(MobState.IsIncapacitated(patient), "Soft crit counts as incapacitated: it suffocates and gets CPR.");
                Assert.That(Blocker.CanMove(patient), "Soft crit crawls.");
                Assert.That(Blocker.CanSpeak(patient), "Soft crit whispers.");
                Assert.That(Blocker.CanAttack(patient), Is.False, "Soft crit does not fight.");
                Assert.That(Blocker.CanDrop(patient), Is.False, "Soft crit handles no items.");
                Assert.That(Blocker.CanPickup(patient, item), Is.False, "Soft crit picks nothing up.");
                Assert.That(GrantedActions(patient), Is.Zero, "No succumb in soft crit.");
                var cuffCheck = new CheckIncapacitatedCuffEvent();
                SEntMan.EventBus.RaiseLocalEvent(patient, ref cuffCheck);
                Assert.That(cuffCheck.Incapacitated, "Soft crit is too weak to resist the cuffs.");
            });

            Poisoned(patient, 30);

            Assert.Multiple(() =>
            {
                Assert.That(MobState.IsSoftCritical(patient), Is.False, "140 damage is hard crit.");
                Assert.That(Blocker.CanMove(patient), Is.False);
                Assert.That(Blocker.CanSpeak(patient), Is.False);
                Assert.That(GrantedActions(patient), Is.GreaterThan(0), "Succumb comes with hard crit.");
            });
        });
    }

    [Test]
    public async Task HardCritHasHysteresis()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var patient = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Poisoned(patient, 140);
            Assert.That(MobState.IsSoftCritical(patient), Is.False, "Setup: 140 damage is hard crit.");

            Poisoned(patient, -12);
            Assert.That(MobState.IsSoftCritical(patient), Is.False, "128 after hard crit stays hard: the way back starts at 125.");

            Poisoned(patient, -5);
            Assert.That(MobState.IsSoftCritical(patient), "123 is soft crit again.");

            Poisoned(patient, 5);
            Assert.That(MobState.IsSoftCritical(patient), "128 coming from soft crit stays soft.");

            Poisoned(patient, 3);
            Assert.That(MobState.IsSoftCritical(patient), Is.False, "131 is hard crit.");
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task NoCprWhileAwake(bool stoppedHeart)
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            PatientIntakeMachineTest.AddAir(SEntMan, map.MapUid);
            var patient = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var rescuer = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            if (stoppedHeart)
            {
                var heart = SEntMan.GetComponent<BodyComponent>(patient).Organs!.ContainedEntities
                    .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == "Heart");
                Assert.That(SEntMan.System<BodyDebugSystem>().TryKillOrgan(patient, heart));
            }

            Poisoned(patient, 110 - (int) SEntMan.System<DamageableSystem>().GetTotalDamage(patient).Float());
            Assert.That(MobState.IsSoftCritical(patient), "Setup: soft crit.");

            Assert.That(SEntMan.System<CprSystem>().TryStart(rescuer, patient), Is.EqualTo(stoppedHeart),
                "Compressions only for a stopped heart while the patient is awake.");
        });
    }

    [Test]
    public async Task SoftCritSuffocates()
    {
        var map = await Pair.CreateTestMap();
        var patient = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            PatientIntakeMachineTest.AddAir(SEntMan, map.MapUid);
            patient = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Poisoned(patient, 110);
        });

        await Pair.RunSeconds(12);

        await Server.WaitAssertion(() =>
        {
            Assert.That(MobState.IsCritical(patient), "Setup: still in crit.");
            Assert.That(SEntMan.System<CirculationSystem>().ReadSpO2(patient), Is.LessThan(88f),
                "A patient in soft crit suffocates like in hard crit.");
        });
    }

    [Test]
    public async Task EpinephrineStandsSoftCritUp()
    {
        var map = await Pair.CreateTestMap();
        var patient = EntityUid.Invalid;
        var standing = SEntMan.System<StandingStateSystem>();

        await Server.WaitAssertion(() =>
        {
            patient = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Poisoned(patient, 110);
            Assert.That(standing.IsDown(patient), "Setup: soft crit lies down.");
            Assert.That(standing.Stand(patient), Is.False, "Without epinephrine soft crit cannot get up.");

            Assert.That(SEntMan.System<StatusEffectsSystem>()
                .TrySetStatusEffectDuration(patient, SoftCritStand, TimeSpan.FromSeconds(2)));
            Assert.That(standing.IsDown(patient), Is.False, "Epinephrine stands the patient up.");

            Poisoned(patient, 30);
            Assert.That(standing.IsDown(patient), "Hard crit falls even on epinephrine.");
        });
    }

    [Test]
    public async Task EpinephrineKeepsSoftCritStanding()
    {
        var map = await Pair.CreateTestMap();
        var patient = EntityUid.Invalid;
        var standing = SEntMan.System<StandingStateSystem>();

        await Server.WaitAssertion(() =>
        {
            PatientIntakeMachineTest.AddAir(SEntMan, map.MapUid);
            patient = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Poisoned(patient, 110);
            Assert.That(SEntMan.System<BloodstreamSystem>().TryAddToBloodstream(patient, new Solution("Epinephrine", 10)));
        });

        await Pair.RunSeconds(2);

        // The effect lasts two seconds and is renewed while the reagent metabolises, so the patient must not flicker down.
        for (var i = 0; i < 8; i++)
        {
            await Server.WaitAssertion(() =>
            {
                Assert.That(MobState.IsCritical(patient), "Setup: the patient is still in crit.");
                Assert.That(standing.IsDown(patient), Is.False, $"Standing on epinephrine, second {i + 2}.");
            });
            await Pair.RunSeconds(1);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SoftCritHasNoRadio(bool softCrit)
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var patient = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            SEntMan.EnsureComponent<TestListenerComponent>(patient);
            if (softCrit)
                Poisoned(patient, 110);

            var chat = SEntMan.System<ChatSystem>();
            chat.TrySendInGameICMessage(patient, $"{SharedChatSystem.RadioCommonPrefix} help", InGameICChatType.Speak, false);
            chat.TrySendInGameICMessage(patient, "help", InGameICChatType.Speak, false);

            // The listener system for speech is declared by DefibrillatorTest; a second one would subscribe twice.
            var listenerType = Server.Resolve<IReflectionManager>().GetAllChildren<TestListenerSystem<EntitySpokeEvent>>().Single();
            var listener = (TestListenerSystem<EntitySpokeEvent>)SEntMan.EntitySysManager.GetEntitySystem(listenerType);
            var spoken = listener.GetEvents(patient).ToList();
            Assert.That(spoken, Has.Count.EqualTo(2), "Both messages are said.");
            Assert.Multiple(() =>
            {
                Assert.That(spoken[0].Channel, softCrit ? Is.Null : Is.Not.Null, "Soft crit does not reach the radio.");
                Assert.That(spoken[1].ObfuscatedMessage, softCrit ? Is.Not.Null : Is.Null, "Soft crit only whispers.");
            });
        });
    }

    [Test]
    public async Task HardCritThresholdsFit()
    {
        var factory = SEntMan.ComponentFactory;

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var proto in Server.ProtoMan.EnumeratePrototypes<EntityPrototype>())
                {
                    if (proto.Abstract
                        || !proto.TryGetComponent<SoftCritComponent>(out var softCrit, factory)
                        || !proto.TryGetComponent<MobThresholdsComponent>(out var thresholds, factory))
                        continue;

                    var crit = thresholds.Thresholds.Where(pair => pair.Value == Content.Shared.Mobs.MobState.Critical).Select(pair => pair.Key).ToList();
                    var dead = thresholds.Thresholds.Where(pair => pair.Value == Content.Shared.Mobs.MobState.Dead).Select(pair => pair.Key).ToList();
                    if (crit.Count == 0 || dead.Count == 0)
                        continue;

                    Assert.That(softCrit.HardCritThreshold >= crit[0] && softCrit.HardCritThreshold < dead[0],
                        $"{proto.ID}: hard crit has to start between crit ({crit[0]}) and death ({dead[0]}).");
                }
            });
        });
    }
}
