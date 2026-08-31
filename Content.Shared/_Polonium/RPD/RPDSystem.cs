// SPDX-FileCopyrightText: 2025 Steve <marlumpy@gmail.com>
// SPDX-FileCopyrightText: 2025 marc-pelletier <113944176+marc-pelletier@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <maciej.walendziuk@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Atmos.Piping;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.RCD.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.RPD;

/// <summary>
/// Everything the rapid pipe dispenser does on top of a plain RCD: picking an atmos pipe layer
/// from where in the tile the player clicked, building the matching per-layer pipe variant,
/// colouring what it builds, and refusing to deconstruct anything but piping.
/// </summary>
/// <remarks>
/// Hangs off the hook events RCDSystem raises rather than living inside it, so upstream RCD stays
/// mergeable.
/// </remarks>
public sealed class RPDSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAtmosPipeLayersSystem _pipeLayers = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    /// <summary>
    /// How far from the tile centre the cursor has to sit before it counts as aiming at a
    /// non-primary layer.
    /// </summary>
    private const float MouseDeadzoneRadius = 0.25f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RPDComponent, RPDColorChangeMessage>(OnColorChange);
        SubscribeLocalEvent<RPDComponent, RPDPipeLayerSelectEvent>(OnPipeLayerSelect);
        SubscribeLocalEvent<RPDComponent, RPDConstructPrototypeEvent>(OnConstructPrototype);
        SubscribeLocalEvent<RPDComponent, RPDObjectConstructedEvent>(OnObjectConstructed);
        SubscribeLocalEvent<RPDComponent, RPDDeconstructAttemptEvent>(OnDeconstructAttempt);

        SubscribeNetworkEvent<RPDEyeRotationEvent>(OnEyeRotation);
    }

    private void OnColorChange(Entity<RPDComponent> ent, ref RPDColorChangeMessage args)
    {
        ent.Comp.PipeColor = args.PipeColor;
        Dirty(ent);
    }

    private void OnEyeRotation(RPDEyeRotationEvent ev, EntitySessionEventArgs session)
    {
        var uid = GetEntity(ev.NetEntity);

        if (session.SenderSession.AttachedEntity is not { } player)
            return;

        if (_hands.GetActiveItem(player) != uid)
            return;

        if (!TryComp<RPDComponent>(uid, out var rpd))
            return;

        rpd.LastKnownEyeRotation = ev.EyeRotation;
    }

    /// <summary>
    /// Works out which pipe layer the click was aimed at, from the cursor's offset within the tile
    /// combined with the player's eye rotation and the grid's rotation. Mirrors what
    /// AlignRPDAtmosPipeLayers shows the player client-side.
    /// </summary>
    private void OnPipeLayerSelect(Entity<RPDComponent> ent, ref RPDPipeLayerSelectEvent args)
    {
        if (!UsesLayers(ent))
            return;

        if (ent.Comp.LastKnownEyeRotation is not { } eyeRotationTheta)
            return;

        var tile = args.Grid.Tile;
        var tileSize = args.Grid.Component.TileSize;
        var tileCenter = new Vector2(tile.X + tileSize / 2, tile.Y + tileSize / 2);
        var mouseCoordsDiff = args.ClickLocation.Position - tileCenter - new Vector2(0.5f, 0.5f);

        if (mouseCoordsDiff.Length() <= MouseDeadzoneRadius)
            return;

        var gridRotation = _transform.GetWorldRotation(args.Grid.GridUid);
        var angle = new Angle(mouseCoordsDiff);
        var eyeRotation = new Angle(eyeRotationTheta);
        var direction = (angle + eyeRotation + gridRotation + Math.PI / 2).GetCardinalDir();

        args.Layer = direction is Direction.North or Direction.East
            ? AtmosPipeLayer.Secondary
            : AtmosPipeLayer.Tertiary;
    }

    /// <summary>
    /// Swaps the prototype about to be spawned for its variant on the chosen layer.
    /// </summary>
    /// <remarks>
    /// The pipe has to be born on the right layer: spawning the primary variant and calling
    /// SetPipeLayer afterwards loses to PipeRestrictOverlapSystem, which checks at anchor time
    /// (i.e. while the pipe is still primary) and unanchors a secondary pipe placed to cross an
    /// existing primary one - exactly the case pipe layers exist for. The AtmosPipeLayer enum order
    /// matches the CreateVariants collection index, same as upstream's AlignAtmosPipeLayers.
    /// </remarks>
    private void OnConstructPrototype(Entity<RPDComponent> ent, ref RPDConstructPrototypeEvent args)
    {
        if (args.Layer == AtmosPipeLayer.Primary || args.Prototype == null || !UsesLayers(ent))
            return;

        if (!_protoManager.TryGetVariantCollection<EntityPrototype>(args.Prototype, out var variants) ||
            (int) args.Layer >= variants.Count)
        {
            return;
        }

        args.Prototype = variants[(int) args.Layer].Id;
    }

    private void OnObjectConstructed(Entity<RPDComponent> ent, ref RPDObjectConstructedEvent args)
    {
        // Fallback for layered entities that are not part of a variant collection, so were spawned
        // on the primary layer above. Entities that did get their layer variant are already correct
        // and skip this.
        if (args.Layer != AtmosPipeLayer.Primary &&
            UsesLayers(ent) &&
            TryComp<AtmosPipeLayersComponent>(args.Spawned, out var pipeLayers) &&
            pipeLayers.CurrentPipeLayer != args.Layer)
        {
            _pipeLayers.SetPipeLayer((args.Spawned, pipeLayers), args.Layer);
        }

        var (colorKey, color) = ent.Comp.PipeColor;

        if (colorKey != "default" && color != null)
            _appearance.SetData(args.Spawned, PipeColorVisuals.Color, color.Value);
    }

    /// <summary>
    /// The RPD only takes apart piping. It never deconstructs tiles, and only deconstructs entities
    /// explicitly whitelisted with <see cref="RPDDeconstructableComponent"/>.
    /// </summary>
    private void OnDeconstructAttempt(Entity<RPDComponent> ent, ref RPDDeconstructAttemptEvent args)
    {
        if (args.Target is not { } target || !HasComp<RPDDeconstructableComponent>(target))
            args.Cancelled = true;
    }

    /// <summary>
    /// Whether the recipe currently selected on this RPD is placed per pipe layer.
    /// </summary>
    private bool UsesLayers(EntityUid uid)
    {
        return TryComp<RCDComponent>(uid, out var rcd) && !rcd.CachedPrototype.NoLayers;
    }
}
