using Content.Server._Corvax.Documents;
using Content.Server._Funkystation.Fax;
using Content.Server.Fax;
using Content.Server.Station.Systems;
using Content.Shared._DV.Fax;
using Content.Shared._DV.Silicons;
using Content.Shared._Funkystation.Fax;
using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Database;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.DeviceNetwork.Systems;
using Content.Shared.Fax;
using Content.Shared.Fax.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Paper;
using Content.Shared.Research.Prototypes;
using Content.Shared.Silicons.StationAi;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._DV.Silicons;

/// <summary>
/// Lets the station AI work with faxes through a hidden fax machine it carries in its brain.
/// </summary>
public sealed partial class StationAiFaxSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DeviceNetworkSystem _deviceNetwork = default!;
    [Dependency] private DocumentPrinterSystem _documents = default!;
    [Dependency] private FaxSystem _fax = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private ServerStationSystem _station = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly LocId AiJobName = "job-name-station-ai";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAiFaxHolderComponent, MapInitEvent>(OnHolderMapInit);
        SubscribeLocalEvent<StationAiFaxHolderComponent, ComponentShutdown>(OnHolderShutdown);
        SubscribeLocalEvent<StationAiFaxHolderComponent, StationAiOpenFaxEvent>(OnOpenFax);
        SubscribeLocalEvent<StationAiFaxHolderComponent, MobStateChangedEvent>(OnMobStateChanged);

        SubscribeLocalEvent<StationAiFaxComponent, MapInitEvent>(OnModuleMapInit);
        SubscribeLocalEvent<StationAiFaxComponent, DeviceNetworkPacketEvent<FaxPingPayload>>(OnPing, before: [typeof(FaxSystem)]);
        SubscribeLocalEvent<StationAiFaxComponent, BoundUserInterfaceMessageAttempt>(OnModuleUiAttempt);
        SubscribeLocalEvent<StationAiFaxComponent, FaxReceivedEvent>(OnFaxReceived);
        SubscribeLocalEvent<StationAiFaxComponent, FaxPaperPrintedEvent>(OnPaperPrinted, before: [typeof(HandheldFaxSystem)]);

        SubscribeLocalEvent<StationAiFaxDocumentComponent, BoundUserInterfaceMessageAttempt>(OnDocumentUiAttempt);

        Subs.BuiEvents<StationAiFaxComponent>(FaxUiKey.Key,
            subs =>
            {
                subs.Event<BoundUIOpenedEvent>(OnUiOpened);
                subs.Event<StationAiFaxOpenMessage>(OnOpenDocument);
                subs.Event<StationAiFaxCopyMessage>(OnCopyDocument);
                subs.Event<StationAiFaxSendMessage>(OnSendDocument);
                subs.Event<StationAiFaxShredMessage>(OnShredDocument);
                subs.Event<StationAiFaxTemplateMessage>(OnTemplate);
            });
    }

    private void OnHolderMapInit(Entity<StationAiFaxHolderComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);

        var slot = _container.EnsureContainer<ContainerSlot>(ent, StationAiFaxHolderComponent.Container);
        if (slot.ContainedEntity is not { } module)
        {
            if (!TrySpawnInContainer(ent.Comp.Module, ent, StationAiFaxHolderComponent.Container, out var spawned))
                return;

            module = spawned.Value;
        }

        _fax.Refresh(module);
        Announce(module);
    }

    private void OnHolderShutdown(Entity<StationAiFaxHolderComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);

        if (!TerminatingOrDeleted(ent) && GetModule(ent) is { } module)
            CloseUis(ent, module);
    }

    private void OnOpenFax(Entity<StationAiFaxHolderComponent> ent, ref StationAiOpenFaxEvent args)
    {
        if (args.Handled || _mobState.IsDead(ent) || GetModule(ent) is not { } module)
            return;

        args.Handled = _ui.TryToggleUi(module, FaxUiKey.Key, ent.Owner);
    }

    private void OnMobStateChanged(Entity<StationAiFaxHolderComponent> ent, ref MobStateChangedEvent args)
    {
        if (GetModule(ent) is not { } module)
            return;

        if (args.NewMobState == MobState.Dead)
            CloseUis(ent, module);
        else if (args.OldMobState == MobState.Dead)
            Announce(module);
    }

    private void OnModuleMapInit(Entity<StationAiFaxComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<FaxMachineComponent>(ent, out var fax))
            fax.FaxName = Loc.GetString(AiJobName);
    }

    private void OnPing(Entity<StationAiFaxComponent> ent, ref DeviceNetworkPacketEvent<FaxPingPayload> args)
    {
        if (TryComp<FaxMachineComponent>(ent, out var fax))
            fax.ResponsePings = IsAvailable(ent, out _);
    }

    private void OnModuleUiAttempt(Entity<StationAiFaxComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (!IsAvailable(ent, out var ai) || args.Actor != ai)
            args.Cancel();
    }

    private void OnDocumentUiAttempt(Entity<StationAiFaxDocumentComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        var module = Transform(ent).ParentUid;
        if (!HasComp<StationAiFaxComponent>(module))
            return;

        if (!IsAvailable(module, out var ai) || args.Actor != ai)
        {
            args.Cancel();
            return;
        }

        var allowed = args.Message switch
        {
            OpenBoundInterfaceMessage => true,
            PaperComponent.PaperInputTextMessage => TryComp<PaperComponent>(ent, out var paper) && IsEditable(paper),
            _ => false,
        };

        if (!allowed)
            args.Cancel();
    }

    private void OnUiOpened(Entity<StationAiFaxComponent> ent, ref BoundUIOpenedEvent args)
    {
        _fax.UpdateUserInterface(ent);
    }

    private void OnFaxReceived(Entity<StationAiFaxComponent> ent, ref FaxReceivedEvent args)
    {
        Notify(ent, ent.Comp.ReceivedNotification, FormattedMessage.EscapeText(args.SenderName));
    }

    private void OnPaperPrinted(Entity<StationAiFaxComponent> ent, ref FaxPaperPrintedEvent args)
    {
        args.Handled = true;

        var library = _container.EnsureContainer<Container>(ent, StationAiFaxComponent.Container);
        if (library.ContainedEntities.Count >= ent.Comp.MaxDocuments ||
            !_container.Insert(args.Paper, library, force: true))
        {
            QueueDel(args.Paper);
            Notify(ent, ent.Comp.ArchiveFullNotification);
            return;
        }

        EnsureComp<StationAiWhitelistComponent>(args.Paper);
        EnsureComp<StationAiFaxDocumentComponent>(args.Paper);
    }

    private void OnOpenDocument(Entity<StationAiFaxComponent> ent, ref StationAiFaxOpenMessage args)
    {
        if (GetDocument(ent, args.Document) is not { } document ||
            !TryComp<PaperComponent>(document, out var paper))
            return;

        paper.Mode = IsEditable(paper) ? PaperComponent.PaperAction.Write : PaperComponent.PaperAction.Read;
        _paper.UpdateUserInterface((document, paper));
        _ui.OpenUi(document, PaperComponent.PaperUiKey.Key, args.Actor);
    }

    private void OnCopyDocument(Entity<StationAiFaxComponent> ent, ref StationAiFaxCopyMessage args)
    {
        if (GetDocument(ent, args.Document) is { } document)
            _fax.Copy(ent, null, args, document);
    }

    private void OnSendDocument(Entity<StationAiFaxComponent> ent, ref StationAiFaxSendMessage args)
    {
        if (GetDocument(ent, args.Document) is { } document)
            _fax.Send(ent, null, args, document);
    }

    private void OnShredDocument(Entity<StationAiFaxComponent> ent, ref StationAiFaxShredMessage args)
    {
        if (GetDocument(ent, args.Document) is not { } document)
            return;

        _adminLogger.Add(LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(args.Actor):actor} shredded {ToPrettyString(document):subject} in the AI fax archive");

        QueueDel(document);
    }

    private void OnTemplate(Entity<StationAiFaxComponent> ent, ref StationAiFaxTemplateMessage args)
    {
        if (!TryComp<FaxMachineComponent>(ent, out var fax) ||
            !IsTemplate(ent.Comp, args.Recipe) ||
            !ProtoMan.TryIndex(args.Recipe, out var recipe) ||
            !ProtoMan.TryIndex(recipe.Result, out var result) ||
            !result.TryComp<PaperComponent>(out var template, EntityManager.ComponentFactory))
            return;

        var content = Loc.HasString(template.Content) ? Loc.GetString(template.Content) : template.Content;
        content = content
            .Replace(Loc.GetString("doc-var-name"), FormattedMessage.EscapeText(Name(args.Actor)))
            .Replace(Loc.GetString("doc-var-job"), Loc.GetString(AiJobName));

        var station = _station.GetOwningStation(args.Actor);
        content = _documents.FormatString(content, station is { } stationUid ? Name(stationUid) : null);

        var label = result.Name[..Math.Min(result.Name.Length, FaxFileMessageValidation.MaxLabelSize)];
        _fax.PrintFile(ent, fax, new FaxFileMessage(label, content, false) { Actor = args.Actor });
    }

    private bool IsTemplate(StationAiFaxComponent fax, ProtoId<LatheRecipePrototype> recipe)
    {
        foreach (var pack in fax.TemplatePacks)
        {
            if (ProtoMan.Index(pack).Recipes.Contains(recipe))
                return true;
        }

        return false;
    }

    private static bool IsEditable(PaperComponent paper)
    {
        return !paper.EditingDisabled && paper.StampedBy.Count == 0;
    }

    private bool IsAvailable(EntityUid module, out EntityUid ai)
    {
        ai = Transform(module).ParentUid;
        return HasComp<StationAiFaxHolderComponent>(ai) && !_mobState.IsDead(ai);
    }

    private EntityUid? GetModule(EntityUid ai)
    {
        return _container.TryGetContainer(ai, StationAiFaxHolderComponent.Container, out var slot) &&
               slot.ContainedEntities.Count > 0
            ? slot.ContainedEntities[0]
            : null;
    }

    private EntityUid? GetDocument(Entity<StationAiFaxComponent> ent, NetEntity document)
    {
        return TryGetEntity(document, out var uid) &&
               _container.TryGetContainer(ent, StationAiFaxComponent.Container, out var library) &&
               library.Contains(uid.Value)
            ? uid
            : null;
    }

    /// <summary>
    /// Faxes only learn about a device from its pong, so tell them this one has come online.
    /// </summary>
    private void Announce(EntityUid module)
    {
        if (!TryComp<FaxMachineComponent>(module, out var fax))
            return;

        var pong = new FaxPongPayload { FaxName = fax.FaxName };
        _deviceNetwork.SendPacket(module, null, ref pong);
    }

    private void CloseUis(EntityUid ai, EntityUid module)
    {
        _ui.CloseUi(module, FaxUiKey.Key, ai);

        if (!_container.TryGetContainer(module, StationAiFaxComponent.Container, out var library))
            return;

        foreach (var document in library.ContainedEntities)
        {
            _ui.CloseUi(document, PaperComponent.PaperUiKey.Key, ai);
        }
    }

    private void Notify(EntityUid module, ProtoId<ChatNotificationPrototype> notification, string? source = null)
    {
        if (!IsAvailable(module, out var ai))
            return;

        var ev = new ChatNotificationEvent(notification, module) { SourceNameOverride = source };
        RaiseLocalEvent(ai, ref ev);
    }
}
