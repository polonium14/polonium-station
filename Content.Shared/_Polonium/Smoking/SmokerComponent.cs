using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.Smoking;

/// <summary>
/// The smoker trait: the body is hooked on nicotine and the lungs start scarred. Without nicotine in the blood
/// the smoker goes through three stages of withdrawal; a smoke ends them at once.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class SmokerComponent : Component
{
    /// <summary>
    /// Each lung keeps a random share of its integrity between <see cref="MinLungIntegrity"/> and this, rolled on
    /// its own when the trait is applied. The rest is scarred: no medicine or surgery restores it, only a transplant.
    /// </summary>
    [DataField]
    public float MaxLungIntegrity = 0.9f;

    /// <inheritdoc cref="MaxLungIntegrity"/>
    [DataField]
    public float MinLungIntegrity = 0.6f;

    /// <summary>
    /// Time without nicotine before each withdrawal stage starts.
    /// </summary>
    [DataField]
    public TimeSpan[] StageStarts =
    {
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(20),
        TimeSpan.FromMinutes(30),
    };

    /// <summary>
    /// Chance per second to feel the craving, from stage one on.
    /// </summary>
    [DataField]
    public float CravingChance = 1f / 60f;

    /// <summary>
    /// Chance per second to cough in stages two and three (tg: 2% and 5%).
    /// </summary>
    [DataField]
    public float[] CoughChances = { 0f, 0.02f, 0.05f };

    /// <summary>
    /// Jitter strength in stages two and three; zero means no jitter.
    /// </summary>
    [DataField]
    public float[] JitterAmplitudes = { 0f, 2f, 5f };

    /// <summary>
    /// When nicotine was last in the blood. Starts at spawn, so a new smoker has the full grace time.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan LastNicotine;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextCheck;

    /// <summary>
    /// Withdrawal stage the smoker is in, 0 when not in withdrawal.
    /// </summary>
    [DataField]
    public int Stage;
}
