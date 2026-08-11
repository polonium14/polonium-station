// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Polonium.Paper;
using Content.Shared._Polonium.Paper;
using Content.Shared.Paper;
using Robust.Shared.GameObjects;
using static Content.Shared.Paper.PaperComponent;

namespace Content.IntegrationTests.Tests._Polonium.Paper;

[TestOf(typeof(SignatureSystem))]
public sealed class PaperSignatureTest : InteractionTest
{
    private const string PaperProto = "Paper";
    private const string PenProto = "Pen";
    private const string StampProto = "RubberStampCaptain";

    // Mirror of the private constants in the systems under test.
    private const int MaxStampedBy = 64;
    private const float MinScale = 0.25f;
    private const float MaxScale = 4.0f;

    /// <summary>
    /// Spawns a paper as the target and opens its UI for the player so that
    /// <see cref="InteractionTest.SendBui"/> has an interface to send through.
    /// </summary>
    private async Task SetupOpenPaper()
    {
        await SpawnTarget(PaperProto);
        await Server.WaitPost(() => SUiSys.OpenUi(STarget!.Value, PaperUiKey.Key, SPlayer));
        await RunTicks(10);
    }

    private List<StampDisplayInfo> ServerStamps()
    {
        return SComp<PaperComponent>(STarget!.Value).StampedBy;
    }

    /// <summary>
    /// A valid stamp placement commits exactly one mark, taking only the transform
    /// from the client while the name/icon are recomputed server-side.
    /// </summary>
    [Test]
    public async Task StampPlaceCommitsMark()
    {
        var stamp = await PlaceInHands(StampProto);
        await SetupOpenPaper();

        Assert.That(ServerStamps(), Is.Empty);

        var pos = new Vector2(0.3f, 0.7f);
        await SendBui(PaperUiKey.Key, new PaperStampPlaceMessage(stamp, pos, 1.2f));

        var stamps = ServerStamps();
        Assert.That(stamps, Has.Count.EqualTo(1));
        var info = stamps[0];
        Assert.Multiple(() =>
        {
            Assert.That(info.Position, Is.EqualTo(pos));
            Assert.That(info.Rotation, Is.EqualTo(1.2f));
            Assert.That(info.Scale, Is.EqualTo(1f), "Stamps always place at natural size.");
            Assert.That(info.LocalizeName, Is.True, "Stamp names are loc ids.");
        });
    }

    /// <summary>
    /// A client that sends NaN/Infinity in the transform must not be able to
    /// persist a poisoned float; it falls back to the paper's center and zero rotation.
    /// </summary>
    [Test]
    public async Task StampPlaceSanitizesNonFinite()
    {
        var stamp = await PlaceInHands(StampProto);
        await SetupOpenPaper();

        var pos = new Vector2(float.NaN, float.PositiveInfinity);
        await SendBui(PaperUiKey.Key, new PaperStampPlaceMessage(stamp, pos, float.NaN));

        var info = ServerStamps()[0];
        Assert.Multiple(() =>
        {
            Assert.That(info.Position, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(info.Rotation, Is.EqualTo(0f));
        });
    }

    /// <summary>
    /// Out-of-range positions are clamped into the normalized [0,1] display area.
    /// </summary>
    [Test]
    public async Task StampPlaceClampsPosition()
    {
        var stamp = await PlaceInHands(StampProto);
        await SetupOpenPaper();

        await SendBui(PaperUiKey.Key, new PaperStampPlaceMessage(stamp, new Vector2(5f, -3f), 0f));

        Assert.That(ServerStamps()[0].Position, Is.EqualTo(new Vector2(1f, 0f)));
    }

    /// <summary>
    /// The message is re-validated: a stamp that isn't held can't mark the paper,
    /// even if a crafted message arrives.
    /// </summary>
    [Test]
    public async Task StampPlaceRequiresHeldStamp()
    {
        var stamp = await PlaceInHands(StampProto);
        await SetupOpenPaper();

        await Drop();
        await SendBui(PaperUiKey.Key, new PaperStampPlaceMessage(stamp, new Vector2(0.5f, 0.5f), 0f));

        Assert.That(ServerStamps(), Is.Empty, "A dropped stamp must not commit a mark.");
    }

    /// <summary>
    /// A signature commits with a server-computed name (the client never sends one),
    /// flagged as a literal name with no icon.
    /// </summary>
    [Test]
    public async Task SignCommitsServerComputedName()
    {
        var pen = await PlaceInHands(PenProto);
        await SetupOpenPaper();

        string expectedName = default!;
        await Server.WaitPost(() => expectedName = SEntMan.GetComponent<MetaDataComponent>(SPlayer).EntityName);

        var pos = new Vector2(0.3f, 0.7f);
        await SendBui(PaperUiKey.Key, new PaperSignMessage(pen, pos, 1f, 0.5f));

        var stamps = ServerStamps();
        Assert.That(stamps, Has.Count.EqualTo(1));
        var info = stamps[0];
        Assert.Multiple(() =>
        {
            Assert.That(info.StampedName, Is.EqualTo(expectedName), "Server recomputes the signer name.");
            Assert.That(info.LocalizeName, Is.False, "A raw signer name is shown verbatim, not localized.");
            Assert.That(info.HasIcon, Is.False);
            Assert.That(info.Position, Is.EqualTo(pos), "The client transform is recorded verbatim.");
            Assert.That(info.Rotation, Is.EqualTo(0.5f));
        });
    }

    /// <summary>
    /// The committed signature scale is always clamped into [MinScale, MaxScale] and
    /// is never a non-finite value, regardless of what the client sends.
    /// </summary>
    [Test]
    public async Task SignClampsScale()
    {
        var pen = await PlaceInHands(PenProto);
        await SetupOpenPaper();

        var pos = new Vector2(0.8f, 0.2f);
        const float rot = 1.25f;

        await SendBui(PaperUiKey.Key, new PaperSignMessage(pen, pos, 100f, rot));
        Assert.That(ServerStamps()[^1].Scale, Is.EqualTo(MaxScale), "Oversized scale clamps down.");

        await SendBui(PaperUiKey.Key, new PaperSignMessage(pen, pos, 0.01f, rot));
        Assert.That(ServerStamps()[^1].Scale, Is.EqualTo(MinScale), "Undersized scale clamps up.");

        // A NaN scale must never be persisted; the committed value stays finite and
        // in range (the exact fallback depends on transport, so assert the invariant).
        await SendBui(PaperUiKey.Key, new PaperSignMessage(pen, pos, float.NaN, rot));
        Assert.That(ServerStamps(), Has.Count.EqualTo(3), "NaN-scale sign should still commit a mark.");
        var committed = ServerStamps()[^1].Scale;
        Assert.That(committed, Is.Not.Null);
        Assert.That(float.IsFinite(committed!.Value), Is.True, "A NaN scale must not be persisted.");
        Assert.That(committed.Value, Is.InRange(MinScale, MaxScale));

        // Clamping scale must leave the client transform untouched on every mark.
        Assert.Multiple(() =>
        {
            foreach (var mark in ServerStamps())
            {
                Assert.That(mark.Position, Is.EqualTo(pos));
                Assert.That(mark.Rotation, Is.EqualTo(rot));
            }
        });
    }

    /// <summary>
    /// Signing without holding the pen is rejected.
    /// </summary>
    [Test]
    public async Task SignRequiresHeldPen()
    {
        var pen = await PlaceInHands(PenProto);
        await SetupOpenPaper();

        await Drop();
        await SendBui(PaperUiKey.Key, new PaperSignMessage(pen, new Vector2(0.5f, 0.5f), 1f, 0f));

        Assert.That(ServerStamps(), Is.Empty);
    }

    /// <summary>
    /// Stamping the same stamp twice adds two marks. The old dedup behaviour was
    /// intentionally removed - accumulation of identical marks is a feature.
    /// </summary>
    [Test]
    public async Task IdenticalStampsAccumulate()
    {
        var stamp = await PlaceInHands(StampProto);
        await SetupOpenPaper();

        await SendBui(PaperUiKey.Key, new PaperStampPlaceMessage(stamp, new Vector2(0.5f, 0.5f), 0f));
        await SendBui(PaperUiKey.Key, new PaperStampPlaceMessage(stamp, new Vector2(0.5f, 0.5f), 0f));

        Assert.That(ServerStamps(), Has.Count.EqualTo(2));
    }

    /// <summary>
    /// A paper caps at <see cref="MaxStampedBy"/> marks. Fill it directly (cheap),
    /// then confirm the real placement handler rejects the overflow mark.
    /// </summary>
    [Test]
    public async Task StampCountIsCapped()
    {
        var stamp = await PlaceInHands(StampProto);
        await SetupOpenPaper();

        await Server.WaitPost(() =>
        {
            var paper = new Entity<PaperComponent>(STarget!.Value, SComp<PaperComponent>(STarget!.Value));
            var paperSys = SEntMan.System<PaperSystem>();
            var stampComp = SEntMan.GetComponent<StampComponent>(ToServer(stamp));
            var info = PaperSystem.GetStampInfo(stampComp);
            for (var i = 0; i < MaxStampedBy; i++)
                Assert.That(paperSys.TryStamp(paper, info, stampComp.StampState), Is.True, $"Fill mark {i} should succeed.");

            Assert.That(paperSys.TryStamp(paper, info, stampComp.StampState), Is.False, "The 65th mark must be rejected.");
        });

        Assert.That(ServerStamps(), Has.Count.EqualTo(MaxStampedBy));

        // The real BUI placement path must also reject once full.
        await SendBui(PaperUiKey.Key, new PaperStampPlaceMessage(stamp, new Vector2(0.5f, 0.5f), 0f));

        Assert.That(ServerStamps(), Has.Count.EqualTo(MaxStampedBy), "Placement over the cap must not add a mark.");
    }
}
