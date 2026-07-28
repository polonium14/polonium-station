using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Chat.Prototypes;
using Content.Shared._RMC14.Xenonids.Pheromones;
using System.Numerics;

namespace Content.Shared._RMC14.Xenonids;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoSystem), typeof(XenoPheromonesSystem))]
public sealed partial class XenoComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<EntProtoId> ActionIds = new();

    [DataField, AutoNetworkedField]
    public Dictionary<EntProtoId, EntityUid> Actions = new();

    [DataField, AutoNetworkedField]
    public int Tier;

    [DataField, AutoNetworkedField]
    public bool BypassTierCount;

    [DataField, AutoNetworkedField]
    public TimeSpan UnlockAt = TimeSpan.FromSeconds(60);

    [DataField, AutoNetworkedField]
    public ProtoId<EmoteSoundsPrototype>? EmoteSounds = "Xeno";

    // not networked - filled from EmoteSounds on init
    [ViewVariables]
    public EmoteSoundsPrototype? Sounds;

    [DataField, AutoNetworkedField]
    public bool MuteOnSpawn;

    [DataField, AutoNetworkedField]
    public Vector2 HudOffset;
}
