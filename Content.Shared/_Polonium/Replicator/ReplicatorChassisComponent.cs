using Content.Shared.Whitelist;

namespace Content.Shared._Polonium.Replicator;

/// <summary>
/// Station machinery a replicator body cannot operate, and the organic leftovers it does not carry.
/// </summary>
[RegisterComponent]
public sealed partial class ReplicatorChassisComponent : Component
{
    /// <summary>
    /// The only things whose interfaces the replicator can open. Verbs stay available everywhere.
    /// </summary>
    [DataField(required: true)]
    public EntityWhitelist UiWhitelist = new();
}
