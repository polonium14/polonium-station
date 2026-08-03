// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Access.Systems;
using Content.Shared.Crayon;
using Content.Shared.Paper;
using Content.Shared.Verbs;

namespace Content.Shared._Polonium.Paper;

public abstract partial class SharedSignatureSystem : EntitySystem
{
    [Dependency] private SharedIdCardSystem _idCard = default!;

    // The sprite used to visualize "signatures" on paper entities.
    public const string SignatureStampState = "paper_stamp-signature";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaperComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
    }

    private void OnGetAltVerbs(Entity<PaperComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (args.Using is not { } pen)
            return;

        if (!TryComp<SignatureWriterComponent>(pen, out var signatureComp))
            return;

        var user = args.User;
        AlternativeVerb verb = new()
        {
            Act = () =>
            {
                TrySignPaper(ent, user, pen, signatureComp);
            },
            Text = Loc.GetString("paper-sign-verb"),
            DoContactInteraction = true,
            Priority = 10
        };
        args.Verbs.Add(verb);
    }

    /// <summary>
    ///     Validates a signing attempt and, if allowed, opens the signature
    ///     placement UI for the signer. The signature isn't committed here; it's
    ///     committed later when the client sends a <see cref="PaperComponent.PaperSignMessage"/>.
    /// </summary>
    public bool TrySignPaper(Entity<PaperComponent> paper, EntityUid signer, EntityUid pen, SignatureWriterComponent signatureComp)
    {
        var ev = new SignAttemptEvent(paper, signer, pen);
        RaiseLocalEvent(pen, ref ev);
        if (ev.Cancelled)
            return false;

        StartSignaturePlacement(paper, signer, pen);

        return true;
    }

    /// <summary>
    ///     Opens the placement UI. Server-only; the shared base does nothing.
    /// </summary>
    protected virtual void StartSignaturePlacement(Entity<PaperComponent> paper, EntityUid signer, EntityUid pen)
    {
    }

    /// <summary>
    ///     Builds the display info for a signature (a text-only "stamp") from the
    ///     signer and the pen they're using, without any placement transform.
    ///     Shared so the client can build an identical preview.
    /// </summary>
    public StampDisplayInfo BuildSignatureInfo(EntityUid signer, EntityUid pen, SignatureWriterComponent signatureComp)
    {
        var signatureColor = signatureComp.Color;
        var signatureFont = "Default"; // Noto Sans as fallback

        if (signatureComp.Font is { } penFont)
            signatureFont = penFont;

        if (TryComp<CrayonComponent>(pen, out var crayon))
            signatureColor = crayon.Color;

        return new StampDisplayInfo
        {
            StampedName = DetermineEntitySignature(signer),
            StampedColor = signatureColor,
            HasIcon = false,
            StampFont = signatureFont,
            LocalizeName = false, // a raw signer name, not a loc id
        };
    }

    public string DetermineEntitySignature(EntityUid uid)
    {
        // If the entity has an ID, use the name on it.
        if (_idCard.TryFindIdCard(uid, out var id) && !string.IsNullOrWhiteSpace(id.Comp.FullName))
        {
            return id.Comp.FullName;
        }

        // Alternatively, return the entity name
        return Name(uid);
    }
}
