using Content.Server._Polonium.Body;
using Content.Server._Polonium.Medical.PatientIntake;
using Content.Shared._Polonium.Body;
using Content.Shared._Polonium.Medical.Cpr;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.CCVar;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Polonium.Medical.Cpr;

/// <summary>
/// Chest compressions. Anyone with free hands and an uncovered mouth can give them to a patient who is
/// out cold, in critical condition or dead. While someone keeps pushing on a heart that is still in the body, the blood
/// moves: the patient keeps their oxygen, loses some asphyxiation and does not rot, and a heart in the last stage of
/// failure can pick up again. Each push can break the ribs, and the rescuer slowly tires. It brings no one back to life.
/// </summary>
public sealed partial class CprSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private CirculationSystem _circulation = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IngestionSystem _ingestion = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private OrganFailureSystem _failure = default!;
    [Dependency] private PatientIntakeSystem _intake = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";
    private static readonly ProtoId<ReagentPrototype> Epinephrine = "Epinephrine";
    private static readonly ProtoId<OrganCategoryPrototype> Heart = "Heart";
    private static readonly ProtoId<OrganCategoryPrototype> Torso = "Torso";
    private static readonly SoundSpecifier PushSound = new SoundPathSpecifier("/Audio/_Polonium/Effects/cprpulse.ogg");

    /// <summary>
    /// How long one cycle keeps holding the body after it ends, so the next cycle can take over without a gap.
    /// </summary>
    private static readonly TimeSpan HoldMargin = TimeSpan.FromSeconds(1);

    private float _cycleTime;
    private float _asphyxiationHeal;
    private float _ribBreakChance;
    private float _ribBreakDamage;
    private float _staminaCost;
    private float _restartChance;
    private float _restartEpinephrineChance;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidProfileComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<HumanoidProfileComponent, CprDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<ChestCompressionsComponent, IsRottingEvent>(OnIsRotting);

        Subs.CVar(_cfg, CCVars.CprCycleTime, value => _cycleTime = value, true);
        Subs.CVar(_cfg, CCVars.CprAsphyxiationHeal, value => _asphyxiationHeal = value, true);
        Subs.CVar(_cfg, CCVars.CprRibBreakChance, value => _ribBreakChance = value, true);
        Subs.CVar(_cfg, CCVars.CprRibBreakDamage, value => _ribBreakDamage = value, true);
        Subs.CVar(_cfg, CCVars.CprStaminaCost, value => _staminaCost = value, true);
        Subs.CVar(_cfg, CCVars.CprHeartRestartChance, value => _restartChance = value, true);
        Subs.CVar(_cfg, CCVars.CprHeartRestartEpinephrineChance, value => _restartEpinephrineChance = value, true);
    }

    /// <summary>
    /// Whether someone is pushing blood through this body right now.
    /// </summary>
    public bool IsCompressed(EntityUid body)
    {
        return TryComp<ChestCompressionsComponent>(body, out var compressions) && compressions.Until > _timing.CurTime;
    }

    private void OnIsRotting(Entity<ChestCompressionsComponent> ent, ref IsRottingEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = ent.Comp.Until > _timing.CurTime;
    }

    /// <summary>
    /// Out cold, in critical condition, dead, or about to be for want of a heartbeat.
    /// </summary>
    private bool NeedsCpr(EntityUid patient)
    {
        return _mobState.IsIncapacitated(patient)
               || _circulation.IsStopped(patient)
               || _statusEffects.HasStatusEffect(patient, CirculationSystem.Unconscious);
    }

    private void OnGetVerbs(Entity<HumanoidProfileComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess
            || !args.CanInteract
            || args.Hands == null
            || args.User == ent.Owner
            || !HasComp<BodyComponent>(ent)
            || !NeedsCpr(ent))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cpr-verb"),
            Act = () => TryStart(user, ent),
            Priority = 5,
        });
    }

    /// <summary>
    /// Starts chest compressions, which go on cycle after cycle until the rescuer stops or is interrupted.
    /// </summary>
    public bool TryStart(EntityUid user, EntityUid patient)
    {
        if (!CanGive(user, patient))
            return false;

        var args = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(_cycleTime), new CprDoAfterEvent(), patient, patient)
        {
            BreakOnMove = true,
            NeedHand = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent | DuplicateConditions.SameTarget,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return false;

        _popup.PopupEntity(Loc.GetString("cpr-start-self", ("target", Identity.Entity(patient, EntityManager))), user, user);
        _popup.PopupEntity(Loc.GetString("cpr-start-others",
                ("user", Identity.Entity(user, EntityManager)),
                ("target", Identity.Entity(patient, EntityManager))),
            user,
            Filter.PvsExcept(user),
            true);
        return true;
    }

    /// <summary>
    /// Both mouths have to be free; the check tells the rescuer what to take off.
    /// </summary>
    private bool CanGive(EntityUid user, EntityUid patient)
    {
        return NeedsCpr(patient)
               && _ingestion.HasMouthAvailable(user, user)
               && _ingestion.HasMouthAvailable(user, patient);
    }

    private void OnDoAfter(Entity<HumanoidProfileComponent> ent, ref CprDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !CanGive(args.User, ent))
            return;

        args.Handled = true;

        _audio.PlayPvs(PushSound, ent);
        RaiseNetworkEvent(new CprLungeEvent(GetNetEntity(args.User)), Filter.Pvs(args.User));

        // Pushing on an empty chest moves no blood, but it still breaks ribs and tires the rescuer.
        if (TryGetHeart(ent, out var heart))
        {
            var compressions = EnsureComp<ChestCompressionsComponent>(ent);
            compressions.Until = _timing.CurTime + TimeSpan.FromSeconds(_cycleTime) + HoldMargin;

            _damageable.TryChangeDamage(ent.Owner,
                new DamageSpecifier(_proto.Index(Asphyxiation), -_asphyxiationHeal),
                ignoreResistances: true,
                interruptsDoAfters: false);

            TryRestartHeart(ent, heart);
        }

        if (_random.Prob(_ribBreakChance))
            TryBreakRibs(ent);

        _stamina.TakeStaminaDamage(args.User, _staminaCost, visual: false);
        // Checked after this cycle's effects: once the patient is out of crit with a beating heart, the rescuer stops.
        args.Repeat = _mobState.IsIncapacitated(ent) || _circulation.IsStopped(ent);
    }

    private bool TryGetHeart(EntityUid body, out EntityUid heart)
    {
        heart = default;
        return TryComp<BodyComponent>(body, out var bodyComp)
               && LimbTargetMap.TryGetOrganByCategory(EntityManager, bodyComp, Heart, out heart);
    }

    /// <summary>
    /// A heart in the last stage of failure can pick up again; epinephrine makes it more likely.
    /// </summary>
    private void TryRestartHeart(EntityUid body, EntityUid heart)
    {
        if (!TryComp<OrganFailureComponent>(heart, out var failure)
            || failure.Stage < _failure.GetStageCount((heart, failure)))
            return;

        var chance = HasEpinephrine(body) ? _restartEpinephrineChance : _restartChance;
        if (_random.Prob(chance))
            _failure.SetStage((heart, failure), failure.Stage - 1);
    }

    private bool HasEpinephrine(EntityUid body)
    {
        Entity<SolutionComponent>? solution = null;
        return TryComp<BloodstreamComponent>(body, out var bloodstream)
               && _solutions.ResolveSolution(body, bloodstream.BloodSolutionName, ref solution, out var blood)
               && blood.GetTotalPrototypeQuantity(Epinephrine) > 0;
    }

    /// <summary>
    /// Breaks the torso bone through a real trauma, so the mend-bones surgery treats it. Broken ribs do not break again.
    /// </summary>
    private void TryBreakRibs(EntityUid patient)
    {
        if (!TryComp<BodyComponent>(patient, out var body)
            || !LimbTargetMap.TryGetOrganByCategory(EntityManager, body, Torso, out var torso)
            || !TryComp<WoundableComponent>(torso, out var woundable)
            || !TryComp<BoneComponent>(woundable.Bone?.ContainedEntities.FirstOrNull(), out var bone)
            || bone.BoneSeverity == BoneSeverity.Broken)
            return;

        if (!_intake.DamageBone((torso, woundable), bone.BoneIntegrity, _ribBreakDamage))
            return;

        _popup.PopupEntity(Loc.GetString("cpr-ribs-break", ("target", Identity.Entity(patient, EntityManager))),
            patient,
            PopupType.MediumCaution);
    }
}
