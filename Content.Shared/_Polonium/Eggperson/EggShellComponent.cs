using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Eggperson;

/// <summary>
/// Eggperson shell, ported from Aquila Station. The more fried the egg is, the harder its shell:
/// brute resistance follows a parabola over burn damage, from fragile when raw to tough when well done.
/// Cracks audibly on heavy hits and shatters into a gib once it has taken too much brute.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class EggShellComponent : Component
{
    /// <summary>
    /// Brute multiplier of a raw egg (no burn damage).
    /// </summary>
    [DataField]
    public float RawBruteModifier = 1.5f;

    /// <summary>
    /// Quadratic and linear terms of the parabola, mod = A*x^2 + B*x + RawBruteModifier.
    /// With the defaults the shell is hardest (about 0.49) at <see cref="HardestBurn"/>.
    /// </summary>
    [DataField]
    public float A = 0.00018f;

    [DataField]
    public float B = -0.027f;

    /// <summary>
    /// Burn damage after which the shell stops getting any harder.
    /// </summary>
    [DataField]
    public FixedPoint2 HardestBurn = 75;

    /// <summary>
    /// Brute damage at which the egg shatters.
    /// </summary>
    [DataField]
    public FixedPoint2 ShatterBrute = 200;

    /// <summary>
    /// A single hit has to deal at least this much brute for the shell to crack audibly.
    /// </summary>
    [DataField]
    public FixedPoint2 CrackThreshold = 5;

    /// <summary>
    /// Damage types the shell protects against (or fails to).
    /// </summary>
    [DataField]
    public List<ProtoId<DamageTypePrototype>> BruteTypes = new() { "Blunt", "Slash", "Piercing" };

    /// <summary>
    /// Damage types that count as the egg being fried.
    /// </summary>
    [DataField]
    public List<ProtoId<DamageTypePrototype>> BurnTypes = new() { "Heat", "Shock", "Cold", "Caustic" };

    [DataField]
    public SoundSpecifier CrackSound = new SoundCollectionSpecifier("EggpersonSpeech", AudioParams.Default.WithVolume(-2f).WithVariation(0.1f));

    [DataField]
    public SoundSpecifier ShatterSound = new SoundPathSpecifier("/Audio/_Polonium/Voice/Eggperson/eggcrack4.ogg", AudioParams.Default.WithVolume(4f));

    [DataField]
    public bool Shattered;
}
