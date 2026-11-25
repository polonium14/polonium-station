#nullable enable
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Horror;
using Content.Shared._Polonium.Chat;
using Content.Shared._Polonium.Horror;
using Content.Shared._Polonium.Horror.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Ghost.Roles.Components;
using Content.Server.Objectives.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Objectives.Systems;
using Content.Shared.Roles;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Horror;

/// <summary>
/// Mechaniki pradawnego horroru: pożeranie duszy, mutacje, cicha rozmowa z nosicielem i macka.
/// </summary>
[TestOf(typeof(EldritchHorrorSystem))]
public sealed class EldritchHorrorMechanicsTest : GameTest
{
    private static readonly ProtoId<HorrorChemicalPrototype> Ephedrine = "HorrorEphedrine";

    private EntityUid _horror;
    private EntityUid _human;

    private EldritchHorrorSystem Horror => SEntMan.System<EldritchHorrorSystem>();

    private Entity<EldritchHorrorComponent> HorrorEnt => (_horror, SComp<EldritchHorrorComponent>(_horror));

    private async Task SetupInfested()
    {
        var map = await Pair.CreateTestMap();
        var coords = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));

        await Pair.Server.WaitPost(() =>
        {
            _horror = SSpawnAtPosition("MobEldritchHorror", coords);
            _human = SSpawnAtPosition("MobHuman", coords.Offset(new Vector2(1f, 0f)));

            var mindSys = SEntMan.System<SharedMindSystem>();
            mindSys.TransferTo(mindSys.CreateMind(null, "horror"), _horror);
            mindSys.TransferTo(mindSys.CreateMind(null, "host"), _human);
            Assert.That(Horror.Infest(HorrorEnt, _human), Is.True);
        });
    }

    [Test]
    public async Task DevouringTheTargetSoulGivesAPoint()
    {
        await SetupInfested();
        EntityUid hostMind = default;
        EntityUid objective = default;

        await Pair.Server.WaitPost(() =>
        {
            var mindSys = SEntMan.System<SharedMindSystem>();
            Assert.That(mindSys.TryGetMind(_human, out hostMind, out _), Is.True);
            Assert.That(mindSys.TryGetMind(_horror, out var horrorMind, out var horrorMindComp), Is.True);
            SEntMan.System<SharedRoleSystem>().MindAddRole(horrorMind, "MindRoleEldritchHorror", horrorMindComp, true);
            Assert.That(SEntMan.System<SharedObjectivesSystem>().TryCreateObjective((horrorMind, horrorMindComp!), "HorrorDevourObjective", out var created), Is.True);
            objective = created!.Value;
            mindSys.AddObjective(horrorMind, horrorMindComp!, objective);

            var comp = SComp<EldritchHorrorComponent>(_horror);
            comp.ConsumeTime = TimeSpan.FromSeconds(0.5);
            Horror.SetSoulTarget(HorrorEnt, hostMind);

            var ev = new HorrorConsumeSoulActionEvent { Performer = _horror };
            SEntMan.EventBus.RaiseLocalEvent(_horror, ev);
            Assert.That(ev.Handled, Is.True, "Pożeranie duszy się nie zaczęło");
        });

        await RunSeconds(2);

        await Pair.Server.WaitAssertion(() =>
        {
            var comp = SComp<EldritchHorrorComponent>(_horror);
            Assert.That(comp.ConsumedSouls, Is.EqualTo(1));
            Assert.That(comp.SoulPoints, Is.EqualTo(5));
            Assert.That(comp.SoulTarget, Is.Null);
            Assert.That(SEntMan.HasComponent<HorrorSoullessComponent>(hostMind), Is.True);
            Assert.That(SComp<CounterConditionComponent>(objective).Count, Is.EqualTo(1), "Cel pożerania dusz nie policzył duszy");
        });
    }

    [Test]
    public async Task CarrierWakesOnceIntoAGhostRole()
    {
        var map = await Pair.CreateTestMap();
        var coords = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));

        await Pair.Server.WaitAssertion(() =>
        {
            var carrier = SSpawnAtPosition("HorrorPetCarrier", coords);
            var user = SSpawnAtPosition("MobHuman", coords);
            Assert.That(SEntMan.HasComponent<GhostRoleComponent>(carrier), Is.False, "Śpiący horror nie powinien być od razu rolą ducha");

            var use = new UseInHandEvent(user);
            SEntMan.EventBus.RaiseLocalEvent(carrier, use);
            Assert.That(SEntMan.HasComponent<GhostRoleComponent>(carrier), Is.True);
            Assert.That(SEntMan.HasComponent<GhostRoleMobSpawnerComponent>(carrier), Is.True);
            Assert.That(SComp<HorrorCarrierComponent>(carrier).Woken, Is.True);
        });
    }

    [Test]
    public async Task UpgradesAndAbilitiesCostSouls()
    {
        await SetupInfested();

        await Pair.Server.WaitAssertion(() =>
        {
            var comp = SComp<EldritchHorrorComponent>(_horror);
            comp.Chemicals = 200;

            Assert.That(Horror.TryInject(HorrorEnt, SProtoMan.Index(Ephedrine)), Is.False);
            Assert.That(Horror.TryBuyUpgrade(HorrorEnt, "HorrorUpgradeAdvancedChemicals"), Is.True);
            Assert.That(Horror.TryInject(HorrorEnt, SProtoMan.Index(Ephedrine)), Is.True, "Efedryna po ulepszeniu nadal zablokowana");

            Assert.That(Horror.TryBuyAbility(HorrorEnt, "ActionHorrorTentacle"), Is.True);
            Assert.That(comp.SoulPoints, Is.EqualTo(0), "4 punkty na start: synteza 2 + macka 2");
            Assert.That(Horror.TryBuyAbility(HorrorEnt, "ActionHorrorViewMemory"), Is.False, "Zakup bez punktów");
            Assert.That(comp.ActionEntities.ContainsKey("ActionHorrorTentacle"), Is.True, "Macka nie trafiła na pasek akcji");
        });
    }

    [Test]
    public async Task HorrorInHeadTalksOnlyToHost()
    {
        await SetupInfested();

        await Pair.Server.WaitAssertion(() =>
        {
            var fromHorror = new PoloniumSpeechRedirectEvent("Słyszysz mnie?");
            SEntMan.EventBus.RaiseLocalEvent(_horror, ref fromHorror);
            Assert.That(fromHorror.Handled, Is.True, "Horror w głowie mówi na głos");

            var fromHost = new PoloniumSpeechRedirectEvent("Kto tam?");
            SEntMan.EventBus.RaiseLocalEvent(_human, ref fromHost);
            Assert.That(fromHost.Handled, Is.False, "Nosiciel bez przełącznika nie powinien mówić w myślach");

            SComp<EldritchHorrorInfestedComponent>(_human).TalkToHorror = true;
            fromHost = new PoloniumSpeechRedirectEvent("Kto tam?");
            SEntMan.EventBus.RaiseLocalEvent(_human, ref fromHost);
            Assert.That(fromHost.Handled, Is.True);
        });
    }

    [Test]
    public async Task TentacleGrowsAndGoesAwayWithTheHorror()
    {
        await SetupInfested();
        var hands = SEntMan.System<SharedHandsSystem>();

        await Pair.Server.WaitPost(() =>
        {
            var comp = SComp<EldritchHorrorComponent>(_horror);
            comp.Chemicals = 100;
            var ev = new HorrorTentacleActionEvent { Performer = _horror };
            SEntMan.EventBus.RaiseLocalEvent(_horror, ev);

            Assert.That(hands.EnumerateHeld(_human).Any(SEntMan.HasComponent<HorrorTentacleComponent>), Is.True, "Nosiciel nie trzyma macki");
            Assert.That(comp.Chemicals, Is.EqualTo(100 - comp.TentacleCost));

            Assert.That(Horror.TryLeaveHost(HorrorEnt), Is.True);
        });

        await RunTicksSync(2);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(hands.EnumerateHeld(_human).Any(SEntMan.HasComponent<HorrorTentacleComponent>), Is.False, "Macka została po wyjściu horroru");
        });
    }
}
