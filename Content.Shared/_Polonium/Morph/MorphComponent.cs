using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.Morph;

/// <summary>
/// Shapeshifter ported from NSV13 (tgstation morph).
/// Disguising goes through a chameleon projector hidden inside the morph,
/// eating goes through the devourer component, this handles the rest.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedMorphSystem))]
public sealed partial class MorphComponent : Component
{
    /// <summary>
    /// Hidden chameleon projector kept inside the morph, which does the actual disguising.
    /// It can't be the morph itself, since revealing removes every action provided by the projector.
    /// </summary>
    [DataField]
    public EntProtoId ProjectorProto = "MorphChameleonProjector";

    [DataField, AutoNetworkedField]
    public EntityUid? Projector;

    public const string ProjectorContainerId = "morph_projector";

    [DataField]
    public EntProtoId DisguiseAction = "ActionMorphDisguise";

    [DataField, AutoNetworkedField]
    public EntityUid? DisguiseActionEntity;

    [DataField]
    public EntProtoId RevealAction = "ActionMorphReveal";

    [DataField, AutoNetworkedField]
    public EntityUid? RevealActionEntity;

    [DataField]
    public EntProtoId StomachAction = "ActionMorphStomach";

    [DataField, AutoNetworkedField]
    public EntityUid? StomachActionEntity;

    [DataField]
    public EntProtoId SpitAction = "ActionMorphSpit";

    [DataField, AutoNetworkedField]
    public EntityUid? SpitActionEntity;

    /// <summary>
    /// Time between changing forms, both disguising and revealing.
    /// </summary>
    [DataField]
    public TimeSpan DisguiseCooldown = TimeSpan.FromSeconds(5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextDisguise;

    /// <summary>
    /// Melee damage multiplier while disguised.
    /// </summary>
    [DataField]
    public float DisguisedDamageMultiplier = 0.25f;

    /// <summary>
    /// Movement speed multiplier while disguised.
    /// </summary>
    [DataField]
    public float DisguisedSpeedModifier = 1.25f;

    /// <summary>
    /// Knockdown applied to whoever touches the disguised morph.
    /// </summary>
    [DataField]
    public TimeSpan AmbushKnockdown = TimeSpan.FromSeconds(4);

    [DataField]
    public ProtoId<ReagentPrototype> AmbushReagent = "MorphVenom";

    [DataField]
    public FixedPoint2 AmbushReagentAmount = 7;

    [DataField]
    public TimeSpan DigestTime = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Part of the digested mob's dead threshold that heals the morph.
    /// </summary>
    [DataField]
    public float DigestHealFraction = 0.25f;

    /// <summary>
    /// Items that can not be digested.
    /// </summary>
    [DataField]
    public EntityWhitelist? DigestBlacklist;

    /// <summary>
    /// Left behind by a digested mob.
    /// </summary>
    [DataField]
    public EntProtoId? DigestRemains = "Ash";

    /// <summary>
    /// Stomach content thrown by the next spit action.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? PreparedThrow;

    [DataField]
    public float ThrowSpeed = 10f;

    [DataField]
    public SoundSpecifier SpitSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");

    [DataField]
    public SoundSpecifier DigestItemSound = new SoundPathSpecifier("/Audio/Items/welder.ogg");

    [DataField]
    public SoundSpecifier DigestMobSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");

    /// <summary>
    /// Mirrors whether the morph has a <see cref="Content.Shared.Polymorph.Components.ChameleonDisguisedComponent"/>.
    /// </summary>
    [ViewVariables]
    public bool Disguised;
}
