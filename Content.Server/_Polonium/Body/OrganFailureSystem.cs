using Content.Server.Chat.Systems;
using Content.Shared._Polonium.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared.Body;
using Content.Shared.CCVar;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Polonium.Body;

/// <summary>
/// Sick organs of a humanoid can fall into failure. The failure grows through stages, wears the organ down and
/// shows symptoms, and passes once the organ is healthy again or replaced. What each stage does to the body
/// as a whole (oxygen, poison) is up to <see cref="CirculationSystem"/>.
/// </summary>
public sealed partial class OrganFailureSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private CirculationSystem _circulation = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private OrganHealingSystem _healing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private const float UpdateInterval = 1f;

    private readonly HashSet<EntityUid> _dirtyBodies = new();
    private float _accumulator;
    private float _contractChance;
    private float _stageChance;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganFailureComponent, OrganDamageSeverityChanged>(OnSeverityChanged);

        Subs.CVar(_cfg, CCVars.OrgansFailureContractChance, value => _contractChance = value, true);
        Subs.CVar(_cfg, CCVars.OrgansFailureStageChance, value => _stageChance = value, true);
    }

    private void OnSeverityChanged(Entity<OrganFailureComponent> ent, ref OrganDamageSeverityChanged args)
    {
        // Sick kidneys slow the body down as soon as they are sick, not only once they fail.
        // Deferred, because the organ only stores its new severity after raising this event.
        if (CompOrNull<OrganComponent>(ent)?.Body is { } body)
            _dirtyBodies.Add(body);
    }

    /// <summary>
    /// Number of stages this organ's failure has.
    /// </summary>
    public int GetStageCount(Entity<OrganFailureComponent> organ)
    {
        return _proto.Index(organ.Comp.Failure).Stages.Count;
    }

    /// <summary>
    /// Sets the failure stage of an organ, 0 to cure it.
    /// </summary>
    public void SetStage(Entity<OrganFailureComponent> organ, int stage)
    {
        stage = Math.Clamp(stage, 0, GetStageCount(organ));
        if (organ.Comp.Stage == stage)
            return;

        organ.Comp.Stage = stage;
        if (CompOrNull<OrganComponent>(organ)?.Body is { } body)
            _circulation.Refresh(body);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var body in _dirtyBodies)
        {
            _circulation.Refresh(body);
        }

        _dirtyBodies.Clear();

        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;

        _accumulator -= UpdateInterval;

        // Copied first: wearing an organ down can kill it, which swaps it for a dead one mid-loop.
        var organs = new List<(Entity<OrganFailureComponent, OrganIntegrityComponent> Organ, EntityUid Body)>();
        var query = EntityQueryEnumerator<OrganFailureComponent, OrganIntegrityComponent, OrganComponent>();
        while (query.MoveNext(out var uid, out var failure, out var integrity, out var organ))
        {
            if (organ.Body is not { } body
                || !HasComp<HumanoidProfileComponent>(body)
                || _mobState.IsDead(body))
                continue;

            // Healthy and not failing: nothing to do, which is almost every organ.
            if (failure.Stage == 0 && integrity.OrganSeverity == OrganSeverity.Normal)
                continue;

            organs.Add(((uid, failure, integrity), body));
        }

        foreach (var (organ, body) in organs)
        {
            if (!TerminatingOrDeleted(organ))
                Tick(organ, body);
        }
    }

    private void Tick(Entity<OrganFailureComponent, OrganIntegrityComponent> organ, EntityUid body)
    {
        var (_, failure, integrity) = organ;
        var healthy = integrity.OrganSeverity == OrganSeverity.Normal;

        if (failure.Stage == 0)
        {
            var lost = 100f * (1f - (integrity.OrganIntegrity / integrity.IntegrityCap).Float());
            if (_random.Prob(Math.Clamp(lost * _contractChance, 0f, 1f)))
                SetStage((organ, failure), 1);

            return;
        }

        if (healthy)
        {
            SetStage((organ, failure), 0);
            return;
        }

        if (failure.Stage < GetStageCount((organ, failure)) && _random.Prob(_stageChance))
            SetStage((organ, failure), failure.Stage + 1);

        var stage = _proto.Index(failure.Failure).Stages[failure.Stage - 1];

        if (stage.Damage is { } damage)
        {
            var factor = _circulation.GetDialysisFactor(body, CompOrNull<OrganComponent>(organ)?.Category);
            if (factor > 0f)
                _circulation.DealOrganDamage(body, damage * (UpdateInterval * factor));
        }

        foreach (var symptom in stage.Symptoms)
        {
            if (_random.Prob(symptom.Chance * UpdateInterval))
                ShowSymptom(body, symptom);
        }

        // Last, because it may kill the organ.
        _healing.DamageOrgan((organ, integrity), integrity.IntegrityCap * stage.SelfDamage * UpdateInterval);
    }

    private void ShowSymptom(EntityUid body, OrganFailureSymptom symptom)
    {
        if (symptom.Self is { } self)
            _popup.PopupEntity(Loc.GetString(self), body, body, PopupType.SmallCaution);

        if (symptom.Others is { } others)
        {
            _popup.PopupEntity(Loc.GetString(others, ("name", Identity.Entity(body, EntityManager))),
                body,
                Filter.PvsExcept(body),
                true);
        }

        if (symptom.Emote is { } emote)
            _chat.TryEmoteWithChat(body, emote.Id);

        if (symptom.Faint is { } faint)
            _statusEffects.TryUpdateStatusEffectDuration(body, CirculationSystem.Unconscious, faint);
    }
}
