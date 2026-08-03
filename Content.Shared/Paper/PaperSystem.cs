// SPDX-FileCopyrightText: 2024 Plykiya <58439124+Plykiya@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 eoineoineoin <github@eoinrul.es>
// SPDX-FileCopyrightText: 2025 J <billsmith116@gmail.com>
// SPDX-FileCopyrightText: 2025 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Qerd <73325910+BigfootBravo@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 ScarKy0 <scarky0@onet.eu>
// SPDX-FileCopyrightText: 2025 Simon <63975668+Simyon264@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 SpeltIncorrectyl <66873282+SpeltIncorrectyl@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Tayrtahn <tayrtahn@gmail.com>
// SPDX-FileCopyrightText: 2025 Vasilis The Pikachu <vasilis@pikachu.systems>
// SPDX-FileCopyrightText: 2025 Winkarst <74284083+Winkarst-cpu@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 slarticodefast <161409025+slarticodefast@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 themias <89101928+themias@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 xsainteer <156868231+xsainteer@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 ArtisticRoomba <145879011+ArtisticRoomba@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2026 Whatstone <166147148+whatston3@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
// SPDX-FileCopyrightText: 2026 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Shared.Administration.Logs;
using Content.Shared.UserInterface;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Random.Helpers;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Audio.Systems;
using static Content.Shared.Paper.PaperComponent;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Paper;

public sealed partial class PaperSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private MetaDataSystem _metaSystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private INetManager _net = default!;

    [Dependency] private EntityQuery<PaperComponent> _paperQuery = default!;

    private static readonly ProtoId<TagPrototype> WriteIgnoreStampsTag = "WriteIgnoreStamps";
    private static readonly ProtoId<TagPrototype> WriteTag = "Write";


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaperComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PaperComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PaperComponent, BeforeActivatableUIOpenEvent>(BeforeUIOpen);
        SubscribeLocalEvent<PaperComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PaperComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PaperComponent, PaperInputTextMessage>(OnInputTextMessage);
        SubscribeLocalEvent<PaperComponent, GetVerbsEvent<InteractionVerb>>(OnGetStampVerb);
        SubscribeLocalEvent<PaperComponent, PaperStampPlaceMessage>(OnPaperStamp);

        SubscribeLocalEvent<RandomPaperContentComponent, MapInitEvent>(OnRandomPaperContentMapInit);

        SubscribeLocalEvent<ActivateOnPaperOpenedComponent, PaperWriteEvent>(OnPaperWrite);
    }

    private void OnMapInit(Entity<PaperComponent> entity, ref MapInitEvent args)
    {
        if (!string.IsNullOrEmpty(entity.Comp.Content))
        {
            SetContent(entity, Loc.GetString(entity.Comp.Content));
        }
    }

    private void OnInit(Entity<PaperComponent> entity, ref ComponentInit args)
    {
        entity.Comp.Mode = PaperAction.Read;
        UpdateUserInterface(entity);

        if (TryComp<AppearanceComponent>(entity, out var appearance))
        {
            if (entity.Comp.Content != "")
                _appearance.SetData(entity, PaperVisuals.Status, PaperStatus.Written, appearance);

            if (entity.Comp.StampState != null)
                _appearance.SetData(entity, PaperVisuals.Stamp, entity.Comp.StampState, appearance);
        }
    }

    private void BeforeUIOpen(Entity<PaperComponent> entity, ref BeforeActivatableUIOpenEvent args)
    {
        entity.Comp.Mode = PaperAction.Read;
        UpdateUserInterface(entity);
    }

    /// <summary>
    /// Displays the paper's text and recorded stamp names when examined at close range.
    /// </summary>
    private void OnExamined(Entity<PaperComponent> entity, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(PaperComponent)))
        {
            if (entity.Comp.Content != "")
            {
                args.PushMarkup(
                    Loc.GetString(
                        "paper-component-examine-detail-has-words",
                        ("paper", entity)
                    )
                );
            }

            if (entity.Comp.StampedBy.Count > 0)
            {
                var commaSeparated =
                    string.Join(", ", entity.Comp.StampedBy.Select(s => s.LocalizeName ? Loc.GetString(s.StampedName) : s.StampedName));
                args.PushMarkup(
                    Loc.GetString(
                        "paper-component-examine-detail-stamped-by",
                        ("paper", entity),
                        ("stamps", commaSeparated))
                );
            }
        }
    }

    /// <summary>
    /// Handles writing or stamping interactions with the paper.
    /// </summary>
    /// <param name="entity">The paper being interacted with.</param>
    /// <param name="args">The interaction event containing the user and item used.</param>
    private void OnInteractUsing(Entity<PaperComponent> entity, ref InteractUsingEvent args)
    {
        // only allow editing if there are no stamps or when using a cyberpen
        var editable = entity.Comp.StampedBy.Count == 0 || _tagSystem.HasTag(args.Used, WriteIgnoreStampsTag);
        if (_tagSystem.HasTag(args.Used, WriteTag))
        {
            if (editable)
            {
                if (entity.Comp.EditingDisabled)
                {
                    var paperEditingDisabledMessage = Loc.GetString("paper-tamper-proof-modified-message");
                    _popupSystem.PopupEntity(paperEditingDisabledMessage, entity, args.User);

                    args.Handled = true;
                    return;
                }

                var ev = new PaperWriteAttemptEvent(entity.Owner);
                RaiseLocalEvent(args.User, ref ev);
                if (ev.Cancelled)
                {
                    if (ev.FailReason is not null)
                    {
                        var fileWriteMessage = Loc.GetString(ev.FailReason);
                        _popupSystem.PopupEntity(fileWriteMessage, entity.Owner, args.User);
                    }

                    args.Handled = true;
                    return;
                }

                var writeEvent = new PaperWriteEvent(args.User, entity);
                RaiseLocalEvent(args.Used, ref writeEvent);

                entity.Comp.Mode = PaperAction.Write;
                _uiSystem.OpenUi(entity.Owner, PaperUiKey.Key, args.User);
                UpdateUserInterface(entity);
            }
            args.Handled = true;
            return;
        }

        // If a stamp, attempt to stamp paper
        if (TryComp<StampComponent>(args.Used, out var stampComp) && TryStamp(entity, GetStampInfo(stampComp), stampComp.StampState))
        {
            // successfully stamped, play popup
            var stampPaperOtherMessage = Loc.GetString("paper-component-action-stamp-paper-other",
                    ("user", args.User),
                    ("target", args.Target),
                    ("stamp", args.Used));
            var stampPaperSelfMessage = Loc.GetString("paper-component-action-stamp-paper-self",
                    ("target", args.Target),
                    ("stamp", args.Used));
            _popupSystem.PopupEntity(stampPaperSelfMessage, stampPaperOtherMessage, args.User, args.User);

            _audio.PlayPredicted(stampComp.Sound, entity, args.User);

            UpdateUserInterface(entity);
        }
    }

    /// <summary>
    /// Creates display information for a stamp.
    /// </summary>
    /// <param name="stamp">The stamp component to convert.</param>
    /// <returns>Display information containing the stamp's name, color, icon, and localization setting.</returns>
    public static StampDisplayInfo GetStampInfo(StampComponent stamp)
    {
        return new StampDisplayInfo
        {
            StampedName = stamp.StampedName,
            StampedColor = stamp.StampedColor,
            StampLargeIcon = stamp.StampLargeIcon, // imp
            LocalizeName = true // stamp names are loc ids
        };
    }

    /// <summary>
    /// Adds a verb that allows a user to stamp the paper with the stamp they are using.
    /// </summary>
    /// <param name="ent">The paper entity receiving the stamp.</param>
    /// <param name="args">The interaction verb context.</param>
    private void OnGetStampVerb(Entity<PaperComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (args.Using is not { } stamp || !TryComp<StampComponent>(stamp, out var stampComp))
            return;

        var user = args.User;
        InteractionVerb verb = new()
        {
            Act = () =>
            {
                StartStampPlacement(ent, user, stamp);
            },
            Text = Loc.GetString("paper-stamp-verb"),
            DoContactInteraction = true,
        };
        args.Verbs.Add(verb);
    }

    /// <summary>
    ///     Opens the paper UI and tells the requesting client to enter stamp
    ///     placement mode. The stamp isn't committed here; it's committed later
    ///     when the client sends a <see cref="PaperComponent.PaperStampPlaceMessage"/>.
    /// <summary>
    /// Starts stamp placement for a user by opening the paper interface and requesting client-side placement.
    /// </summary>
    /// <param name="paper">The paper entity to stamp.</param>
    /// <param name="user">The user placing the stamp.</param>
    /// <param name="stamp">The stamp entity to place.</param>
    private void StartStampPlacement(Entity<PaperComponent> paper, EntityUid user, EntityUid stamp)
    {
        if (!_net.IsServer)
            return;

        if (!_player.TryGetSessionByEntity(user, out var session))
            return;

        _uiSystem.OpenUi(paper.Owner, PaperUiKey.Key, user);
        RaiseNetworkEvent(new PaperStampRequestEvent(GetNetEntity(paper.Owner), GetNetEntity(stamp)), session);
    }

    /// <summary>
    /// Validates and applies a stamp placement request to the paper.
    /// </summary>
    /// <param name="paper">The paper receiving the stamp.</param>
    /// <param name="args">The stamp placement request and its actor.</param>
    private void OnPaperStamp(Entity<PaperComponent> paper, ref PaperStampPlaceMessage args)
    {
        var user = args.Actor;

        if (!TryGetEntity(args.Stamp, out var stamp))
            return;

        if (!TryComp<StampComponent>(stamp, out var stampComp))
            return;

        // Re-validate: the user must still hold the stamp and be able to reach the
        // paper. A client-supplied message must not let a dropped/unheld stamp mark.
        if (!_hands.IsHolding(user, stamp.Value) ||
            !_interaction.InRangeUnobstructed(user, paper.Owner))
        {
            _popupSystem.PopupEntity(Loc.GetString("paper-stamp-failure", ("target", paper.Owner)), user, user, PopupType.SmallCaution);
            return;
        }

        var stampInfo = GetStampInfo(stampComp);
        // Sanitize the client-supplied transform against NaN/Infinity so a bad float
        // can't be persisted and networked to every viewer.
        var pos = args.Position;
        if (!float.IsFinite(pos.X) || !float.IsFinite(pos.Y))
            pos = new Vector2(0.5f, 0.5f);
        stampInfo.Position = Vector2.Clamp(pos, Vector2.Zero, Vector2.One);
        stampInfo.Rotation = float.IsFinite(args.Rotation) ? args.Rotation : 0f;
        stampInfo.Scale = 1f;

        if (!TryStamp(paper, stampInfo, stampComp.StampState))
            return;

        var stampPaperOtherMessage = Loc.GetString("paper-component-action-stamp-paper-other",
                ("user", user),
                ("target", paper.Owner),
                ("stamp", stamp.Value));
        var stampPaperSelfMessage = Loc.GetString("paper-component-action-stamp-paper-self",
                ("target", paper.Owner),
                ("stamp", stamp.Value));
        _popupSystem.PopupEntity(stampPaperSelfMessage, stampPaperOtherMessage, user, user);

        _audio.PlayPvs(stampComp.Sound, paper);

        UpdateUserInterface(paper);
    }

    /// <summary>
    /// Processes submitted paper text and returns the paper to read mode.
    /// </summary>
    /// <param name="entity">The paper receiving the text.</param>
    /// <param name="args">The submitted text and actor information.</param>
    private void OnInputTextMessage(Entity<PaperComponent> entity, ref PaperInputTextMessage args)
    {
        var ev = new PaperWriteAttemptEvent(entity.Owner);
        RaiseLocalEvent(args.Actor, ref ev);
        if (ev.Cancelled)
            return;

        if (args.Text.Length <= entity.Comp.ContentSize)
        {
            SetContent(entity, args.Text);

            var paperStatus = string.IsNullOrWhiteSpace(args.Text) ? PaperStatus.Blank : PaperStatus.Written;

            if (TryComp<AppearanceComponent>(entity, out var appearance))
                _appearance.SetData(entity, PaperVisuals.Status, paperStatus, appearance);

            if (TryComp(entity, out MetaDataComponent? meta))
                _metaSystem.SetEntityDescription(entity, "", meta);

            _adminLogger.Add(LogType.Chat,
                LogImpact.Low,
                $"{ToPrettyString(args.Actor):player} has written on {ToPrettyString(entity):entity} the following text: {args.Text}");

            _audio.PlayPvs(entity.Comp.Sound, entity);
        }

        entity.Comp.Mode = PaperAction.Read;
        UpdateUserInterface(entity);
    }

    private void OnRandomPaperContentMapInit(Entity<RandomPaperContentComponent> ent, ref MapInitEvent args)
    {
        if (!_paperQuery.TryComp(ent, out var paperComp))
        {
            Log.Warning($"{ToPrettyString(ent)} has a {nameof(RandomPaperContentComponent)} but no {nameof(PaperComponent)}!");
            RemCompDeferred(ent, ent.Comp);
            return;
        }
        var dataset = ProtoMan.Index(ent.Comp.Dataset);
        // Intentionally not using the Pick overload that directly takes a LocalizedDataset,
        // because we want to get multiple attributes from the same pick.
        var pick = _random.Pick(dataset.Values);

        // Name
        _metaSystem.SetEntityName(ent, Loc.GetString(pick));
        // Description
        _metaSystem.SetEntityDescription(ent, Loc.GetString($"{pick}.desc"));
        // Content
        SetContent((ent, paperComp), Loc.GetString($"{pick}.content"));

        // Our work here is done
        RemCompDeferred(ent, ent.Comp);
    }

    private void OnPaperWrite(Entity<ActivateOnPaperOpenedComponent> entity, ref PaperWriteEvent args)
    {
        _interaction.UseInHandInteraction(args.User, entity);
    }

    /// <summary>
    ///     Accepts the name and state to be stamped onto the paper, returns true if successful.
    /// <summary>
    /// Adds a stamp record to the paper and applies its visual state when the paper has not been stamped.
    /// </summary>
    /// <param name="stampInfo">The information to record for the stamp.</param>
    /// <param name="spriteStampState">The visual state used for the first stamp.</param>
    /// <returns><c>true</c> after the stamp record is added.</returns>
    public bool TryStamp(Entity<PaperComponent> entity, StampDisplayInfo stampInfo, string spriteStampState)
    {
        // Every stamp action adds a new mark, even if an identical one already exists.
        entity.Comp.StampedBy.Add(stampInfo);
        Dirty(entity);
        if (entity.Comp.StampState == null && TryComp<AppearanceComponent>(entity, out var appearance))
        {
            entity.Comp.StampState = spriteStampState;
            // Would be nice to be able to display multiple sprites on the paper
            // but most of the existing images overlap
            _appearance.SetData(entity, PaperVisuals.Stamp, entity.Comp.StampState, appearance);
        }
        return true;
    }

    /// <summary>
    ///     Copy any stamp information from one piece of paper to another.
    /// </summary>
    public void CopyStamps(Entity<PaperComponent?> source, Entity<PaperComponent?> target)
    {
        if (!Resolve(source, ref source.Comp) || !Resolve(target, ref target.Comp))
            return;

        target.Comp.StampedBy = new List<StampDisplayInfo>(source.Comp.StampedBy);
        target.Comp.StampState = source.Comp.StampState;
        Dirty(target);

        if (TryComp<AppearanceComponent>(target, out var appearance))
        {
            // delete any stamps if the stamp state is null
            _appearance.SetData(target, PaperVisuals.Stamp, target.Comp.StampState ?? "", appearance);
        }
    }

    public void SetContent(EntityUid entity, string content)
    {
        if (!TryComp<PaperComponent>(entity, out var paper))
            return;
        SetContent((entity, paper), content);
    }

    /// <summary>
    /// Sets the paper's content and updates its user interface and visual status.
    /// </summary>
    /// <param name="entity">The paper whose content should be updated.</param>
    /// <param name="content">The text to write on the paper.</param>
    public void SetContent(Entity<PaperComponent> entity, string content)
    {
        entity.Comp.Content = content;
        Dirty(entity);
        UpdateUserInterface(entity);

        if (!TryComp<AppearanceComponent>(entity, out var appearance))
            return;

        var status = string.IsNullOrWhiteSpace(content)
            ? PaperStatus.Blank
            : PaperStatus.Written;

        _appearance.SetData(entity, PaperVisuals.Status, status, appearance);
    }

    /// <summary>
    /// Updates the paper user interface with its current content, stamps, and interaction mode.
    /// </summary>
    /// <param name="entity">The paper entity whose user interface should be updated.</param>
    public void UpdateUserInterface(Entity<PaperComponent> entity)
    {
        if (!_net.IsServer)
            return;

        _uiSystem.SetUiState(entity.Owner, PaperUiKey.Key, new PaperBoundUserInterfaceState(entity.Comp.Content, entity.Comp.StampedBy, entity.Comp.Mode));
    }
}

/// <summary>
/// Event fired when using a pen on paper, opening the UI.
/// </summary>
[ByRefEvent]
public record struct PaperWriteEvent(EntityUid User, EntityUid Paper);

/// <summary>
/// Cancellable event for attempting to write on a piece of paper.
/// </summary>
/// <param name="paper">The paper that the writing will take place on.</param>
[ByRefEvent]
public record struct PaperWriteAttemptEvent(EntityUid Paper, string? FailReason = null, bool Cancelled = false);
