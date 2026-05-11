// SPDX-FileCopyrightText: 2024 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Actions;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Weapons.Ranged.Components;

/// <summary>
/// Lets you shoot a gun using an action.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(ActionGunSystem))]
public sealed partial class ActionGunComponent : Component
{
    /// <summary>
    /// Action to create, must use <see cref="ActionGunShootEvent"/>.
    /// </summary>
    [DataField]
    public EntProtoId Action = string.Empty;

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    /// <summary>
    /// Prototype of gun entity to spawn.
    /// Deleted when this component is removed.
    /// </summary>
    [DataField]
    public EntProtoId GunProto = string.Empty;

    [DataField, AutoNetworkedField]
    public EntityUid? Gun;

    /// <summary>
    /// Multiple actions to their gun prototypes.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, EntProtoId> Actions = new();

    [DataField, AutoNetworkedField]
    public Dictionary<EntProtoId, EntityUid> ActionEntities = new();

    [DataField, AutoNetworkedField]
    public Dictionary<EntityUid, EntityUid> Guns = new();
}

/// <summary>
/// Action event for <see cref="ActionGunComponent"/> to shoot at a position.
/// </summary>
public sealed partial class ActionGunShootEvent : WorldTargetActionEvent;
