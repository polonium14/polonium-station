using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Client._Polonium.Medical.Surgery;
using Content.Client._Shitmed.Medical.Surgery;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared.Body;
using NUnit.Framework;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Medical;

/// <summary>
/// The one-view surgery window: surgeries under group headings, the gel-less close beside the ordinary one,
/// and the steps of a picked surgery.
/// </summary>
[TestFixture]
public sealed class OperationWindowTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task WindowShowsGroupsSplitRowAndSteps()
    {
        var map = await Pair.CreateTestMap();
        EntityUid body = default;

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var user = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Server.ResolveDependency<ISharedPlayerManager>().SetAttachedEntity(ServerSession!, user);

            var torso = SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
                .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == "Torso");
            SEntMan.AddComponent<IncisionOpenComponent>(torso);
            SEntMan.AddComponent<BleedersClampedComponent>(torso);
            SEntMan.AddComponent<SkinRetractedComponent>(torso);
            SEntMan.AddComponent<BonesSawedComponent>(torso);
            SEntMan.AddComponent<BonesOpenComponent>(torso);
        });
        await Pair.RunTicksSync(10);

        await Server.WaitAssertion(() =>
        {
            var user = ServerSession!.AttachedEntity!.Value;
            Assert.That(SEntMan.System<SharedUserInterfaceSystem>().TryOpenUi(body, SurgeryUIKey.Key, user));
            typeof(Content.Server._Shitmed.Medical.Surgery.SurgerySystem)
                .GetMethod("RefreshUI", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(SEntMan.System<Content.Server._Shitmed.Medical.Surgery.SurgerySystem>(), [body]);
        });
        await Pair.RunTicksSync(10);

        await Client.WaitAssertion(() =>
        {
            var window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<OperationWindow>().Single();
            var rows = window.Surgeries.Children.OfType<BoxContainer>().ToList();
            Assert.Multiple(() =>
            {
                Assert.That(window.Surgeries.Children.OfType<Label>().Count(), Is.GreaterThanOrEqualTo(2), "Group headings.");
                Assert.That(rows.Count(row => row.ChildCount == 2), Is.EqualTo(1), "Only the close surgery has an alternative beside it.");
                Assert.That(window.Steps.ChildCount, Is.Zero, "No surgery is picked yet.");
            });

            var cBody = CEntMan.GetEntity(SEntMan.GetNetEntity(body));
            Assert.That(CEntMan.System<SharedUserInterfaceSystem>().TryGetOpenUi<OperationBui>(cBody, SurgeryUIKey.Key, out var bui));
            typeof(OperationBui).GetMethod("OnSurgeryPressed", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(bui, [new EntProtoId("SurgeryCloseIncisionField")]);

            var buttons = (System.Collections.IDictionary) typeof(OperationBui)
                .GetField("_surgeryButtons", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(bui)!;
            var opened = ((Content.Client._Shitmed.Choice.UI.ChoiceControl, string)) buttons[new EntProtoId("SurgeryOpenIncision")]!;
            Assert.That(opened.Item1.NameLabel.GetMessage(), Does.Contain("#5FD35F").IgnoreCase, "The incision is already open.");

            var steps = window.Steps.Children.OfType<SurgeryStepButton>().ToList();
            Assert.Multiple(() =>
            {
                Assert.That(steps, Has.Count.EqualTo(2));
                Assert.That(steps[0].Button.Disabled, Is.False, "Closing the bones is the next step.");
                Assert.That(steps[1].Button.Disabled, Is.True);
            });
        });
    }

    /// <summary>
    /// On an untouched patient nothing is marked as done, and a surgery behind a closed chest says what it needs.
    /// </summary>
    [Test]
    public async Task UntouchedPatientShowsNothingDoneAndNamesRequirements()
    {
        var map = await Pair.CreateTestMap();
        EntityUid body = default;

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var user = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Server.ResolveDependency<ISharedPlayerManager>().SetAttachedEntity(ServerSession!, user);
        });
        await Pair.RunTicksSync(10);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<SharedUserInterfaceSystem>().TryOpenUi(body, SurgeryUIKey.Key, ServerSession!.AttachedEntity!.Value));
            typeof(Content.Server._Shitmed.Medical.Surgery.SurgerySystem)
                .GetMethod("RefreshUI", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(SEntMan.System<Content.Server._Shitmed.Medical.Surgery.SurgerySystem>(), [body]);
        });
        await Pair.RunTicksSync(10);

        await Client.WaitAssertion(() =>
        {
            var cBody = CEntMan.GetEntity(SEntMan.GetNetEntity(body));
            Assert.That(CEntMan.System<SharedUserInterfaceSystem>().TryGetOpenUi<OperationBui>(cBody, SurgeryUIKey.Key, out var bui));
            var buttons = (System.Collections.IDictionary) typeof(OperationBui)
                .GetField("_surgeryButtons", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(bui)!;

            var requires = Client.ResolveDependency<Robust.Shared.Localization.ILocalizationManager>().GetString("surgery-window-requires", ("surgery", "")).Trim();
            var done = new List<string>();
            var blocked = new List<string>();
            foreach (System.Collections.DictionaryEntry entry in buttons)
            {
                var button = ((Content.Client._Shitmed.Choice.UI.ChoiceControl, string)) entry.Value!;
                var text = button.Item1.NameLabel.GetMessage() ?? string.Empty;
                Assert.That(text, Does.Contain(button.Item2), "Every button carries its name.");
                if (text.Contains("#5FD35F", StringComparison.OrdinalIgnoreCase))
                    done.Add(entry.Key!.ToString());
                if (text.Contains(requires))
                    blocked.Add(entry.Key!.ToString());
            }

            Assert.Multiple(() =>
            {
                Assert.That(buttons.Count, Is.GreaterThan(5));
                Assert.That(done, Is.Empty, "Nothing has been done to this patient.");
                Assert.That(blocked, Does.Contain("SurgeryRemoveHeart"));
                Assert.That(blocked, Does.Not.Contain("SurgeryOpenIncision"));
            });
        });
    }

    /// <summary>
    /// Sewing a limb back on needs an incision in the torso first, and the window has to say so.
    /// </summary>
    [Test]
    public async Task AttachingALimbNamesTheIncisionItNeeds()
    {
        var map = await Pair.CreateTestMap();
        EntityUid body = default;

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var user = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Server.ResolveDependency<ISharedPlayerManager>().SetAttachedEntity(ServerSession!, user);

            var arm = SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
                .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == "ArmLeft");
            SEntMan.System<Robust.Shared.Containers.SharedContainerSystem>().Remove(arm, SEntMan.GetComponent<BodyComponent>(body).Organs!);
        });
        await Pair.RunTicksSync(10);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<SharedUserInterfaceSystem>().TryOpenUi(body, SurgeryUIKey.Key, ServerSession!.AttachedEntity!.Value));
            typeof(Content.Server._Shitmed.Medical.Surgery.SurgerySystem)
                .GetMethod("RefreshUI", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(SEntMan.System<Content.Server._Shitmed.Medical.Surgery.SurgerySystem>(), [body]);
        });
        await Pair.RunTicksSync(10);

        await Client.WaitAssertion(() =>
        {
            var cBody = CEntMan.GetEntity(SEntMan.GetNetEntity(body));
            Assert.That(CEntMan.System<SharedUserInterfaceSystem>().TryGetOpenUi<OperationBui>(cBody, SurgeryUIKey.Key, out var bui));
            var buttons = (System.Collections.IDictionary) typeof(OperationBui)
                .GetField("_surgeryButtons", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(bui)!;
            Assert.That(buttons.Contains(new EntProtoId("SurgeryAttachLeftArm")), "keys: " + string.Join(",", buttons.Keys.Cast<object>()));
            var attach = ((Content.Client._Shitmed.Choice.UI.ChoiceControl, string)) buttons[new EntProtoId("SurgeryAttachLeftArm")]!;
            Assert.That(attach.Item1.NameLabel.GetMessage(), Does.Contain(":"), "The attach surgery names what it requires.");

            typeof(OperationBui).GetMethod("OnSurgeryPressed", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(bui, [new EntProtoId("SurgeryAttachLeftArm")]);
            var window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<OperationWindow>().Single();
            Assert.That(window.Steps.GetChild(0), Is.Not.InstanceOf<SurgeryStepButton>(), "A line above the steps says what comes first.");
        });
    }
}
