// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Polonium.Paper;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using static Content.Shared.Paper.PaperComponent;

namespace Content.Server._Polonium.Paper;

public sealed partial class SignatureSystem : SharedSignatureSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private const float MinScale = 0.25f;
    private const float MaxScale = 4.0f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaperComponent, PaperSignMessage>(OnPaperSign);
    }

    protected override void StartSignaturePlacement(Entity<PaperComponent> paper, EntityUid signer, EntityUid pen)
    {
        if (!TryComp<ActorComponent>(signer, out var actor))
            return;

        // Show the document, then tell this one client to enter placement mode.
        _ui.OpenUi(paper.Owner, PaperUiKey.Key, signer);
        RaiseNetworkEvent(new PaperSignRequestEvent(GetNetEntity(paper.Owner), GetNetEntity(pen)), actor.PlayerSession);
    }

    private void OnPaperSign(Entity<PaperComponent> paper, ref PaperSignMessage args)
    {
        var signer = args.Actor;

        if (!TryGetEntity(args.Pen, out var pen))
            return;

        if (!TryComp<SignatureWriterComponent>(pen, out var signatureComp))
            return;

        // Re-validate: the signer must still hold the pen and be able to reach the
        // paper. A client-supplied message must not let a dropped/unheld pen sign.
        if (!_hands.IsHolding(signer, pen.Value) ||
            !_interaction.InRangeUnobstructed(signer, paper.Owner))
        {
            _popup.PopupEntity(Loc.GetString("paper-signed-failure", ("target", paper.Owner)), signer, signer, PopupType.SmallCaution);
            return;
        }

        // The name/color/font are computed server-side; only the transform is
        // taken from the client (and clamped/sanitized against NaN/Infinity).
        var stampInfo = BuildSignatureInfo(signer, pen.Value, signatureComp);
        stampInfo.Position = SanitizePosition(args.Position);
        stampInfo.Scale = float.IsFinite(args.Scale) ? Math.Clamp(args.Scale, MinScale, MaxScale) : 1f;
        stampInfo.Rotation = float.IsFinite(args.Rotation) ? args.Rotation : 0f;

        if (!_paper.TryStamp(paper, stampInfo, SignatureStampState))
        {
            _popup.PopupEntity(Loc.GetString("paper-signed-failure", ("target", paper.Owner)), signer, signer, PopupType.SmallCaution);
            return;
        }

        // Show popups and play a paper writing sound
        var signedOtherMessage = Loc.GetString("paper-signed-other", ("user", signer), ("target", paper.Owner));
        _popup.PopupEntity(signedOtherMessage, signer, Filter.PvsExcept(signer, entityManager: EntityManager), true);

        var signedSelfMessage = Loc.GetString("paper-signed-self", ("target", paper.Owner));
        _popup.PopupEntity(signedSelfMessage, signer, signer);

        _audio.PlayEntity(paper.Comp.Sound, Filter.Pvs(signer), signer, true);

        _paper.UpdateUserInterface(paper);
    }

    // A client-supplied position may be NaN/Infinity; fall back to center before
    // clamping so a bad float can't be persisted and networked to every viewer.
    private static Vector2 SanitizePosition(Vector2 pos)
    {
        if (!float.IsFinite(pos.X) || !float.IsFinite(pos.Y))
            pos = new Vector2(0.5f, 0.5f);
        return Vector2.Clamp(pos, Vector2.Zero, Vector2.One);
    }
}
