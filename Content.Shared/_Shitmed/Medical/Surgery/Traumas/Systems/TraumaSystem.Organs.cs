using System.Linq;
using Content.Shared._Shitmed.CCVar;
using Content.Shared._Shitmed.Medical.Surgery.Pain;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;

public partial class TraumaSystem
{
    private const string OrganDamagePainIdentifier = "OrganDamage";
    public static readonly EntProtoId OrgansDamagedSlowdown = "OrgansDamagedSlowdownEffect";

    private void InitOrgans()
    {
        SubscribeLocalEvent<BodyComponent, OrganIntegrityChangedEventOnWoundable>(OnOrganIntegrityOnWoundableChanged);
        SubscribeLocalEvent<OrganComponent, OrganIntegrityChangedEvent>(OnOrganIntegrityChanged);
        SubscribeLocalEvent<BodyComponent, OrganDamageSeverityChangedOnWoundable>(OnOrganSeverityChanged);
        SubscribeLocalEvent<OrganIntegrityComponent, ComponentInit>(OnOrganIntegrityInit);
    }

    #region Event handling

    private void OnOrganIntegrityInit(Entity<OrganIntegrityComponent> ent, ref ComponentInit args)
    {
        if (ent.Comp.OrganIntegrity <= FixedPoint2.Zero)
            ent.Comp.OrganIntegrity = ent.Comp.IntegrityCap;
    }

    private void OnOrganIntegrityOnWoundableChanged(Entity<BodyComponent> body, ref OrganIntegrityChangedEventOnWoundable args)
    {
        if (!_consciousness.TryGetNerveSystem(body.Owner, out var nerveSys) || body.Comp.Organs is null)
            return;

        var totalIntegrity = FixedPoint2.Zero;
        var totalIntegrityCap = FixedPoint2.Zero;

        foreach (var organEnt in body.Comp.Organs.ContainedEntities)
        {
            if (!TryComp<OrganIntegrityComponent>(organEnt, out var integrity))
                continue;

            totalIntegrity += integrity.OrganIntegrity;
            totalIntegrityCap += integrity.IntegrityCap;
        }

        // Getting your organ turned into a blood mush inside you applies a LOT of internal pain, that can get you dead.
        if (!_pain.TryChangePainModifier(
                nerveSys.Value,
                body.Owner,
                OrganDamagePainIdentifier,
                (totalIntegrityCap - totalIntegrity) / 2,
                nerveSys.Value.Comp))
        {
            _pain.TryAddPainModifier(
                nerveSys.Value,
                body.Owner,
                OrganDamagePainIdentifier,
                (totalIntegrityCap - totalIntegrity) / 2,
                PainDamageTypes.TraumaticPain,
                nerveSys.Value.Comp);
        }
    }

    private void OnOrganIntegrityChanged(Entity<OrganComponent> organ, ref OrganIntegrityChangedEvent args)
    {
        if (organ.Comp.Body is not { } body || !TryComp<OrganIntegrityComponent>(organ.Owner, out var integrity))
            return;

        if (args.NewIntegrity < integrity.IntegrityCap || !TryGetBodyTraumas(body, out var traumas, TraumaType.OrganDamage))
            return;

        foreach (var trauma in traumas.Where(trauma => trauma.Comp.TraumaTarget == organ.Owner))
        {
            RemoveTrauma(trauma);
        }
    }

    private void OnOrganSeverityChanged(Entity<BodyComponent> body, ref OrganDamageSeverityChangedOnWoundable args)
    {
        if (args.Organ.Comp.Body is not { } bodyUid || args.NewSeverity < args.OldSeverity)
            return;

        var organCategory = args.Organ.Comp.Category?.Id ?? "organ";

        _popup.PopupClient(Loc.GetString($"popup-trauma-OrganDamage-{args.NewSeverity.ToString()}", ("part", organCategory)),
            bodyUid,
            bodyUid,
            PopupType.SmallCaution);

        if (args.NewSeverity != OrganSeverity.Destroyed)
            return;

        if (_consciousness.TryGetNerveSystem(bodyUid, out var nerveSys)
            && !_mobState.IsDead(bodyUid))
        {
            var sex = Sex.Unsexed;
            if (TryComp<HumanoidProfileComponent>(bodyUid, out var humanoid))
                sex = humanoid.Sex;

            _pain.PlayPainSoundWithCleanup(
                bodyUid,
                nerveSys.Value.Comp,
                nerveSys.Value.Comp.OrganDestructionReflexSounds[sex],
                AudioParams.Default.WithVolume(6f));

            _stun.TryUpdateParalyzeDuration(bodyUid, nerveSys.Value.Comp.OrganDamageStunTime);
            _movementMod.TryUpdateMovementSpeedModDuration(
                 bodyUid,
                 OrgansDamagedSlowdown,
                 nerveSys.Value.Comp.OrganDamageStunTime * _cfg.GetCVar(SurgeryCVars.OrganTraumaSlowdownTimeMultiplier),
                 _cfg.GetCVar(SurgeryCVars.OrganTraumaWalkSpeedSlowdown),
                 _cfg.GetCVar(SurgeryCVars.OrganTraumaRunSpeedSlowdown));
        }

        if (TryGetBodyTraumas(bodyUid, out var bodyTraumas, TraumaType.OrganDamage))
        {
            foreach (var trauma in bodyTraumas)
            {
                if (trauma.Comp.TraumaTarget != args.Organ.Owner)
                    continue;

                RemoveTrauma(trauma);
            }
        }

        if (TryComp<OrganIntegrityComponent>(args.Organ.Owner, out var organIntegrity))
            _audio.PlayPvs(organIntegrity.OrganDestroyedSound, bodyUid);

        if (body.Comp.Organs is not null)
            _container.Remove(args.Organ.Owner, body.Comp.Organs, force: true);

        if (_net.IsServer)
            QueueDel(args.Organ.Owner);
    }

    #endregion

    #region Public API
    public bool TryCreateOrganDamageModifier(EntityUid uid,
        FixedPoint2 severity,
        EntityUid effectOwner,
        string identifier,
        OrganIntegrityComponent? organ = null)
    {
        if (severity == 0
            || !Resolve(uid, ref organ))
            return false;

        if (!organ.IntegrityModifiers.TryAdd((identifier, effectOwner), severity))
            return false;

        UpdateOrganIntegrity(uid, organ);

        return true;
    }

    public bool TrySetOrganDamageModifier(EntityUid uid,
        FixedPoint2 severity,
        EntityUid effectOwner,
        string identifier,
        OrganIntegrityComponent? organ = null)
    {
        if (severity == 0
            || !Resolve(uid, ref organ))
            return false;

        organ.IntegrityModifiers[(identifier, effectOwner)] = severity;
        UpdateOrganIntegrity(uid, organ);

        return true;
    }

    public bool TryChangeOrganDamageModifier(EntityUid uid,
        FixedPoint2 change,
        EntityUid effectOwner,
        string identifier,
        OrganIntegrityComponent? organ = null)
    {
        if (change == 0
            || !Resolve(uid, ref organ))
            return false;

        if (!organ.IntegrityModifiers.TryGetValue((identifier, effectOwner), out var value))
            return false;

        organ.IntegrityModifiers[(identifier, effectOwner)] = value + change;
        UpdateOrganIntegrity(uid, organ);

        return true;
    }

    public bool TryRemoveOrganDamageModifier(EntityUid uid,
        EntityUid effectOwner,
        string identifier,
        OrganIntegrityComponent? organ = null)
    {
        if (!Resolve(uid, ref organ))
            return false;

        if (!organ.IntegrityModifiers.Remove((identifier, effectOwner)))
            return false;

        if (TryComp<TraumaComponent>(effectOwner, out var traumaComp))
            RemoveTrauma((effectOwner, traumaComp));

        UpdateOrganIntegrity(uid, organ);
        return true;
    }

    #endregion

    #region Private API

    private void UpdateOrganIntegrity(EntityUid uid, OrganIntegrityComponent organ)
    {
        var oldIntegrity = organ.OrganIntegrity;

        if (organ.IntegrityModifiers.Count > 0)
        {
            var totalDamage = organ.IntegrityModifiers.Aggregate(FixedPoint2.Zero, (current, modifier) => current + modifier.Value);

            var floor = FixedPoint2.Zero;
            if (TryComp<OrganComponent>(uid, out var organComp)
                && organComp.Category == "Brain"
                && organ.IntegrityThresholds.TryGetValue(OrganSeverity.Damaged, out var damagedThreshold))
                floor = damagedThreshold;

            organ.OrganIntegrity = FixedPoint2.Clamp(organ.IntegrityCap - totalDamage, floor, organ.IntegrityCap);
        }

        if (oldIntegrity != organ.OrganIntegrity)
        {
            var ev = new OrganIntegrityChangedEvent(oldIntegrity, organ.OrganIntegrity);
            RaiseLocalEvent(uid, ref ev);

            if (TryComp<OrganComponent>(uid, out var organComp) && organComp.Body is { } body)
            {
                var ev1 = new OrganIntegrityChangedEventOnWoundable((uid, organComp), oldIntegrity, organ.OrganIntegrity);
                RaiseLocalEvent(body, ref ev1);
            }
        }

        var nearestSeverity = organ.OrganSeverity;
        foreach (var (severity, value) in organ.IntegrityThresholds.OrderByDescending(kv => kv.Value))
        {
            if (organ.OrganIntegrity < value)
                continue;

            nearestSeverity = severity;
            break;
        }

        if (nearestSeverity != organ.OrganSeverity)
        {
            var ev = new OrganDamageSeverityChanged(organ.OrganSeverity, nearestSeverity);
            RaiseLocalEvent(uid, ref ev);

            if (TryComp<OrganComponent>(uid, out var organComp2) && organComp2.Body is { } body2)
            {
                var ev1 = new OrganDamageSeverityChangedOnWoundable((uid, organComp2), organ.OrganSeverity, nearestSeverity);
                RaiseLocalEvent(body2, ref ev1);
            }
        }

        organ.OrganSeverity = nearestSeverity;
        Dirty(uid, organ);
    }

    #endregion
}
