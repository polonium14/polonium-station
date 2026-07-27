using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Emoting;

// use as a template
//[Serializable, NetSerializable, DataDefinition] public sealed partial class AnimationNameEmoteEvent : EntityEventArgs { }

[Serializable, NetSerializable, DataDefinition] public sealed partial class AnimationFlipEmoteEvent : EntityEventArgs { }
[Serializable, NetSerializable, DataDefinition] public sealed partial class AnimationSpinEmoteEvent : EntityEventArgs { }
[Serializable, NetSerializable, DataDefinition] public sealed partial class AnimationJumpEmoteEvent : EntityEventArgs { }

[RegisterComponent] public sealed partial class AnimatedEmotesComponent : Component { }

/// <summary>
///     Tells clients in PVS to play an emote animation once.
///     Deliberately a one-shot event rather than component state: persisted state replays the
///     animation for everyone who later enters PVS or reconnects.
/// </summary>
[Serializable, NetSerializable]
public sealed class AnimatedEmoteEvent : EntityEventArgs
{
    public NetEntity Entity;
    public ProtoId<EmotePrototype> Emote;

    public AnimatedEmoteEvent(NetEntity entity, ProtoId<EmotePrototype> emote)
    {
        Entity = entity;
        Emote = emote;
    }
}
