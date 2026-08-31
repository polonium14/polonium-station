// SPDX-FileCopyrightText: 2025 Steve <marlumpy@gmail.com>
// SPDX-FileCopyrightText: 2025 marc-pelletier <113944176+marc-pelletier@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <maciej.walendziuk@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Atmos.Components;
using Content.Shared.RCD.Systems;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.RPD;

/// <summary>
/// Client -> server push of the holder's eye rotation, which the server needs for pipe layer
/// selection but cannot read itself (eye rotation is not networked).
/// </summary>
[Serializable, NetSerializable]
public sealed class RPDEyeRotationEvent : EntityEventArgs
{
    public readonly NetEntity NetEntity;
    public float? EyeRotation;

    public RPDEyeRotationEvent(NetEntity netEntity, float? eyeRotation)
    {
        NetEntity = netEntity;
        EyeRotation = eyeRotation;
    }
}

/// <summary>
/// Sent by the RPD menu when the player picks a pipe colour.
/// </summary>
[Serializable, NetSerializable]
public sealed class RPDColorChangeMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity NetEntity;
    public readonly (string Key, Color? Color) PipeColor;

    public RPDColorChangeMessage(NetEntity entity, (string Key, Color? Color) pipeColor)
    {
        NetEntity = entity;
        PipeColor = pipeColor;
    }
}

[Serializable, NetSerializable]
public enum RpdUiKey : byte
{
    Key
}

/// <summary>
/// Raised on an RCD when a construction click lands, so an RPD can pick the atmos pipe layer the
/// player aimed at. The result is snapshotted into the do-after, so it survives the delay.
/// </summary>
[ByRefEvent]
public struct RPDPipeLayerSelectEvent
{
    public readonly EntityCoordinates ClickLocation;
    public readonly MapGridData Grid;
    public AtmosPipeLayer Layer;

    public RPDPipeLayerSelectEvent(EntityCoordinates clickLocation, MapGridData grid)
    {
        ClickLocation = clickLocation;
        Grid = grid;
        Layer = AtmosPipeLayer.Primary;
    }
}

/// <summary>
/// Raised on an RCD just before it spawns a constructed entity, so an RPD can swap in the pipe
/// variant belonging to <see cref="Layer"/>.
/// </summary>
[ByRefEvent]
public struct RPDConstructPrototypeEvent
{
    public readonly AtmosPipeLayer Layer;
    public string? Prototype;

    public RPDConstructPrototypeEvent(string? prototype, AtmosPipeLayer layer)
    {
        Prototype = prototype;
        Layer = layer;
    }
}

/// <summary>
/// Raised on an RCD right after it spawned a constructed entity.
/// </summary>
[ByRefEvent]
public struct RPDObjectConstructedEvent
{
    public readonly EntityUid Spawned;
    public readonly AtmosPipeLayer Layer;

    public RPDObjectConstructedEvent(EntityUid spawned, AtmosPipeLayer layer)
    {
        Spawned = spawned;
        Layer = layer;
    }
}

/// <summary>
/// Raised on an RCD before a deconstruction, so an RPD can veto anything outside its whitelist.
/// </summary>
[ByRefEvent]
public struct RPDDeconstructAttemptEvent
{
    public readonly EntityUid? Target;
    public bool Cancelled;

    public RPDDeconstructAttemptEvent(EntityUid? target)
    {
        Target = target;
        Cancelled = false;
    }
}
