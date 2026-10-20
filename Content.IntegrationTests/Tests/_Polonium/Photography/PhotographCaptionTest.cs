using PhotoViewerBoundUserInterface = Content.Client._Polonium.Photography.PhotoViewerBoundUserInterface;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Polonium.Photography;
using Content.Server.Examine;
using Content.Shared._Polonium.Paper;
using Content.Shared._Polonium.Photography;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Photography;

/// <summary>
/// Signing a photograph: a pen used on it opens the viewer with a caption input, the
/// client's text is sanitized and stored with the pen's ink and font, it shows on examine,
/// and a signed photo can't be rewritten.
/// </summary>
[TestOf(typeof(PoloniumPhotographySystem))]
public sealed class PhotographCaptionTest : InteractionTest
{
    private const string PhotoProto = "PoloniumPhotograph";

    [Test]
    public async Task PenSignsPhotoOnce()
    {
        await SpawnTarget(PhotoProto);
        var pen = await PlaceInHands("Pen");

        // Pen on photo: server opens the viewer for us and offers the caption input.
        await Interact();
        Assert.That(TryGetBui(PhotoViewerUiKey.Key, out var bui), "Using a pen on a photo must open its viewer.");
        Assert.That(((PhotoViewerBoundUserInterface) bui!).IsSigning, "The viewer must show the caption input.");

        await SendBui(PhotoViewerUiKey.Key, new PhotoSignMessage("  Maurice\n\tTexas  "));

        var photo = Comp<PoloniumPhotographComponent>();
        var penColor = Comp<SignatureWriterComponent>(pen).Color;
        Assert.Multiple(() =>
        {
            Assert.That(photo.Caption, Is.EqualTo("Maurice Texas"), "Whitespace must be collapsed and trimmed.");
            Assert.That(photo.CaptionColor, Is.EqualTo(penColor), "The caption takes the pen's ink.");
            Assert.That(photo.CaptionFont, Is.EqualTo(PhotographyConstants.DefaultCaptionFont));
        });

        var text = await ExamineTarget();
        Assert.That(text, Does.Contain("Maurice Texas"), "The caption must be readable on examine.");

        // A second caption is ignored.
        await SendBui(PhotoViewerUiKey.Key, new PhotoSignMessage("Someone else"));
        Assert.That(Comp<PoloniumPhotographComponent>().Caption, Is.EqualTo("Maurice Texas"));
    }

    [Test]
    public async Task PenOnOpenViewerShowsInput()
    {
        await SpawnTarget(PhotoProto);

        // Viewer already open (e.g. activated in hand) before the pen touches the photo.
        await Server.WaitPost(() => SEntMan.System<Robust.Server.GameObjects.UserInterfaceSystem>()
            .OpenUi(SEntMan.GetEntity(Target!.Value), PhotoViewerUiKey.Key, SEntMan.GetEntity(Player)));
        await RunTicks(15);
        Assert.That(TryGetBui(PhotoViewerUiKey.Key, out var bui));
        Assert.That(((PhotoViewerBoundUserInterface) bui!).IsSigning, Is.False, "No pen yet, no input.");

        await PlaceInHands("Pen");
        await Interact();
        await RunTicks(15);
        Assert.That(((PhotoViewerBoundUserInterface) bui!).IsSigning, "A pen on an open viewer must show the input.");
    }

    [Test]
    public async Task UnheldPenCannotSign()
    {
        await SpawnTarget(PhotoProto);
        await PlaceInHands("Pen");
        await Interact();

        // Drop the pen into the world, then claim it in a message anyway.
        await Server.WaitPost(() => HandSys.TryDrop(SEntMan.GetEntity(Player)));
        await RunTicks(1);

        await SendBui(PhotoViewerUiKey.Key, new PhotoSignMessage("forged"));
        Assert.That(Comp<PoloniumPhotographComponent>().Caption, Is.Null);
    }

    [Test]
    public void SanitizeCaption()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SharedPoloniumPhotographySystem.SanitizeCaption(null), Is.Empty);
            Assert.That(SharedPoloniumPhotographySystem.SanitizeCaption(" \n\t "), Is.Empty);
            Assert.That(SharedPoloniumPhotographySystem.SanitizeCaption("a\u0000b​c"), Is.EqualTo("abc"));
            Assert.That(SharedPoloniumPhotographySystem.SanitizeCaption("Zażółć  gęślą"), Is.EqualTo("Zażółć gęślą"));
            Assert.That(SharedPoloniumPhotographySystem.SanitizeCaption(new string('x', 100)),
                Has.Length.EqualTo(PhotographyConstants.CaptionMaxLength));
        });
    }

    private async Task<string> ExamineTarget()
    {
        var text = string.Empty;
        await Server.WaitPost(() =>
            text = SEntMan.System<ExamineSystem>()
                .GetExamineText(SEntMan.GetEntity(Target!.Value), SEntMan.GetEntity(Player))
                .ToString());
        return text;
    }
}
