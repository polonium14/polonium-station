using System.Linq;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;

public partial class TraumaSystem
{
    private const float LegCollapseSpeedThreshold = 1f / 3.4f;

    private const float CrawlSpeed = 0.25f;

    private void InitBones()
    {
        SubscribeLocalEvent<BoneComponent, BoneSeverityChangedEvent>(OnBoneSeverityChanged);
        SubscribeLocalEvent<BoneComponent, BoneIntegrityChangedEvent>(OnBoneIntegrityChanged);
        SubscribeLocalEvent<BodyComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshLegSpeed);
        SubscribeLocalEvent<BodyComponent, StandAttemptEvent>(OnStandAttempt);
    }

    #region Event Handling

    private void OnBoneSeverityChanged(Entity<BoneComponent> bone, ref BoneSeverityChangedEvent args)
    {
        if (bone.Comp.BoneWoundable == null
            || args.NewSeverity < args.OldSeverity)
            return;

        if (!TryComp<OrganComponent>(bone.Comp.BoneWoundable.Value, out var organ) || organ.Body is not { } body)
            return;

        var part = organ.Category?.Id ?? "part";

        _popup.PopupClient(Loc.GetString($"popup-trauma-BoneDamage-{args.NewSeverity.ToString()}", ("part", part)),
            body,
            PopupType.SmallCaution);

        var volumeFloat = args.NewSeverity switch
        {
            BoneSeverity.Damaged => -8f,
            BoneSeverity.Cracked => 1f,
            BoneSeverity.Broken => 6f,
            _ => 0f,
        };

        _audio.PlayPvs(bone.Comp.BoneBreakSound, body, AudioParams.Default.WithVolume(volumeFloat));
    }

    private void OnBoneIntegrityChanged(Entity<BoneComponent> bone, ref BoneIntegrityChangedEvent args)
    {
        if (bone.Comp.BoneWoundable == null)
            return;

        if (!TryComp<OrganComponent>(bone.Comp.BoneWoundable.Value, out var organ) || organ.Body is not { } body)
            return;

        if (args.NewIntegrity == bone.Comp.IntegrityCap)
        {
            if (organ.Category?.Id is "HandLeft" or "HandRight")
                _virtual.DeleteInHandsMatching(body, bone);

            if (TryGetWoundableTrauma(bone.Comp.BoneWoundable.Value, out var traumas, TraumaType.BoneDamage))
                foreach (var trauma in traumas.Where(trauma => trauma.Comp.TraumaTarget == bone))
                    RemoveTrauma(trauma);
        }

        if (organ.Category?.Id is "LegLeft" or "LegRight" or "FootLeft" or "FootRight" or "ArmLeft" or "ArmRight")
            _movementSpeed.RefreshMovementSpeedModifiers(body);
    }

    private void OnRefreshLegSpeed(Entity<BodyComponent> body, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (body.Comp.Organs is not { } organs || organs.ContainedEntities.Count == 0)
            return;

        if (!LimbTargetMap.TryGetOrganByCategory(EntityManager, body, "Torso", out _))
            return;

        var legless = IsLegless(body);

        var multiplier = legless || _standing.IsDown(body.Owner)
            ? GetCrawlMultiplier(body)
            : (GetLegMultiplier(body, "LegLeft", "FootLeft") + GetLegMultiplier(body, "LegRight", "FootRight")) / 2f;

        args.ModifySpeed(multiplier);

        if (multiplier < LegCollapseSpeedThreshold)
            _standing.Down(body);

        if (legless)
            _alert.ShowAlert(body.Owner, _legsCollapsedAlertId);
        else
            _alert.ClearAlert(body.Owner, _legsCollapsedAlertId);
    }

    private void OnStandAttempt(Entity<BodyComponent> body, ref StandAttemptEvent args)
    {
        if (IsLegless(body))
            args.Cancel();
    }

    private bool IsLegless(Entity<BodyComponent> body)
    {
        if (body.Comp.Organs is null || body.Comp.Organs.ContainedEntities.Count == 0)
            return false;

        return GetLegMultiplier(body, "LegLeft", "FootLeft") <= 0f
            && GetLegMultiplier(body, "LegRight", "FootRight") <= 0f;
    }

    private float GetCrawlMultiplier(Entity<BodyComponent> body)
    {
        var limbSum = GetLimbMultiplier(body, "LegLeft")
            + GetLimbMultiplier(body, "LegRight")
            + GetLimbMultiplier(body, "ArmLeft")
            + GetLimbMultiplier(body, "ArmRight");

        return CrawlSpeed * (limbSum / 4f);
    }

    private float GetLegMultiplier(
        Entity<BodyComponent> body,
        ProtoId<OrganCategoryPrototype> legCategory,
        ProtoId<OrganCategoryPrototype> footCategory)
    {
        if (!LimbTargetMap.TryGetOrganByCategory(EntityManager, body, legCategory, out var legOrgan)
            || !TryComp<WoundableComponent>(legOrgan, out var legWoundable)
            || !TryGetBoneSeverity(legWoundable, out var legSeverity))
            return 0f;

        var legMultiplier = legSeverity switch
        {
            BoneSeverity.Cracked => 0.5f,
            BoneSeverity.Damaged => 1f / 1.6f,
            BoneSeverity.Broken => 0f,
            _ => 1f,
        };

        if (legMultiplier == 0f)
            return 0f;

        var footMultiplier = 1f;
        if (!LimbTargetMap.TryGetOrganByCategory(EntityManager, body, footCategory, out var footOrgan))
        {
            footMultiplier = 0.44f;
        }
        else if (TryComp<WoundableComponent>(footOrgan, out var footWoundable)
            && TryGetBoneSeverity(footWoundable, out var footSeverity))
        {
            footMultiplier = footSeverity switch
            {
                BoneSeverity.Damaged => 0.77f,
                BoneSeverity.Cracked => 0.66f,
                BoneSeverity.Broken => 0.55f,
                _ => 1f,
            };
        }

        return legMultiplier * footMultiplier;
    }

    private float GetLimbMultiplier(Entity<BodyComponent> body, ProtoId<OrganCategoryPrototype> category)
    {
        if (!LimbTargetMap.TryGetOrganByCategory(EntityManager, body, category, out var organ)
            || !TryComp<WoundableComponent>(organ, out var woundable)
            || !TryGetBoneSeverity(woundable, out var severity))
            return 0f;

        return severity switch
        {
            BoneSeverity.Cracked => 0.5f,
            BoneSeverity.Damaged => 1f / 1.6f,
            BoneSeverity.Broken => 0f,
            _ => 1f,
        };
    }

    private bool TryGetBoneSeverity(WoundableComponent woundable, out BoneSeverity severity)
    {
        severity = default;
        if (woundable.Bone is null)
            return false;

        foreach (var bone in woundable.Bone.ContainedEntities)
        {
            if (!TryComp<BoneComponent>(bone, out var boneComp))
                continue;

            severity = boneComp.BoneSeverity;
            return true;
        }

        return false;
    }

    #endregion

    #region Public API

    public void RefreshLimbMovementSpeed(EntityUid body)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(body);
    }

    public bool ApplyDamageToBone(EntityUid bone, FixedPoint2 severity, BoneComponent? boneComp = null)
    {
        if (severity == 0
            || !Resolve(bone, ref boneComp))
            return false;

        var newIntegrity = FixedPoint2.Clamp(boneComp.BoneIntegrity - severity, 0, boneComp.IntegrityCap);
        if (boneComp.BoneIntegrity == newIntegrity)
            return false;

        var ev = new BoneIntegrityChangedEvent((bone, boneComp), boneComp.BoneIntegrity, newIntegrity);
        RaiseLocalEvent(bone, ref ev);

        boneComp.BoneIntegrity = newIntegrity;
        CheckBoneSeverity(bone, boneComp);

        Dirty(bone, boneComp);
        return true;
    }

    public bool ApplyBoneTrauma(
        EntityUid boneEnt,
        Entity<WoundableComponent> woundable,
        Entity<TraumaInflicterComponent> inflicter,
        FixedPoint2 inflicterSeverity,
        BoneComponent? boneComp = null)
    {
        if (!Resolve(boneEnt, ref boneComp))
            return false;

        if (_net.IsServer)
            AddTrauma(boneEnt, woundable, inflicter, TraumaType.BoneDamage, inflicterSeverity);

        ApplyDamageToBone(boneEnt, inflicterSeverity, boneComp);

        return true;
    }

    public bool SetBoneIntegrity(EntityUid bone, FixedPoint2 integrity, BoneComponent? boneComp = null)
    {
        if (!Resolve(bone, ref boneComp))
            return false;

        var newIntegrity = FixedPoint2.Clamp(integrity, 0, boneComp.IntegrityCap);
        if (boneComp.BoneIntegrity == newIntegrity)
            return false;

        var ev = new BoneIntegrityChangedEvent((bone, boneComp), boneComp.BoneIntegrity, newIntegrity);
        RaiseLocalEvent(bone, ref ev);

        boneComp.BoneIntegrity = newIntegrity;
        CheckBoneSeverity(bone, boneComp);

        Dirty(bone, boneComp);
        return true;
    }

    public void UpdateBodyBoneAlert(EntityUid body, BodyComponent? bodyComp = null)
    {
        if (!Resolve(body, ref bodyComp) || bodyComp.Organs is null)
            return;

        var hasBrokenBones = false;

        foreach (var organ in bodyComp.Organs.ContainedEntities)
        {
            if (!TryComp<WoundableComponent>(organ, out var woundable) || woundable.Bone is null)
                continue;

            foreach (var boneEntity in woundable.Bone.ContainedEntities)
            {
                if (!TryComp(boneEntity, out BoneComponent? boneComp) || boneComp.BoneSeverity != BoneSeverity.Broken)
                    continue;

                hasBrokenBones = true;
                break;
            }

            if (hasBrokenBones)
                break;
        }

        if (hasBrokenBones)
            _alert.ShowAlert(body, _brokenBonesAlertId);
        else
            _alert.ClearAlert(body, _brokenBonesAlertId);
    }

    #endregion

    #region Private API

    private void CheckBoneSeverity(EntityUid bone, BoneComponent boneComp)
    {
        var nearestSeverity = boneComp.BoneSeverity;

        foreach (var (severity, value) in _boneThresholds.OrderByDescending(kv => kv.Value))
        {
            if (boneComp.BoneIntegrity < value)
                continue;

            nearestSeverity = severity;
            break;
        }

        if (nearestSeverity != boneComp.BoneSeverity)
        {
            var ev = new BoneSeverityChangedEvent((bone, boneComp), boneComp.BoneSeverity, nearestSeverity);
            RaiseLocalEvent(bone, ref ev, true);
        }

        boneComp.BoneSeverity = nearestSeverity;
        Dirty(bone, boneComp);

        if (boneComp.BoneWoundable != null
            && TryComp<OrganComponent>(boneComp.BoneWoundable.Value, out var organ)
            && organ.Body is { } body)
        {
            UpdateBodyBoneAlert(body);

            if (organ.Category?.Id is "LegLeft" or "LegRight" or "FootLeft" or "FootRight" or "ArmLeft" or "ArmRight")
                _movementSpeed.RefreshMovementSpeedModifiers(body);
        }
    }

    private bool TryFumble(string message, SoundPathSpecifier sound, EntityUid body, float odds)
    {
        var rand = new System.Random((int) _timing.CurTick.Value);
        if (rand.NextFloat() < odds)
        {
            _popup.PopupClient(Loc.GetString(message), body, PopupType.Medium);
            var ev = new DropHandItemsEvent();
            RaiseLocalEvent(body, ref ev, false);
            _audio.PlayPredicted(sound, body, body);
            return true;
        }
        return false;
    }

    #endregion
}
