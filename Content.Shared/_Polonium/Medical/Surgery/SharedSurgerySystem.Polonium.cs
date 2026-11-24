using Content.Shared._Polonium.Medical.Surgery;
using Content.Shared.Bed.Sleep;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.IdentityManagement;
using Content.Shared.Jittering;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

// The Shitmed system's namespace, so this stays one partial class with it.
namespace Content.Shared._Shitmed.Medical.Surgery;

/// <summary>
/// Polonium step types for <see cref="IsStepComplete"/>, which only knows the step types listed in it.
/// Also: a step done too slowly, with an improvised tool, in poor conditions or by a clumsy surgeon can fail,
/// and a patient who feels nothing is operated on faster.
/// </summary>
public abstract partial class SharedSurgerySystem
{
    [Dependency] private SurgicalCavitySystem _cavity = default!;
    [Dependency] private OrganRepairSurgerySystem _organRepair = default!;
    [Dependency] private IRobustRandom _random = default!;

    private static readonly ProtoId<DamageTypePrototype> BotchDamageType = "Slash";

    private bool PoloniumStepComplete(EntityUid step, EntityUid part, EntityUid body, EntityUid surgery)
    {
        return _cavity.IsStepComplete(step, part) && _organRepair.IsStepComplete(step, body, surgery);
    }

    /// <summary>
    /// Whether surgery does not hurt this patient: they are anesthetized or on a painkiller.
    /// </summary>
    public bool FeelsNoPain(EntityUid patient)
    {
        return Status.HasEffectComp<ForcedSleepingStatusEffectComponent>(patient)
            || Status.HasEffectComp<AnalgesiaStatusEffectComponent>(patient);
    }

    private float PatientSpeedModifier(EntityUid patient)
    {
        return FeelsNoPain(patient) ? _config.GetCVar(CCVars.SurgeryNumbPatientSpeed) : 1f;
    }

    /// <summary>
    /// Chance, from 0 to 1, that this step fails with a tool of this speed: nothing up to the threshold slowdown,
    /// then growing with the slowdown and with the step's base time.
    /// </summary>
    public float BotchChance(EntityUid step, EntityUid user, EntityUid body, float toolSpeed)
    {
        if (!_stepQuery.TryComp(step, out var stepComp) || stepComp.Duration <= 0f)
            return 0f;

        var slowdown = GetSurgeryDuration(step, user, body, toolSpeed) / stepComp.Duration;
        var chance = (slowdown - _config.GetCVar(CCVars.SurgeryBotchThreshold))
            * stepComp.Duration
            * _config.GetCVar(CCVars.SurgeryBotchChancePerSecond);
        chance = Math.Clamp(chance, 0f, _config.GetCVar(CCVars.SurgeryBotchMaxChance));

        // A clumsy surgeon fails on top of that, even with the best tools.
        return 1f - (1f - chance) * (1f - ClumsyChance(user, out _));
    }

    /// <summary>
    /// How often this surgeon botches a step whatever the tool: the worst of their clumsy effects and shaking hands.
    /// </summary>
    private float ClumsyChance(EntityUid user, out SoundSpecifier? sound)
    {
        sound = null;
        var worst = HasComp<JitteringComponent>(user) ? _config.GetCVar(CCVars.SurgeryJitterBotchChance) : 0f;

        if (Status.TryEffectsWithComp<ClumsySurgeryStatusEffectComponent>(user, out var effects))
        {
            foreach (var effect in effects)
            {
                if (effect.Comp1.ClumsyChance <= worst)
                    continue;

                worst = effect.Comp1.ClumsyChance;
                sound = effect.Comp1.ClumsySound;
            }
        }

        return Math.Clamp(worst, 0f, 1f);
    }

    /// <summary>
    /// Rolls for a finished step; on a failure the part is cut and the step stays undone. Server only.
    /// </summary>
    private bool TryBotchStep(EntityUid user, EntityUid body, EntityUid part, EntityUid step, EntityUid tool, EntityUid surgery)
    {
        CanPerformStep(user, body, part, step, tool, false, out _, out _, out var data, surgery);
        if (!_random.Prob(BotchChance(step, user, body, data?.Speed ?? 1f)))
            return false;

        _popup.PopupEntity(
            Loc.GetString("surgery-popup-step-botched", ("user", Identity.Entity(user, EntityManager))),
            body,
            PopupType.MediumCaution);

        ClumsyChance(user, out var sound);
        _audio.PlayPvs(sound, body);

        var damage = new DamageSpecifier(_prototypes.Index(BotchDamageType), _config.GetCVar(CCVars.SurgeryBotchDamage));
        var ev = new SurgeryStepDamageEvent(user, body, part, surgery, damage, 1f);
        RaiseLocalEvent(body, ref ev);
        return true;
    }
}
