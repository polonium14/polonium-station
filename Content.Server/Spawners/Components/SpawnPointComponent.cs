using Content.Server.Ghost.Roles;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.Spawners.Components;

[RegisterComponent]
public sealed partial class SpawnPointComponent : Component, ISpawnPoint
{
    /// <summary>
    /// The job this spawn point is valid for.
    /// Null will allow all jobs to spawn here.
    /// </summary>
    [DataField("job_id")]
    public ProtoId<JobPrototype>? Job;

    /// <summary>
    /// The type of spawn point.
    /// </summary>
    [DataField("spawn_type"), ViewVariables(VVAccess.ReadWrite)]
    public SpawnPointType SpawnType { get; set; } = SpawnPointType.Unset;

    public override string ToString()
    {
        return $"{Job} {SpawnType}";
    }

    // currently only has any amount of functionality in relation to GhostJob type spawners.
    // If true, then when the spawned entity enters cryo-storage it will reactivate the ghost role via the spawner
    [DataField("respawn")]
    [Access(typeof(GhostRoleSystem), Other = AccessPermissions.Read)]
    public bool Respawn = false;
}

public enum SpawnPointType
{
    Unset = 0,
    LateJoin,
    Job,
    Observer,
    GhostJob, // Funky
}
