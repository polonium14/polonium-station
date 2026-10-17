// SPDX-FileCopyrightText: 2025 beck <163376292+widgetbeck@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// these are HEAVILY based on the Bingle free-agent ghostrole from GoobStation, but reflavored and reprogrammed to make them more Robust (and less of a meme.)
// all credit for the core gameplay concepts and a lot of the core functionality of the code goes to the folks over at Goob, but I re-wrote enough of it to justify putting it in our filestructure.
// the original Bingle PR can be found here: https://github.com/Goob-Station/Goob-Station/pull/1519

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.Teleportation.Systems;

namespace Content.Shared._Impstation.Replicator;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState] // POLONIUM: state
public sealed partial class ReplicatorComponent : Component
{
    /// <summary>
    /// If a replicator is Queen, it will spawn a nest when it spawns.
    /// </summary>
    [DataField, AutoNetworkedField] // POLONIUM: networked, the crown passes on
    public bool Queen;

    /// <summary>
    /// Current upgrade stage.
    /// </summary>
    [DataField]
    public int UpgradeStage;

    /// <summary>
    /// Used in determining what action should be granted on leveling up the nest.
    /// </summary>
    public int TargetUpgradeStage;

    /// <summary>
    /// Used to store related replicators on a queen after the nest is destroyed, so they can be transferred to the new nest.
    /// </summary>
    public HashSet<Entity<ReplicatorComponent>> RelatedReplicators = [];

    /// <summary>
    /// Used to store the EntityUid of the source nest of this replicator.
    /// </summary>
    public EntityUid? MyNest = null;

    [DataField]
    public EntProtoId Level1Id = "MobReplicator"; // POLONIUM

    [DataField]
    public EntProtoId Level2Id = "MobReplicatorTier2";

    [DataField]
    public EntProtoId Level3Id = "MobReplicatorTier3";

    [DataField]
    public EntProtoId Level2Action = "ActionReplicatorUpgrade2";

    [DataField]
    public EntProtoId Level3Action = "ActionReplicatorUpgrade3";

    /// <summary>
    /// The action to spawn a new nest.
    /// </summary>
    [DataField]
    public EntProtoId SpawnNewNestAction = "ActionReplicatorSpawnNest";

    [DataField]
    public EntProtoId? StunRayProto = "ReplicatorStunRay";

    [DataField]
    public string StunRayHandId = "stun_ray";

    [DataField]
    public EntProtoId TeleportPreyAction = "ActionReplicatorTeleportPrey";

    [DataField]
    public TimeSpan TeleportPreyDelay = TimeSpan.FromSeconds(1);

    // POLONIUM
    [DataField]
    public TimeSpan TeleportPreyParalyzeDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan TeleportPreyImmunityDuration = TimeSpan.FromSeconds(25);

    [DataField]
    public float TeleportPreyNestClearance = 18f;

    [DataField]
    public float TeleportPreyRangeStep = 2f;

    [DataField]
    public float TeleportPreyMaxRange = 28f;

    /// <summary>
    /// The longest side of a grid, in tiles, from which the teleport distances apply in full.
    /// On smaller grids the nest clearance and the rings shrink with it.
    /// </summary>
    [DataField]
    public float TeleportPreyFullGridSize = 80f;

    /// <summary>
    /// How far the distances shrink at most on a small grid, as a fraction of the full ones.
    /// </summary>
    [DataField]
    public float TeleportPreyMinScale = 0.4f;

    [DataField]
    public TimeSpan AttackFailPopupCooldown = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public TimeSpan NextAttackFailPopup;
    // POLONIUM end

    [DataField]
    public float TeleportPreyTargetRange = 12f;

    [DataField]
    public float TeleportPreyRadiusMin = SharedRandomGridTeleportSystem.DefaultMinDistance;

    [DataField]
    public float TeleportPreyRadiusMax = SharedRandomGridTeleportSystem.DefaultMaxDistance;

    [DataField]
    public SoundSpecifier TeleportPreySound = new SoundPathSpecifier("/Audio/_Polonium/Effects/Replicators/teleport.ogg");

    // prevent adding additional nest action if someone ghosts out and re-attaches
    public bool HasSpawnedNest;

    // POLONIUM
    [ViewVariables]
    public EntityUid? NestAction;

    /// <summary>
    /// How long a queen whose player has disconnected keeps the crown.
    /// </summary>
    [DataField]
    public TimeSpan QueenAbsenceGrace = TimeSpan.FromMinutes(3);
}

[Serializable, NetSerializable]
public enum ReplicatorVisuals : byte
{
    Combat,
    Thrust // POLONIUM
}
