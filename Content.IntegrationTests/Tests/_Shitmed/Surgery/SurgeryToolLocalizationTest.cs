using System.Globalization;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

[TestFixture]
public sealed class SurgeryToolLocalizationTest : GameTest
{
    private static readonly (string Key, string Nominative, string Genitive, string English)[] Tools =
    [
        ("bone-gel", "żel kostny", "żelu kostnego", "bone gel"),
        ("bone-saw", "piła do kości", "piły do kości", "a bone saw"),
        ("bone-setter", "nastawiacz kości", "nastawiacza kości", "a bone setter"),
        ("cautery", "kauter", "kautera", "a cautery"),
        ("drill", "wiertło", "wiertła", "a drill"),
        ("hemostat", "hemostat", "hemostatu", "a hemostat"),
        ("retractor", "rozwórka", "rozwórki", "a retractor"),
        ("scalpel", "skalpel", "skalpela", "a scalpel"),
        ("stitches", "nici chirurgiczne", "nici chirurgicznych", "stitches"),
        ("tending", "opatrunek", "opatrunku", "a wound tender"),
        ("tweezers", "pęseta", "pęsety", "tweezers"),
        ("organ", "organ", "organu", "an organ"),
    ];

    [TestCase("pl-PL")]
    [TestCase("en-US")]
    public async Task SurroundingMessagesChooseTheToolNameForm(string culture)
    {
        await Server.WaitAssertion(() =>
        {
            var loc = Server.ResolveDependency<ILocalizationManager>();
            var previous = loc.DefaultCulture;
            try
            {
                loc.SetCulture(CultureInfo.GetCultureInfo(culture));
                foreach (var (key, nominative, genitive, english) in Tools)
                {
                    var tool = $"surgery-tool-name-{key}";
                    var popup = loc.GetString("surgery-error-missing-tool", ("tool", tool));
                    Assert.That(popup, Is.EqualTo(culture == "pl-PL"
                        ? $"Do wykonania tego kroku potrzebujesz {genitive}."
                        : $"You need {english} to perform this step!"));

                    foreach (var message in new[] { "surgery-tool-unlimited", "surgery-tool-used" })
                    {
                        var examine = loc.GetString(message, ("tool", tool), ("speed", "1.00"), ("color", "white"));
                        Assert.That(examine, Does.StartWith($"- {(culture == "pl-PL" ? nominative : english)} "));
                        Assert.That(examine, Does.Contain("[color=white]1.00x[/color]"));
                        Assert.That(examine, Does.Not.Contain("surgery-tool-name-"));
                    }
                }
            }
            finally
            {
                loc.SetCulture(previous!);
            }
        });
    }

    [Test]
    public async Task PolishEntityTextResolvesToolCasesWithoutCallerArguments()
    {
        await Server.WaitAssertion(() =>
        {
            var loc = Server.ResolveDependency<ILocalizationManager>();
            var previous = loc.DefaultCulture;
            try
            {
                loc.SetCulture(CultureInfo.GetCultureInfo("pl-PL"));
                // Entity names and descriptions are resolved without Fluent arguments.
                // Their nested LOC calls must supply the case themselves.
                var gel = loc.GetEntityData("BoneGel");
                Assert.That(gel.Name, Is.EqualTo("butelka żelu kostnego"));
                Assert.That(gel.Desc, Does.StartWith("Pojemnik na żel kostny,"));
                Assert.That(loc.GetEntityData("MedicalStitches").Desc,
                    Does.Contain("wchłanialnych nici chirurgicznych z poliglikolidu"));
                Assert.That(loc.GetEntityData("SurgeryStepOpenIncisionScalpel").Name,
                    Is.EqualTo("Natnij skalpelem"));

                foreach (var (prototype, name) in new[]
                         {
                             ("Bonesetter", "nastawiacz kości"),
                             ("Cautery", "kauter"),
                             ("Drill", "wiertło"),
                             ("Scalpel", "skalpel"),
                             ("ScalpelAdvanced", "zaawansowany skalpel"),
                             ("ScalpelLaser", "skalpel laserowy"),
                             ("Retractor", "rozwórka"),
                             ("Hemostat", "hemostat"),
                         })
                {
                    Assert.That(loc.GetEntityData(prototype).Name, Is.EqualTo(name));
                }

                Assert.That(loc.GetEntityData("Drill").Desc, Does.StartWith("Wiertło chirurgiczne"));
                Assert.That(loc.GetEntityData("ScalpelLaser").Desc, Does.StartWith("Skalpel tnący"));
                Assert.That(loc.GetEntityData("Saw").Desc, Does.EndWith("jako piła do kości."));
                Assert.That(loc.GetEntityData("SawAdvanced").Desc,
                    Does.StartWith("Najnowocześniejsza piła do kości firmy Interdyne."));
                Assert.That(loc.GetEntityData("SurgeryKitFilled").Desc,
                    Is.EqualTo(loc.GetEntityData("SurgeryKit").Desc));
            }
            finally
            {
                loc.SetCulture(previous!);
            }
        });
    }

    [TestCase("pl-PL", "Do wykonania tego kroku potrzebujesz skalpela.", "- skalpel ")]
    [TestCase("en-US", "You need a scalpel to perform this step!", "- a scalpel ")]
    public async Task SurgeryCallersPassTheLocalizationKey(string culture, string expectedPopup, string expectedExamine)
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var loc = Server.ResolveDependency<ILocalizationManager>();
            var previous = loc.DefaultCulture;
            try
            {
                loc.SetCulture(CultureInfo.GetCultureInfo(culture));
                var user = SEntMan.SpawnEntity(null, coords);
                SEntMan.AddComponent<HandsComponent>(user);
                SEntMan.System<SharedHandsSystem>().AddHand(user, "right", HandLocation.Right);
                var patient = SEntMan.SpawnEntity("HealedTraumaBody", coords);
                var part = SEntMan.SpawnEntity("HealedTraumaTorso", coords);
                var surgery = SEntMan.System<SurgerySystem>();
                var step = surgery.GetSingleton("SurgeryStepOpenIncisionScalpel")!.Value;
                Assert.That(surgery.CanPerformStepWithHeld(user, patient, part, step, false, out var popup), Is.False);
                Assert.That(popup, Is.EqualTo(expectedPopup));

                foreach (var used in new[] { false, true })
                {
                    var examine = new SurgeryToolExaminedEvent(new FormattedMessage());
                    SEntMan.System<SurgeryToolExamineSystem>().OnExamined(user, new ScalpelComponent { Used = used }, ref examine);
                    Assert.That(examine.Message.ToString(), Does.StartWith(expectedExamine));
                }
            }
            finally
            {
                loc.SetCulture(previous!);
            }
        });
    }
}
