using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Polonium.Paper;
using Content.Shared.Paper;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using static Content.Shared.Paper.PaperComponent;

namespace Content.IntegrationTests.Tests._Polonium.Paper;

/// <summary>
/// The server only accepts paper text from someone who opened the paper with a writing tool.
/// </summary>
[TestOf(typeof(PaperWriteGuardSystem))]
public sealed class PaperWriteGuardTest : InteractionTest
{
    private const string PaperProto = "Paper";
    private const string PenProto = "Pen";
    private const string CyberPenProto = "CyberPen";
    private const string Original = "original";
    private const string Written = "written";
    private const string Forged = "forged";

    [Test]
    public async Task ReaderCannotWrite()
    {
        await SpawnPaper();
        await OpenToRead();

        await SendBui(PaperUiKey.Key, new PaperInputTextMessage(Forged));

        Assert.That(PaperText(), Is.EqualTo(Original));
    }

    [Test]
    public async Task PenCanWrite()
    {
        await PlaceInHands(PenProto);
        await SpawnPaper();
        await OpenWithHeldTool();

        await SendBui(PaperUiKey.Key, new PaperInputTextMessage(Written));

        Assert.That(PaperText(), Is.EqualTo(Written));
    }

    [Test]
    public async Task ReaderCanTakeAPenAndWrite()
    {
        await SpawnPaper();
        await OpenToRead();
        await PlaceInHands(PenProto);
        await OpenWithHeldTool();

        await SendBui(PaperUiKey.Key, new PaperInputTextMessage(Written));

        Assert.That(PaperText(), Is.EqualTo(Written));
    }

    [Test]
    public async Task PuttingThePenAwayKeepsTheDraft()
    {
        await PlaceInHands(PenProto);
        await SpawnPaper();
        await OpenWithHeldTool();
        await Drop();

        await SendBui(PaperUiKey.Key, new PaperInputTextMessage(Written));

        Assert.That(PaperText(), Is.EqualTo(Written));
    }

    [Test]
    public async Task PenCannotRewriteAStampedPaper()
    {
        await PlaceInHands(PenProto);
        await SpawnPaper();
        await Stamp();
        await OpenToRead();

        await SendBui(PaperUiKey.Key, new PaperInputTextMessage(Forged));

        Assert.That(PaperText(), Is.EqualTo(Original));
    }

    [Test]
    public async Task StampLandingWhileWritingBlocksTheSave()
    {
        await PlaceInHands(PenProto);
        await SpawnPaper();
        await OpenWithHeldTool();
        await Stamp();

        await SendBui(PaperUiKey.Key, new PaperInputTextMessage(Forged));

        Assert.That(PaperText(), Is.EqualTo(Original));
    }

    [Test]
    public async Task CyberPenCanRewriteAStampedPaper()
    {
        await PlaceInHands(CyberPenProto);
        await SpawnPaper();
        await Stamp();
        await OpenWithHeldTool();

        await SendBui(PaperUiKey.Key, new PaperInputTextMessage(Written));

        Assert.That(PaperText(), Is.EqualTo(Written));
    }

    [Test]
    public async Task ClosingTheWindowEndsTheRightToWrite()
    {
        await PlaceInHands(PenProto);
        await SpawnPaper();
        await OpenWithHeldTool();
        await CloseBui(PaperUiKey.Key);
        await OpenToRead();

        await SendBui(PaperUiKey.Key, new PaperInputTextMessage(Forged));

        Assert.That(PaperText(), Is.EqualTo(Original));
    }

    [Test]
    public async Task SavingEndsTheRightToWrite()
    {
        await PlaceInHands(PenProto);
        await SpawnPaper();
        await OpenWithHeldTool();

        await SendBui(PaperUiKey.Key, new PaperInputTextMessage(Written));
        await SendBui(PaperUiKey.Key, new PaperInputTextMessage(Forged));

        Assert.That(PaperText(), Is.EqualTo(Written));
    }

    [Test]
    public async Task NullTextIsIgnored()
    {
        await PlaceInHands(PenProto);
        await SpawnPaper();
        await OpenWithHeldTool();

        await SendBui(PaperUiKey.Key, new PaperInputTextMessage(null!));

        Assert.That(PaperText(), Is.EqualTo(Original));
    }

    private async Task SpawnPaper()
    {
        await SpawnTarget(PaperProto);
        await Server.WaitPost(() => SEntMan.System<PaperSystem>().SetContent(STarget!.Value, Original));
    }

    private async Task OpenToRead()
    {
        await Server.WaitPost(() => SUiSys.OpenUi(STarget!.Value, PaperUiKey.Key, SPlayer));
        await RunTicks(10);
    }

    private async Task OpenWithHeldTool()
    {
        await Interact(awaitDoAfters: false);
        await RunTicks(10);
    }

    private async Task Stamp()
    {
        await Server.WaitPost(() =>
        {
            var paper = new Entity<PaperComponent>(STarget!.Value, SComp<PaperComponent>(STarget!.Value));
            var stamp = new StampDisplayInfo { StampedName = "OK", StampedColor = Color.Red };
            Assert.That(SEntMan.System<PaperSystem>().TryStamp(paper, stamp, "paper_stamp-generic"));
        });
        await RunTicks(5);
    }

    private string PaperText()
    {
        return SComp<PaperComponent>(STarget!.Value).Content;
    }
}
