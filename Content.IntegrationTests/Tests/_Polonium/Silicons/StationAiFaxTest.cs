using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Pair;
using Content.Server._DV.Silicons;
using Content.Server.Fax;
using Content.Shared._DV.Silicons;
using Content.Shared.Fax;
using Content.Shared.Fax.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Paper;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Research.Prototypes;
using Content.Shared.Silicons.StationAi;
using NUnit.Framework;
using Robust.Shared;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Silicons;

/// <summary>
/// The station AI's fax module: discovery, the document archive, permissions
/// and what happens when the AI leaves its core or dies.
/// </summary>
[TestFixture]
[TestOf(typeof(StationAiFaxSystem))]
public sealed class StationAiFaxTest : GameTest
{
    private static readonly EntProtoId CoreProto = "PlayerStationAiEmpty";
    private static readonly EntProtoId BrainProto = "StationAiBrain";
    private static readonly EntProtoId FaxProto = "FaxMachineBase";
    private static readonly EntProtoId PaperProto = "Paper";
    private static readonly ProtoId<LatheRecipePrototype> AllowedTemplate = "PrintedDocumentReportStationRecipe";
    private static readonly ProtoId<LatheRecipePrototype> ForbiddenTemplate = "PrintedDocumentSyndicateSummonsRecipe";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task EnteringACoreGivesTheModuleAndAction()
    {
        var map = await Pair.CreateTestMap();
        var (_, ai, module) = await SpawnAi(map);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<StationAiFaxHolderComponent>(ai).ActionEntity, Is.Not.Null);
            Assert.That(SEntMan.GetComponent<FaxMachineComponent>(module).FaxName,
                Is.EqualTo(Server.ResolveDependency<ILocalizationManager>().GetString("job-name-station-ai")));
        });
    }

    [Test]
    public async Task ExistingFaxesLearnAboutTheAiWithoutARefresh()
    {
        var map = await Pair.CreateTestMap();
        var fax = await SpawnFax(map);
        var (_, _, module) = await SpawnAi(map);
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var faxComp = SEntMan.GetComponent<FaxMachineComponent>(fax);
            var moduleComp = SEntMan.GetComponent<FaxMachineComponent>(module);
            Assert.That(faxComp.KnownFaxes.Values, Does.Contain(moduleComp.FaxName));
            Assert.That(moduleComp.KnownFaxes.Values, Does.Contain(faxComp.FaxName));
        });
    }

    [Test]
    public async Task FaxSentToTheAiLandsInTheArchive()
    {
        var map = await Pair.CreateTestMap();
        var fax = await SpawnFax(map);
        var (_, ai, module) = await SpawnAi(map);
        await RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            var system = SEntMan.System<FaxSystem>();
            var faxComp = SEntMan.GetComponent<FaxMachineComponent>(fax);
            var moduleName = SEntMan.GetComponent<FaxMachineComponent>(module).FaxName;
            system.SetDestination(fax, faxComp.KnownFaxes.First(p => p.Value == moduleName).Key, faxComp);

            var paper = SEntMan.SpawnAtPosition(PaperProto, map.GridCoords);
            SEntMan.System<PaperSystem>().SetContent(paper, "for the AI");
            system.Send(fax, faxComp, new FaxSendMessage { Actor = ai }, paper);
            SEntMan.DeleteEntity(paper);
        });
        await RunSeconds(PrintDelay(module));

        await Server.WaitAssertion(() =>
        {
            var document = Library(module).Single();
            Assert.That(SEntMan.GetComponent<PaperComponent>(document).Content, Does.Contain("for the AI"));
            Assert.That(SEntMan.HasComponent<StationAiWhitelistComponent>(document));
            Assert.That(LoosePapers(), Is.Empty, "An AI's fax must not drop paper on the floor.");
        });
    }

    [Test]
    public async Task AiWritesPrintsAndSendsADocument()
    {
        var map = await Pair.CreateTestMap();
        var fax = await SpawnFax(map);
        var (_, ai, module) = await SpawnAi(map);
        await OpenFaxUi(ai, module);

        await SendBui(module, FaxUiKey.Key, new FaxFileMessage("draft", string.Empty, false));
        await RunSeconds(PrintDelay(module) + SendCooldown(module));

        EntityUid document = default;
        await Server.WaitPost(() => document = Library(module).Single());
        await SendBui(module, FaxUiKey.Key, new StationAiFaxOpenMessage(SEntMan.GetNetEntity(document)));
        await SendBui(document, PaperComponent.PaperUiKey.Key, new PaperComponent.PaperInputTextMessage("written by the AI"));

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<PaperComponent>(document).Content, Is.EqualTo("written by the AI")));

        await Server.WaitPost(() =>
        {
            var system = SEntMan.System<FaxSystem>();
            var moduleComp = SEntMan.GetComponent<FaxMachineComponent>(module);
            var faxName = SEntMan.GetComponent<FaxMachineComponent>(fax).FaxName;
            system.SetDestination(module, moduleComp.KnownFaxes.First(p => p.Value == faxName).Key, moduleComp);
        });
        await SendBui(module, FaxUiKey.Key, new StationAiFaxSendMessage(SEntMan.GetNetEntity(document)));
        await RunSeconds(PrintDelay(fax));

        await Server.WaitAssertion(() =>
        {
            var printed = LoosePapers().Single();
            Assert.That(SEntMan.GetComponent<PaperComponent>(printed).Content, Does.Contain("written by the AI"));
            Assert.That(Library(module), Has.Count.EqualTo(1), "Sending must keep the AI's own copy.");
        });
    }

    [Test]
    public async Task StampedDocumentsCannotBeEdited()
    {
        var map = await Pair.CreateTestMap();
        var (_, ai, module) = await SpawnAi(map);
        await OpenFaxUi(ai, module);

        await Server.WaitPost(() =>
        {
            var stamp = new StampDisplayInfo { StampedName = "stamp-component-stamped-name-default", StampedColor = Color.Red };
            SEntMan.System<FaxSystem>().Receive(module,
                new FaxPrintout("original", "stamped", stampState: "paper_stamp-generic", stampedBy: new List<StampDisplayInfo> { stamp }));
        });
        await RunSeconds(PrintDelay(module));

        EntityUid document = default;
        await Server.WaitPost(() => document = Library(module).Single());
        await SendBui(module, FaxUiKey.Key, new StationAiFaxOpenMessage(SEntMan.GetNetEntity(document)));
        await SendBui(document, PaperComponent.PaperUiKey.Key, new PaperComponent.PaperInputTextMessage("forged"));

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<PaperComponent>(document).Content, Is.EqualTo("original")));
    }

    [Test]
    public async Task FullArchiveDiscardsNewDocuments()
    {
        var map = await Pair.CreateTestMap();
        var (_, _, module) = await SpawnAi(map);

        await Server.WaitPost(() =>
        {
            SEntMan.GetComponent<StationAiFaxComponent>(module).MaxDocuments = 1;
            var system = SEntMan.System<FaxSystem>();
            system.Receive(module, new FaxPrintout("first", "first"));
            system.Receive(module, new FaxPrintout("second", "second"));
        });
        await RunSeconds(PrintDelay(module) * 2);

        await Server.WaitAssertion(() =>
        {
            var document = Library(module).Single();
            Assert.That(SEntMan.GetComponent<PaperComponent>(document).Content, Is.EqualTo("first"));
            Assert.That(LoosePapers(), Is.Empty);
        });
    }

    [Test]
    public async Task FilePrintingRespectsTheCooldown()
    {
        var map = await Pair.CreateTestMap();
        var (_, ai, module) = await SpawnAi(map);

        await Server.WaitAssertion(() =>
        {
            var system = SEntMan.System<FaxSystem>();
            var fax = SEntMan.GetComponent<FaxMachineComponent>(module);
            var message = new FaxFileMessage(null, "spam", false) { Actor = ai };
            system.PrintFile(module, fax, message);
            system.PrintFile(module, fax, message);
            Assert.That(fax.PrintingQueue, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task TemplatesAreLimitedToTheirPacksAndFilledIn()
    {
        var map = await Pair.CreateTestMap();
        var (_, ai, module) = await SpawnAi(map);
        await OpenFaxUi(ai, module);

        await SendBui(module, FaxUiKey.Key, new StationAiFaxTemplateMessage(ForbiddenTemplate));
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<FaxMachineComponent>(module).PrintingQueue, Is.Empty));

        await SendBui(module, FaxUiKey.Key, new StationAiFaxTemplateMessage(AllowedTemplate));
        await RunSeconds(PrintDelay(module));

        await Server.WaitAssertion(() =>
        {
            var loc = Server.ResolveDependency<ILocalizationManager>();
            var content = SEntMan.GetComponent<PaperComponent>(Library(module).Single()).Content;
            Assert.That(content, Does.Contain(SEntMan.GetComponent<MetaDataComponent>(ai).EntityName));
            Assert.That(content, Does.Contain(loc.GetString("job-name-station-ai")));
            Assert.That(content, Does.Not.Contain(loc.GetString("doc-var-name")));
            Assert.That(content, Does.Not.Contain(loc.GetString("doc-var-job")));
        });
    }

    [Test]
    public async Task LeavingTheCoreClosesTheFaxAndHidesIt()
    {
        var map = await Pair.CreateTestMap();
        var fax = await SpawnFax(map);
        var (core, ai, module) = await SpawnAi(map);
        await OpenFaxUi(ai, module);

        await Server.WaitPost(() =>
        {
            var containers = SEntMan.System<SharedContainerSystem>();
            containers.Remove(ai, containers.GetContainer(core, StationAiCoreComponent.Container));
        });
        await RunTicksSync(10);

        await Server.WaitPost(() => SEntMan.System<FaxSystem>().Refresh(fax));
        await RunTicksSync(5);

        var moduleName = string.Empty;
        await Server.WaitPost(() => moduleName = SEntMan.GetComponent<FaxMachineComponent>(module).FaxName);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<SharedUserInterfaceSystem>().IsUiOpen(module, FaxUiKey.Key, ai), Is.False);
            Assert.That(SEntMan.HasComponent<StationAiFaxHolderComponent>(ai), Is.False);
            Assert.That(SEntMan.GetComponent<FaxMachineComponent>(fax).KnownFaxes.Values, Does.Not.Contain(moduleName));
        });

        await Server.WaitPost(() =>
        {
            var containers = SEntMan.System<SharedContainerSystem>();
            containers.Insert(ai, containers.GetContainer(core, StationAiCoreComponent.Container));
        });
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var slot = SEntMan.System<SharedContainerSystem>().GetContainer(ai, StationAiFaxHolderComponent.Container);
            Assert.That(slot.ContainedEntities.Single(), Is.EqualTo(module), "Returning must keep the same archive.");
            Assert.That(SEntMan.GetComponent<FaxMachineComponent>(fax).KnownFaxes.Values, Does.Contain(moduleName));
        });
    }

    [Test]
    public async Task DeadAiIsHiddenUntilRevived()
    {
        var map = await Pair.CreateTestMap();
        var fax = await SpawnFax(map);
        var (_, ai, module) = await SpawnAi(map);
        await OpenFaxUi(ai, module);

        await Server.WaitPost(() => SEntMan.System<MobStateSystem>().ChangeMobState(ai, MobState.Dead));
        await RunTicksSync(5);
        await Server.WaitPost(() => SEntMan.System<FaxSystem>().Refresh(fax));
        await RunTicksSync(5);

        var moduleName = string.Empty;
        await Server.WaitPost(() => moduleName = SEntMan.GetComponent<FaxMachineComponent>(module).FaxName);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<SharedUserInterfaceSystem>().IsUiOpen(module, FaxUiKey.Key, ai), Is.False);
            Assert.That(SEntMan.GetComponent<FaxMachineComponent>(fax).KnownFaxes.Values, Does.Not.Contain(moduleName));
        });

        await Server.WaitPost(() => SEntMan.System<MobStateSystem>().ChangeMobState(ai, MobState.Alive));
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<FaxMachineComponent>(fax).KnownFaxes.Values, Does.Contain(moduleName)));
    }

    [Test]
    public async Task ArchiveReachesTheClientWhileTheEyeIsFarAway()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() => Server.CfgMan.SetCVar(CVars.NetPVS, true));
        var (core, ai, module) = await SpawnAi(map);

        await Server.WaitPost(() => SEntMan.System<FaxSystem>().Receive(module, new FaxPrintout("archived", "archived")));
        await RunSeconds(PrintDelay(module));

        NetEntity netModule = default, netDocument = default;
        await Server.WaitPost(() =>
        {
            netModule = SEntMan.GetNetEntity(module);
            netDocument = SEntMan.GetNetEntity(Library(module).Single());

            var eye = SEntMan.GetComponent<StationAiCoreComponent>(core).RemoteEntity!.Value;
            SEntMan.System<SharedTransformSystem>().SetCoordinates(eye, new EntityCoordinates(map.MapUid, 2000, 2000));
        });
        await RunTicksSync(30);

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.TryGetEntity(netModule, out var clientModule), "The module must reach the AI's client.");
            Assert.That(CEntMan.TryGetEntity(netDocument, out var clientDocument), "Documents must reach the AI's client.");
            var library = CEntMan.System<SharedContainerSystem>().GetContainer(clientModule!.Value, StationAiFaxComponent.Container);
            Assert.That(library.ContainedEntities, Does.Contain(clientDocument!.Value));
        });

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<SharedUserInterfaceSystem>().TryOpenUi(module, FaxUiKey.Key, ai)));
    }

    private async Task<EntityUid> SpawnFax(TestMapData map)
    {
        EntityUid fax = default;
        await Server.WaitPost(() =>
        {
            fax = SEntMan.SpawnAtPosition(FaxProto, map.GridCoords);
            SEntMan.System<SharedPowerReceiverSystem>().SetNeedsPower(fax, false);
        });
        await RunTicksSync(5);
        return fax;
    }

    private async Task<(EntityUid Core, EntityUid Ai, EntityUid Module)> SpawnAi(TestMapData map)
    {
        EntityUid core = default, ai = default, module = default;
        await Server.WaitPost(() =>
        {
            core = SEntMan.SpawnAtPosition(CoreProto, map.GridCoords);
            SEntMan.System<SharedPowerReceiverSystem>().SetNeedsPower(core, false);

            ai = SEntMan.SpawnAtPosition(BrainProto, map.GridCoords);
            var containers = SEntMan.System<SharedContainerSystem>();
            Assert.That(containers.Insert(ai, containers.GetContainer(core, StationAiCoreComponent.Container)));
            SEntMan.System<SharedMindSystem>().ControlMob(ServerSession!.UserId, ai);

            module = containers.GetContainer(ai, StationAiFaxHolderComponent.Container).ContainedEntities.Single();
        });
        await RunTicksSync(10);
        return (core, ai, module);
    }

    private async Task OpenFaxUi(EntityUid ai, EntityUid module)
    {
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<SharedUserInterfaceSystem>().TryOpenUi(module, FaxUiKey.Key, ai)));
        await RunTicksSync(15);
    }

    private async Task SendBui(EntityUid target, Enum key, BoundUserInterfaceMessage message)
    {
        var netTarget = SEntMan.GetNetEntity(target);
        await Client.WaitPost(() =>
        {
            var ui = CEntMan.GetComponent<UserInterfaceComponent>(CEntMan.GetEntity(netTarget));
            Assert.That(ui.ClientOpenInterfaces.TryGetValue(key, out var bui), $"The client has no open {key} UI.");
            bui!.SendMessage(message);
        });
        await RunTicksSync(15);
    }

    private IReadOnlyList<EntityUid> Library(EntityUid module)
    {
        return SEntMan.System<SharedContainerSystem>().GetContainer(module, StationAiFaxComponent.Container).ContainedEntities;
    }

    private List<EntityUid> LoosePapers()
    {
        var containers = SEntMan.System<SharedContainerSystem>();
        var papers = new List<EntityUid>();
        var query = SEntMan.EntityQueryEnumerator<PaperComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!containers.IsEntityInContainer(uid))
                papers.Add(uid);
        }

        return papers;
    }

    private float PrintDelay(EntityUid fax)
    {
        return SEntMan.GetComponent<FaxMachineComponent>(fax).PrintingTime + 0.5f;
    }

    private float SendCooldown(EntityUid fax)
    {
        return SEntMan.GetComponent<FaxMachineComponent>(fax).SendTimeout;
    }
}
