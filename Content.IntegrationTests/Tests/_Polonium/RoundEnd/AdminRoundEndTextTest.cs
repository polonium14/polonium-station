using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.RoundEnd;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared._Polonium.RoundEnd;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Rules.Components;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests._Polonium.RoundEnd;

public sealed class AdminRoundEndTextTest : GameTest
{
    private const string Text = "The crew [bold]evacuated[/bold] the station.";
    private const string RuleText = "Rule summary";

    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true
    };

    [Test]
    public async Task RejectsUnsafeMarkup()
    {
        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(AdminRoundEndText.TryValidate(Text, out _), Is.True);
            Assert.That(AdminRoundEndText.TryValidate("[color=red]Text[/color]", out _), Is.True);
            Assert.That(AdminRoundEndText.TryValidate("[bold Text", out _), Is.False, "Accepted broken markup");
            Assert.That(AdminRoundEndText.TryValidate("[cmdlink=\"x\" command=\"disconnect\"/]", out _), Is.False,
                "Accepted a tag that runs commands on click");
            Assert.That(AdminRoundEndText.TryValidate(new string('x', AdminRoundEndText.MaxLength + 1), out _), Is.False,
                "Accepted text over the limit");
        });
    }

    [Test]
    public async Task FindsRulesWithText()
    {
        var server = Pair.Server;
        var roundEndText = server.System<AdminRoundEndTextSystem>();

        await server.WaitAssertion(() =>
        {
            var components = roundEndText.GetTextRuleComponents();
            Assert.That(components, Does.Contain(typeof(NukeopsRuleComponent)).And.Contain(typeof(ZombieRuleComponent)));
            Assert.That(components, Does.Not.Contain(typeof(MaxTimeRestartRuleComponent)),
                "Listed a rule that adds no text to the summary");
        });
    }

    [Test]
    public async Task OnlyEditableDuringRound()
    {
        var server = Pair.Server;
        var ticker = server.System<ServerGameTicker>();
        var roundEndText = server.System<AdminRoundEndTextSystem>();
        var admin = Pair.Player!;

        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(roundEndText.Prepend(RuleText), Is.EqualTo(RuleText));

            Assert.That(roundEndText.TrySetText(admin, "[bold Text"), Is.False);
            Assert.That(roundEndText.TrySetText(admin, $"  {Text}  "), Is.True);
            Assert.That(roundEndText.Text, Is.EqualTo(Text));
            Assert.That(roundEndText.Editor, Is.EqualTo(admin.Name));
            Assert.That(roundEndText.Prepend(RuleText), Does.StartWith(Loc.GetString("admin-round-end-text-header"))
                .And.EndWith($"{Text}\n{RuleText}"));

            ticker.EndRound();
            Assert.That(roundEndText.TrySetText(admin, "Changed"), Is.False, "Changed the text after the summary was sent");
            Assert.That(roundEndText.Text, Is.EqualTo(Text));
        });

        await Pair.WaitCommand("restartroundnow");
        await Pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(roundEndText.Text, Is.Empty);
            Assert.That(roundEndText.Editor, Is.Null);
        });
    }
}
