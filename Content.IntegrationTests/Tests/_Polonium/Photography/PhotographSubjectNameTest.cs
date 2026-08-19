using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Polonium.Photography;
using Content.Server.Examine;
using Content.Shared._Polonium.Photography;
using Content.Shared.IdentityManagement;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Photography;

/// <summary>
/// Covers the auto-describe path: on capture the server freezes the clicked subject's name
/// (<see cref="Identity.Name"/> - identity-aware for humanoids, plain entity name otherwise)
/// and surfaces it on examine. A bare-tile shot has no subject and must stay silent. We
/// answer the capture token by hand with a correctly-sized payload so the server
/// store/examine path runs headlessly.
/// </summary>
[TestOf(typeof(PoloniumPhotographySystem))]
public sealed class PhotographSubjectNameTest : InteractionTest
{
    private const string CameraProto = "PoloniumCamera";
    private const string TargetProto = "Wrench";

    [Test]
    public async Task ExamineNamesTheClickedSubject()
    {
        var sys = SEntMan.System<PoloniumPhotographySystem>();

        await SpawnTarget(TargetProto);
        await PlaceInHands(CameraProto);

        // Shoot the target: issues capture token #1 with the subject name frozen.
        await Interact();

        string expected = default!;
        await Server.WaitPost(() => expected = Identity.Name(STarget!.Value, SEntMan));

        // Answer token id 1 (deterministic first shot of a fresh round) with a valid-length,
        // content-irrelevant payload; no GPU render. StoredCount below fails loudly if wrong.
        await SubmitPhoto(1);

        Assert.That(sys.StoredCount, Is.EqualTo(1), "The hand-submitted photo must have been stored.");

        var text = await ExaminePhotograph();
        Assert.That(text, Does.Contain(expected),
            "Examining the photograph must name its clicked subject.");
    }

    [Test]
    public async Task BareTileShotHasNoSubjectLine()
    {
        var sys = SEntMan.System<PoloniumPhotographySystem>();

        await PlaceInHands(CameraProto);

        // Click an empty tile a step away (in range, clear LOS): a subjectless capture.
        var tile = MapData.GridCoords.Offset(new Vector2(1f, 0f));
        await Interact(null, tile);

        await SubmitPhoto(1);
        Assert.That(sys.StoredCount, Is.EqualTo(1), "The hand-submitted photo must have been stored.");

        var text = await ExaminePhotograph();
        Assert.That(text, Does.Not.Contain("A photograph of"),
            "A photograph of nothing must not carry a subject line.");
    }

    /// <summary>Send a <see cref="SubmitPhotoEvent"/> from the client session, bypassing
    /// the client render, and let it land server-side.</summary>
    private async Task SubmitPhoto(int captureId)
    {
        var blob = new byte[PhotographyConstants.PhotoByteLength];
        await Client.WaitPost(() =>
            CEntMan.EntityNetManager!.SendSystemNetworkMessage(new SubmitPhotoEvent(captureId, blob)));
        await RunTicks(5);
    }

    /// <summary>Examine text of the single photograph spawned this round, as the player.</summary>
    private async Task<string> ExaminePhotograph()
    {
        var text = string.Empty;
        await Server.WaitPost(() =>
        {
            var photo = EntityUid.Invalid;
            var query = SEntMan.EntityQueryEnumerator<PoloniumPhotographComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                photo = uid;
                break;
            }

            Assert.That(photo, Is.Not.EqualTo(EntityUid.Invalid), "A photograph must have spawned.");
            text = SEntMan.System<ExamineSystem>()
                .GetExamineText(photo, SEntMan.GetEntity(Player))
                .ToString();
        });
        return text;
    }
}
