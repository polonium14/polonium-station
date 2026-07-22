using Content.Server.DeviceLinking.Systems;
using Content.Server.Medical.Components;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.HealthAnalyzer;
using Content.Shared.Body;
using Content.Shared.Buckle.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.MedicalScanner;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.Medical;

/// <summary>
/// Drives the body scanner console (<c>ComputerBodyScanner</c>). The console carries no scanner
/// of its own - it's a <see cref="DeviceLinkSourceComponent"/> pointed (via the network
/// configurator, same as every other linkable machine) at an operating table or medical bed's
/// <see cref="DeviceLinkSinkComponent"/>. On a periodic refresh while its BUI is open, this
/// system resolves whoever is buckled to the linked bed and reuses
/// <see cref="HealthAnalyzerSystem.SendAnalyzerUiState"/> to drive the exact same doll+tabs
/// Body/Organs/Chemicals window the handheld health analyzer uses (see
/// <c>Content.Client.HealthAnalyzer.UI.HealthAnalyzerWindow</c>) - the console is just another
/// entity opening the same UI key/BUI class, not a bespoke summary view.
/// </summary>
public sealed partial class BodyScannerConsoleSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private HealthAnalyzerSystem _healthAnalyzer = default!;
    [Dependency] private DeviceLinkSystem _deviceLink = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyScannerConsoleComponent, BoundUIOpenedEvent>(OnBoundUiOpened);

        Subs.BuiEvents<BodyScannerConsoleComponent>(HealthAnalyzerUiKey.Key, subs =>
        {
            subs.Event<HealthAnalyzerModeSelectedMessage>(OnModeSelected);
            subs.Event<HealthAnalyzerPartSelectedMessage>(OnPartSelected);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<BodyScannerConsoleComponent>();

        while (query.MoveNext(out var uid, out var console))
        {
            if (curTime < console.NextUiUpdateTime)
                continue;

            console.NextUiUpdateTime = curTime + console.UiUpdateInterval;
            UpdateUi((uid, console));
        }
    }

    private void OnBoundUiOpened(Entity<BodyScannerConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        // Push a state immediately on open so the window doesn't sit blank until the next
        // periodic refresh tick.
        UpdateUi(ent);
    }

    private void OnModeSelected(Entity<BodyScannerConsoleComponent> ent, ref HealthAnalyzerModeSelectedMessage args)
    {
        ent.Comp.CurrentMode = args.Mode;
        ent.Comp.CurrentBodyPart = null; // switching mode resets any limb drilldown, matches the handheld scanner
        UpdateUi(ent);
    }

    private void OnPartSelected(Entity<BodyScannerConsoleComponent> ent, ref HealthAnalyzerPartSelectedMessage args)
    {
        ent.Comp.CurrentMode = HealthAnalyzerMode.Body; // selecting a limb always jumps to the Body tab, matches the handheld scanner

        EntityUid? part = null;
        if (args.BodyPart is { } bodyPart
            && GetPatient(ent) is { } patient
            && LimbTargetMap.TryGetCategory(bodyPart, out var category)
            && TryComp<BodyComponent>(patient, out var body)
            && LimbTargetMap.TryGetOrganByCategory(EntityManager, body, category, out var organ))
            part = organ;

        ent.Comp.CurrentBodyPart = part;
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<BodyScannerConsoleComponent> console)
    {
        if (!_ui.IsUiOpen(console.Owner, HealthAnalyzerUiKey.Key))
            return;

        var patient = GetPatient(console);
        _healthAnalyzer.SendAnalyzerUiState(console.Owner, HealthAnalyzerUiKey.Key, patient, console.Comp.CurrentMode, console.Comp.CurrentBodyPart, patient != null);
    }

    private EntityUid? GetPatient(Entity<BodyScannerConsoleComponent> console)
    {
        var linkedBeds = TryComp<DeviceLinkSourceComponent>(console.Owner, out var source)
            ? _deviceLink.GetLinkedSinks((console.Owner, source), BodyScannerConsoleComponent.LinkPort)
            : new HashSet<EntityUid>();

        return GetFirstOccupant(linkedBeds);
    }

    /// <summary>
    /// A console can (in principle) be linked to several beds at once, since
    /// DeviceLinkSource/Sink natively support many-to-many links. There's no priority/ordering
    /// concept for beds worth building here, so we simply show the first linked bed that
    /// actually has someone buckled to it - a scanner watching several beds at once is an edge
    /// case anyway; normally one console watches one operating table/medical bed.
    /// </summary>
    private EntityUid? GetFirstOccupant(HashSet<EntityUid> beds)
    {
        foreach (var bed in beds)
        {
            if (!TryComp<StrapComponent>(bed, out var strap))
                continue;

            foreach (var occupant in strap.BuckledEntities)
                return occupant;
        }

        return null;
    }
}
