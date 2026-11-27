#nullable enable
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Horror;
using Content.Shared._Polonium.Horror;
using Content.Shared._Polonium.Horror.Components;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mind;
using Content.Shared.Mindshield.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Horror;

/// <summary>
/// Pradawny horror: wejście do głowy, chemikalia, wyjście, przejęcie ciała, opór, wyrzut po trafieniu i operacja.
/// </summary>
[TestOf(typeof(EldritchHorrorSystem))]
public sealed class EldritchHorrorCoreTest : GameTest
{
    private static readonly ProtoId<HorrorChemicalPrototype> Bicaridine = "HorrorBicaridine";
    private static readonly ProtoId<HorrorChemicalPrototype> Ephedrine = "HorrorEphedrine";

    private EntityCoordinates _coords;
    private EntityUid _horror;
    private EntityUid _human;

    private EldritchHorrorSystem Horror => SEntMan.System<EldritchHorrorSystem>();

    private Entity<EldritchHorrorComponent> HorrorEnt => (_horror, SComp<EldritchHorrorComponent>(_horror));

    private async Task SetupInfested(bool minds)
    {
        var map = await Pair.CreateTestMap();
        _coords = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));

        await Pair.Server.WaitPost(() =>
        {
            _horror = SSpawnAtPosition("MobEldritchHorror", _coords);
            _human = SSpawnAtPosition("MobHuman", _coords.Offset(new Vector2(1f, 0f)));

            if (minds)
            {
                var mindSys = SEntMan.System<SharedMindSystem>();
                mindSys.TransferTo(mindSys.CreateMind(null, "horror"), _horror);
                mindSys.TransferTo(mindSys.CreateMind(null, "host"), _human);
            }

            Assert.That(Horror.Infest(HorrorEnt, _human), Is.True);
        });
    }

    private EntityUid? MindOf(EntityUid uid)
    {
        return SEntMan.System<SharedMindSystem>().TryGetMind(uid, out var mind, out _) ? mind : null;
    }

    [Test]
    public async Task InfestInjectAndLeave()
    {
        await SetupInfested(false);

        await Pair.Server.WaitAssertion(() =>
        {
            var comp = SComp<EldritchHorrorComponent>(_horror);
            Assert.That(comp.Host, Is.EqualTo(_human));
            Assert.That(SEntMan.HasComponent<EldritchHorrorInfestedComponent>(_human), Is.True);

            comp.Chemicals = 100;
            var proto = SProtoMan.Index(Bicaridine);
            Assert.That(Horror.TryInject(HorrorEnt, proto), Is.True, "Wstrzyknięcie bikarydyny się nie udało");
            Assert.That(comp.Chemicals, Is.EqualTo(100 - proto.Cost));

            // efedryna wymaga zaawansowanej syntezy
            Assert.That(Horror.TryInject(HorrorEnt, SProtoMan.Index(Ephedrine)), Is.False);

            Assert.That(Horror.TryLeaveHost(HorrorEnt), Is.True);
            Assert.That(comp.Host, Is.Null);
        });

        await RunTicksSync(1);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<EldritchHorrorInfestedComponent>(_human), Is.False);
        });
    }

    [Test]
    public async Task ControlSwapsMindsAndHitGivesBodyBack()
    {
        await SetupInfested(true);
        EntityUid? horrorMind = null;
        EntityUid? hostMind = null;

        await Pair.Server.WaitPost(() =>
        {
            horrorMind = MindOf(_horror);
            hostMind = MindOf(_human);
            Assert.That(Horror.CanControl(HorrorEnt, _human, out _), Is.True);
            Horror.TakeControl(HorrorEnt, _human);
        });

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(SComp<EldritchHorrorComponent>(_horror).Controlling, Is.True);
            Assert.That(MindOf(_human), Is.EqualTo(horrorMind), "Horror nie siedzi w ciele nosiciela");

            var captive = SComp<EldritchHorrorInfestedComponent>(_human).CaptiveMindContainer.ContainedEntity;
            Assert.That(captive, Is.Not.Null);
            Assert.That(MindOf(captive!.Value), Is.EqualTo(hostMind), "Umysł nosiciela nie trafił do uwięzionego umysłu");

            var damage = new DamageSpecifier(SProtoMan.Index<Content.Shared.Damage.Prototypes.DamageTypePrototype>("Blunt"), 30);
            SEntMan.System<DamageableSystem>().TryChangeDamage(_human, damage, true);
        });

        await RunTicksSync(2);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(SComp<EldritchHorrorComponent>(_horror).Controlling, Is.False, "Trafienie nie wyrzuciło horroru");
            Assert.That(MindOf(_human), Is.EqualTo(hostMind));
            Assert.That(MindOf(_horror), Is.EqualTo(horrorMind));
        });
    }

    [Test]
    public async Task ResistGivesBodyBack()
    {
        await SetupInfested(true);
        EntityUid? hostMind = null;

        await Pair.Server.WaitPost(() =>
        {
            hostMind = MindOf(_human);
            Horror.TakeControl(HorrorEnt, _human);
            var captive = SComp<EldritchHorrorInfestedComponent>(_human).CaptiveMindContainer.ContainedEntity!.Value;
            // the resist attempt takes 20-30 s, here it ends right away
            SComp<HorrorCaptiveMindComponent>(captive).ResistEnd = Pair.Server.Timing.CurTime;
        });

        await RunTicksSync(2);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(SComp<EldritchHorrorComponent>(_horror).Controlling, Is.False);
            Assert.That(MindOf(_human), Is.EqualTo(hostMind));
        });
    }

    [Test]
    public async Task MindshieldBlocksControl()
    {
        await SetupInfested(true);

        await Pair.Server.WaitAssertion(() =>
        {
            SEntMan.AddComponent<MindShieldComponent>(_human);
            Assert.That(Horror.CanControl(HorrorEnt, _human, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("horror-control-mindshield"));
        });
    }

    [Test]
    public async Task RemovalSurgeryStepPullsHorrorOut()
    {
        await SetupInfested(false);

        await Pair.Server.WaitPost(() =>
        {
            Assert.That(LimbTargetMap.TryGetOrganByCategory(SEntMan, SComp<BodyComponent>(_human), "Head", out var head), Is.True);
            var surgeon = SSpawnAtPosition("MobHuman", _coords);
            var surgery = SSpawnAtPosition("SurgeryRemoveEldritchHorror", _coords);
            var step = SSpawnAtPosition("SurgeryStepRemoveEldritchHorror", _coords);
            var ev = new SurgeryStepEvent(surgeon, _human, head, EntityUid.Invalid, surgery, step);
            SEntMan.EventBus.RaiseLocalEvent(step, ref ev);
        });

        await RunTicksSync(1);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(SComp<EldritchHorrorComponent>(_horror).Host, Is.Null, "Operacja nie wyjęła horroru");
            Assert.That(SEntMan.HasComponent<EldritchHorrorInfestedComponent>(_human), Is.False);
        });
    }
}
