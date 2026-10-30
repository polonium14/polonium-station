using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Polonium.Medical.OperatingTable;
using Content.Shared.Buckle;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Verbs;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Medical;

[TestFixture]
[TestOf(typeof(OperatingTableLightSystem))]
public sealed class OperatingTableLightTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";

    private EntityUid _table;
    private EntityUid _patient;
    private EntityUid _user;

    private async Task SetUp()
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        await Server.WaitPost(() =>
        {
            _table = SEntMan.SpawnEntity("OperatingTable", coords);
            _patient = SEntMan.SpawnEntity("MobHuman", coords);
            _user = SEntMan.SpawnEntity("MobHuman", coords);
        });

        await Pair.RunTicksSync(5);
    }

    private async Task Buckle()
    {
        await Server.WaitPost(() =>
            Assert.That(SEntMan.System<SharedBuckleSystem>().TryBuckle(_patient, null, _table, popup: false),
                "Sanity check: the patient should buckle to the table."));
        await Pair.RunTicksSync(5);
    }

    private async Task Damage(int amount)
    {
        await Server.WaitPost(() =>
        {
            var damage = new DamageSpecifier(SProtoMan.Index(Blunt), FixedPoint2.New(amount));
            SEntMan.System<DamageableSystem>().TryChangeDamage(_patient, damage, ignoreResistances: true);
        });
        await Pair.RunTicksSync(5);
    }

    private bool LightEnabled()
    {
        Assert.That(SEntMan.System<SharedPointLightSystem>().TryGetLight(_table, out var light), "The table should have a light.");
        return light!.Enabled;
    }

    private VitalsState Vitals()
    {
        var appearance = SEntMan.System<SharedAppearanceSystem>();
        Assert.That(appearance.TryGetData<VitalsState>(_table, OperatingTableVisuals.VitalsState, out var state),
            "The table should have published its vitals state.");
        return state;
    }

    [Test]
    public async Task LightVerbTogglesTheLight()
    {
        await SetUp();

        await Server.WaitAssertion(() =>
        {
            var table = SEntMan.GetComponent<OperatingTableLightComponent>(_table);
            Assert.That(table.LightOn, Is.False);
            Assert.That(LightEnabled(), Is.False);
        });

        await UseLightVerb("operating-table-light-turn-on");

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<OperatingTableLightComponent>(_table).LightOn);
            Assert.That(LightEnabled());
            Assert.That(SEntMan.System<SharedAppearanceSystem>().TryGetData<bool>(_table, OperatingTableVisuals.LightOn, out var lightOn) && lightOn);
        });

        await UseLightVerb("operating-table-light-turn-off");

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<OperatingTableLightComponent>(_table).LightOn, Is.False);
            Assert.That(LightEnabled(), Is.False);
        });
    }

    private async Task UseLightVerb(string textKey)
    {
        await Server.WaitPost(() =>
        {
            var text = Server.ResolveDependency<ILocalizationManager>().GetString(textKey);
            var verbs = SEntMan.System<SharedVerbSystem>();
            var verb = verbs.GetLocalVerbs(_table, _user, typeof(AlternativeVerb), force: true).Single(v => v.Text == text);
            verbs.ExecuteVerb(verb, _user, _table, forced: true);
        });
        await Pair.RunTicksSync(5);
    }

    [Test]
    public async Task VitalsFollowThePatientsHealth()
    {
        await SetUp();
        await Buckle();

        await Server.WaitAssertion(() =>
        {
            Assert.That(Vitals(), Is.EqualTo(VitalsState.Healthy));
            Assert.That(SEntMan.GetComponent<OperatingTableMonitoredComponent>(_patient).Table, Is.EqualTo(_table));
        });

        await Damage(30);
        await Server.WaitAssertion(() => Assert.That(Vitals(), Is.EqualTo(VitalsState.Injured)));

        await Damage(40);
        await Server.WaitAssertion(() => Assert.That(Vitals(), Is.EqualTo(VitalsState.Critical)));

        await Damage(200);
        await Server.WaitAssertion(() => Assert.That(Vitals(), Is.EqualTo(VitalsState.Dead)));
    }

    [Test]
    public async Task MonitorStopsWhenThePatientLeaves()
    {
        await SetUp();
        await Buckle();
        await Damage(30);

        await Server.WaitPost(() => SEntMan.System<SharedBuckleSystem>().Unbuckle(_patient, null));
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Vitals(), Is.EqualTo(VitalsState.None));
            Assert.That(SEntMan.HasComponent<OperatingTableMonitoredComponent>(_patient), Is.False);
        });

        // Nobody is on the table, so it must not react to someone else's damage.
        await Damage(100);
        await Server.WaitAssertion(() => Assert.That(Vitals(), Is.EqualTo(VitalsState.None)));
    }
}
