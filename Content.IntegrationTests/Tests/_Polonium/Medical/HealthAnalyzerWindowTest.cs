using Content.Server._Polonium.Medical.HealthAnalyzer;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared.FixedPoint;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Client._Polonium.Medical.HealthAnalyzer;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Body;
using Content.Server.Medical.HealthAnalyzer;
using Content.Shared._Polonium.Medical.HealthAnalyzer;
using Content.Shared.Body;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Polonium.Medical;

/// <summary>
/// The one-view analyzer window draws every kind of patient without throwing: none, a human with a bit of everything,
/// a diona, a dead patient and an animal, one after another in the same window.
/// </summary>
[TestFixture]
public sealed class HealthAnalyzerWindowTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task WindowDrawsEveryKindOfPatient()
    {
        var map = await Pair.CreateTestMap();
        var scans = new List<HealthAnalyzerScanMessage>();

        await Server.WaitAssertion(() =>
        {
            var analyzer = SEntMan.System<PatientScanSystem>();
            var debug = SEntMan.System<BodyDebugSystem>();

            EntityUid Part(EntityUid body, string category)
            {
                return SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
                    .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == category);
            }

            var human = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(debug.TryKillOrgan(human, Part(human, "LungLeft")));
            Assert.That(debug.TrySetOrganIntegrity(human, Part(human, "Heart"), SEntMan.GetComponent<OrganIntegrityComponent>(Part(human, "Heart")).IntegrityCap * 0.2f));
            Assert.That(debug.TrySetFailureStage(human, Part(human, "Heart"), 2));
            Assert.That(debug.TrySetBoneIntegrity(human, Part(human, "Torso"), FixedPoint2.Zero));
            Assert.That(debug.TryAddWound(human, Part(human, "ArmLeft"), "Slash", 30));

            var dead = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(debug.TryRemoveOrgan(dead, Part(dead, "Heart")));
            SEntMan.System<MobStateSystem>().ChangeMobState(dead, MobState.Dead);

            scans.Add(analyzer.BuildScan(null, null));
            scans.Add(analyzer.BuildScan(human, true));
            scans.Add(analyzer.BuildScan(SEntMan.SpawnAtPosition("MobDiona", map.GridCoords), true));
            scans.Add(analyzer.BuildScan(dead, false));
            scans.Add(analyzer.BuildScan(SEntMan.SpawnAtPosition("MobMonkey", map.GridCoords), true));
            scans.Add(analyzer.BuildScan(human, true));
        });

        await Pair.RunTicksSync(5);

        PatientScanWindow? window = null;
        await Client.WaitPost(() =>
        {
            window = new PatientScanWindow();
            window.OpenCentered();
        });

        foreach (var scan in scans)
        {
            await Client.WaitPost(() =>
            {
                window!.Populate(scan);
                // The damage tooltip is built on hover; build it here too.
                typeof(PatientScanWindow).GetMethod("DamageTooltip", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, null);
            });
            await Pair.RunTicksSync(10);
        }

        await Client.WaitPost(() =>
        {
            window!.ReleaseDoll();
            window.Close();
        });
    }

    [Test]
    public async Task ChartMovesSegmentsWhenTheyChangePlaces()
    {
        await Client.WaitAssertion(() =>
        {
            var chart = new Content.Client._Polonium.UserInterface.Controls.SegmentedBarChart { SlideOnReorder = true };
            chart.SetEntry("Brute", 30, Color.Red, text: "Brute");
            chart.SetEntry("Burn", 20, Color.Orange, text: "Burn");

            // Burn overtakes Brute: the same two segments swap places instead of a third one appearing.
            chart.Clear();
            chart.SetEntry("Burn", 31, Color.Orange, text: "Burn");
            chart.SetEntry("Brute", 30, Color.Red, text: "Brute");

            Assert.That(chart.ChildCount, Is.EqualTo(2), "One label per segment, no copy left fading out.");
        });
    }
}
