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

    /// <summary>
    /// Initializes the system and registers alternative verbs for paper entities.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaperComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
    }

    /// <summary>
    /// Adds a signing alternative verb when the user can interact with the paper while using a signature-writing tool.
    /// </summary>
    /// <param name="ent">The paper entity receiving the signature verb.</param>
    /// <param name="args">The alternative verbs available to the user.</param>
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
    /// <summary>
    /// Attempts to begin placing a signature on paper.
    /// </summary>
    /// <param name="paper">The paper entity to sign.</param>
    /// <param name="signer">The entity signing the paper.</param>
    /// <param name="pen">The entity used to sign the paper.</param>
    /// <returns><c>true</c> if signature placement begins; <c>false</c> if the signing attempt is cancelled.</returns>
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
    /// <summary>
    /// Starts placing a signature on the specified paper.
    /// </summary>
    /// <param name="paper">The paper receiving the signature.</param>
    /// <param name="signer">The entity signing the paper.</param>
    /// <param name="pen">The writing instrument used for the signature.</param>
    protected virtual void StartSignaturePlacement(Entity<PaperComponent> paper, EntityUid signer, EntityUid pen)
    {
    }

    /// <summary>
    ///     Builds the display info for a signature (a text-only "stamp") from the
    ///     signer and the pen they're using, without any placement transform.
    ///     Shared so the client can build an identical preview.
    /// <summary>
    /// Builds the visual information used to display a signature.
    /// </summary>
    /// <param name="signer">The entity whose name appears on the signature.</param>
    /// <param name="pen">The writing instrument used for the signature.</param>
    /// <param name="signatureComp">The writing instrument's signature settings.</param>
    /// <returns>Signature display data containing the signer's name, color, and font.</returns>
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

    /// <summary>
    /// Determines the signature name for an entity.
    /// </summary>
    /// <param name="uid">The entity whose signature name is determined.</param>
    /// <returns>The non-empty full name from the entity's ID card, or the entity's name when no suitable ID-card name is available.</returns>
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
