// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.FixedPoint;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Storage.Components;

/// <summary>
/// Cheek pouches. Gives the entity a small hidden storage container in its mouth.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedMouthStorageSystem))]
public sealed partial class MouthStorageComponent : Component
{
    public const string MouthContainerId = "mouth";

    [DataField, AutoNetworkedField]
    public EntProtoId? OpenStorageAction;

    [DataField, AutoNetworkedField]
    public EntityUid? Action;

    [DataField]
    public EntProtoId MouthProto = "CheekStorage";

    [ViewVariables]
    public Container Mouth = default!;

    [DataField]
    public EntityUid? MouthId;

    /// <summary>
    /// Minimum inflicted damage on hit to spit out items.
    /// </summary>
    [DataField]
    public FixedPoint2 SpitDamageThreshold = FixedPoint2.New(2);
}
