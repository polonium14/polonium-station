// SPDX-FileCopyrightText: 2025 Mehnix <56132549+Mehnix@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Server.PowerCell;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.BaseAnalyzer;

public abstract class BaseAnalyzerSystem<TAnalyzerComponent, TAnalyzerDoAfterEvent> : EntitySystem
    where TAnalyzerComponent : BaseAnalyzerComponent
    where TAnalyzerDoAfterEvent : SimpleDoAfterEvent, new()
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly PowerCellSystem Cell = default!;
    [Dependency] protected readonly SharedAudioSystem Audio = default!;
    [Dependency] protected readonly SharedDoAfterSystem DoAfterSystem = default!;
    [Dependency] protected readonly ItemToggleSystem Toggle = default!;
    [Dependency] protected readonly UserInterfaceSystem UiSystem = default!;
    [Dependency] protected readonly TransformSystem TransformSystem = default!;
    [Dependency] protected readonly SharedPopupSystem PopupSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<TAnalyzerComponent, TAnalyzerDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<TAnalyzerComponent, EntGotInsertedIntoContainerMessage>(OnInsertedIntoContainer);
        SubscribeLocalEvent<TAnalyzerComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<TAnalyzerComponent, DroppedEvent>(OnDropped);
    }

    public override void Update(float frameTime)
    {
        var analyzerQuery = EntityQueryEnumerator<TAnalyzerComponent, TransformComponent>();
        while (analyzerQuery.MoveNext(out var uid, out var component, out var transform))
        {
            //Update rate limited to 1 second
            if (component.NextUpdate > Timing.CurTime)
                continue;

            if (component.ScannedEntity is not { } target)
                continue;

            if (Deleted(target))
            {
                StopAnalyzingEntity((uid, component), target);
                continue;
            }

            component.NextUpdate = Timing.CurTime + component.UpdateInterval;

            //Get distance between analyzer and the scanned entity
            var targetCoordinates = Transform(target).Coordinates;
            if (component.MaxScanRange != null && !TransformSystem.InRange(targetCoordinates, transform.Coordinates, component.MaxScanRange.Value))
            {
                //Range too far, disable updates
                StopAnalyzingEntity((uid, component), target);
                continue;
            }

            UpdateScannedUser(uid, target, true);
        }
    }

    /// <summary>
    /// Trigger the doafter for scanning
    /// </summary>
    public void OnAfterInteract(Entity<TAnalyzerComponent> uid, ref AfterInteractEvent args)
    {
        if (args.Target == null || !args.CanReach || !ValidScanTarget(args.Target) || !Cell.HasDrawCharge(uid, user: args.User))
            return;

        Audio.PlayPvs(uid.Comp.ScanningBeginSound, uid);

        var doAfterCancelled = !DoAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, uid.Comp.ScanDelay, new TAnalyzerDoAfterEvent(), uid, target: args.Target, used: uid)
        {
            NeedHand = true,
            BreakOnMove = true,
        });

        if (args.Target == args.User || doAfterCancelled || uid.Comp.Silent || args.Target is null)
            return;

        if (ScanTargetPopupMessage(uid, args, out var msg))
            PopupSystem.PopupEntity(msg, args.Target.Value, args.Target.Value, PopupType.Medium);
    }

    public void OnDoAfter(Entity<TAnalyzerComponent> uid, ref TAnalyzerDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target == null || !Cell.HasDrawCharge(uid, user: args.User))
            return;

        if (!uid.Comp.Silent)
            Audio.PlayPvs(uid.Comp.ScanningEndSound, uid);

        OpenUserInterface(args.User, uid);
        BeginAnalyzingEntity(uid, args.Target.Value);
        args.Handled = true;
    }

    /// <summary>
    /// Turn off when placed into a storage item or moved between slots/hands
    /// </summary>
    public void OnInsertedIntoContainer(Entity<TAnalyzerComponent> uid, ref EntGotInsertedIntoContainerMessage args)
    {
        if (uid.Comp.ScannedEntity is { })
            Toggle.TryDeactivate(uid.Owner);
    }

    /// <summary>
    /// Disable continuous updates once turned off
    /// </summary>
    public void OnToggled(Entity<TAnalyzerComponent> ent, ref ItemToggledEvent args)
    {
        if (!args.Activated && ent.Comp.ScannedEntity is { } target)
            StopAnalyzingEntity(ent, target);
    }

    /// <summary>
    /// Turn off the analyser when dropped
    /// </summary>
    public void OnDropped(Entity<TAnalyzerComponent> uid, ref DroppedEvent args)
    {
        if (uid.Comp.ScannedEntity is { })
            Toggle.TryDeactivate(uid.Owner);
    }

    private void OpenUserInterface(EntityUid user, EntityUid analyzer)
    {
        if (!UiSystem.HasUi(analyzer, GetUiKey()))
            return;

        UiSystem.OpenUi(analyzer, GetUiKey(), user);
    }

    /// <summary>
    /// Mark the entity as being analyzed, and link the analyzer to it
    /// </summary>
    /// <param name="analyzer">The analyzer that should receive the updates</param>
    /// <param name="target">The entity to start analyzing</param>
    public abstract void BeginAnalyzingEntity(Entity<TAnalyzerComponent> analyzer, EntityUid target, EntityUid? part = null);
    

    /// <summary>
    /// Remove the analyzer from the active list, and remove the component if it has no active analyzers
    /// </summary>
    /// <param name="analyzer">The analyzer that's receiving the updates</param>
    /// <param name="target">The entity to analyze</param>
    private void StopAnalyzingEntity(Entity<TAnalyzerComponent> analyzer, EntityUid target)
    {
        //Unlink the analyzer
        analyzer.Comp.ScannedEntity = null;

        Toggle.TryDeactivate(analyzer.Owner);

        UpdateScannedUser(analyzer, target, false);
    }

    /// <summary>
    /// Send an update for the target to the analyzer
    /// </summary>
    /// <param name="analyzer">The analyzer</param>
    /// <param name="target">The entity being scanned</param>
    /// <param name="scanMode">True makes the UI show ACTIVE, False makes the UI show INACTIVE</param>
    public abstract void UpdateScannedUser(EntityUid analyzer, EntityUid target, bool scanMode, EntityUid? part = null);

    /// <returns>A <see cref="Robust.Shared.Serialization.NetSerializableAttribute"/> byte enum key.</returns>
    protected abstract Enum GetUiKey();

    /// <summary>
    /// The message the scan target recieves on scan.
    /// </summary>
    /// <returns>true if the message should be shown</returns>
    protected abstract bool ScanTargetPopupMessage(Entity<TAnalyzerComponent> uid, AfterInteractEvent args, [NotNullWhen(true)] out string? message);

    /// <summary>
    /// Used to validate if a specific entity is a valid target for a specific analyzer.
    /// </summary>
    protected abstract bool ValidScanTarget(EntityUid? target);
}
