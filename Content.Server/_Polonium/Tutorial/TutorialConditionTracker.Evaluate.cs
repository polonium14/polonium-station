using System.Linq;
using Content.Server.Ame.EntitySystems;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Conditions;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Body.Systems;
using Content.Shared.CombatMode;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Wires;
using Robust.Shared.Containers;

namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// The one place a completion condition is answered. The switch is deliberately flat: a line
/// of a step's YAML maps onto a line here, and the work behind it lives in the other files.
/// </summary>
public sealed partial class TutorialConditionTracker
{
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private AmeControllerSystem _ame = default!;
    [Dependency] private SharedInternalsSystem _internals = default!;
    [Dependency] private MobStateSystem _mobs = default!;
    [Dependency] private PlantTraySystem _plantTray = default!;
    [Dependency] private TutorialEyeRecoverySystem _eyes = default!;
    [Dependency] private SharedMaterialStorageSystem _materials = default!;

    public bool Evaluate(EntityUid player, TutorialSessionComponent session, TutorialCondition condition)
    {
        return condition switch
        {
            AnyCondition any => any.Conditions.Any(c => Evaluate(player, session, c)),
            AllCondition all => all.Conditions.Count > 0 && all.Conditions.All(c => Evaluate(player, session, c)),
            ReachAnchorCondition reach => CheckReach(player, reach.AnchorId, reach.Range),
            AnyReachAnchorsCondition anyReach => anyReach.AnchorIds.Any(id => CheckReach(player, id, anyReach.Range)),
            CrawlingReachCondition crawl => _standing.IsDown(player) && crawl.AnchorIds.Any(id => CheckReach(player, id, crawl.Range)),
            BothHandsAnchorsCondition both => CheckBothHands(player, both.AnchorIds),
            NotCondition not => !Evaluate(player, session, not.Condition),
            SlotContainsAnchorRecursiveCondition slot => CheckSlotContainsRecursive(player, slot),
            ToolQualitiesCondition tools => CheckToolQualities(player, tools),
            DoorStateAnchorCondition doorState => AnyAnchor(player, doorState.AnchorId,
                uid => TryComp<DoorComponent>(uid, out var door) && door.State == doorState.State),
            ItemPulledCondition pull => CheckPulling(player, session, pull),
            AnchorsNearCondition near => CheckAnchorsNear(player, near.AnchorId, near.NearAnchorId, near.Range, near.Away),
            ManualAcknowledgeCondition => session.Flags.Contains("ack"),
            InternalsOnCondition => _internals.AreInternalsWorking(player),
            DrainableReagentNearbyCondition reagent => CheckDrainableReagent(player, reagent),
            AnchorSolutionContainsCondition sol => CheckAnchorSolution(player, sol),
            PrototypeSolutionContainsCondition protoSol => CheckPrototypeSolution(player, protoSol),
            PlantTrayLevelsCondition levels => AnyAnchor(player, levels.AnchorId,
                uid => TryComp<PlantTrayComponent>(uid, out var tray)
                       && tray.WaterLevel >= levels.Water
                       && tray.NutritionLevel >= levels.Nutrition),
            UiOpenedAnchorCondition ui => session.Flags.Contains($"ui:{ui.AnchorId}"),
            // no anchor at all reads as closed on purpose - a trainee who ate the sheet or threw
            // it down a disposal is not left standing in front of a step that can never finish
            UiClosedAnchorCondition uiShut => !AnyAnchor(player, uiShut.AnchorId, uid => UiOpenFor(uid, player)),
            CombatModeCondition combat => (TryComp<CombatModeComponent>(player, out var mode)
                                           && mode.IsInCombatMode) == combat.Enabled,
            WiresPanelOpenCondition panel => AnyAnchor(player, panel.AnchorId,
                uid => TryComp<WiresPanelComponent>(uid, out var wires) && wires.Open == panel.Open),
            PlantTrayPlantedCondition tray => AnyAnchor(player, tray.AnchorId,
                uid => _plantTray.TryGetPlant((uid, null), out _)),
            DoorBoltedAnchorCondition bolts => AnyAnchor(player, bolts.AnchorId,
                uid => TryComp<DoorBoltComponent>(uid, out var bolt) && bolt.BoltsDown == bolts.Bolted),
            EntityDeletedCondition del => CheckDeleted(session, del.AnchorId),
            ClimbAnchorCondition climb => session.Flags.Contains($"climb:{climb.AnchorId}"),
            BuckledToAnchorCondition buckle => buckle.EntityAnchorId is { } sitter
                ? AnyAnchor(player, sitter, uid => BuckledTo(uid, buckle.AnchorId))
                : BuckledTo(player, buckle.AnchorId),
            AnchorAmmoFullCondition ammo => CountFullAmmo(player, ammo.AnchorId) >= ammo.Count,
            AnchorAmmoEmptyCondition drained => CheckAmmoEmpty(player, drained),
            // the map spawner keeps its anchor next to the mob it spawned and has no damage at all,
            // which would read as fully healed the moment the step starts
            AnchorDamageBelowCondition hurt => AnyAnchor(player, hurt.AnchorId,
                uid => HasComp<DamageableComponent>(uid) && DamageOf(uid, hurt.DamageType) <= hurt.Max),
            AnchorInsideAnchorCondition inside => AnyAnchor(player, inside.AnchorId, uid =>
                _container.TryGetContainingContainer((uid, null, null), out var holder)
                && TryComp<TutorialAnchorComponent>(holder.Owner, out var box)
                && box.AnchorId == inside.ContainerAnchorId),
            ItemSlotFilledCondition slot => AnyAnchor(player, slot.AnchorId,
                uid => _container.TryGetContainer(uid, slot.Slot, out var held) && held.ContainedEntities.Count > 0),
            UnbuckledCondition => !TryComp<BuckleComponent>(player, out var buckle) || !buckle.Buckled,
            CameraRotatedCondition rotated => CheckCameraRotated(player, session, rotated),
            FlagCondition flag => session.Flags.Contains(flag.Flag),
            TargetChangedCondition => session.Flags.Contains(TargetChangedFlag),
            HeldCondition held => CheckHeld(player, session, held),
            QuipsSaidCondition => !_mentor.QuipsPending(player),
            // the reset key zeroes this, and a fresh spawn starts at zero too
            CameraAlignedCondition aligned => !TryComp<InputMoverComponent>(player, out var mover)
                || Math.Abs(Angle.ShortestDistance(Angle.Zero, mover.TargetRelativeRotation).Degrees) <= aligned.Degrees,
            ExaminedAnchorCondition exam => session.Flags.Contains($"examined:{exam.AnchorId}"),
            ItemToggledCondition toggle => CheckToggled(player, toggle),
            EntityStunnedCondition stun => AnyAnchor(player, stun.AnchorId, uid => HasComp<StunnedComponent>(uid) || HasComp<KnockedDownComponent>(uid)),
            CuffedAnchorCondition cuff => AnyAnchor(player, cuff.AnchorId, uid => TryComp<CuffableComponent>(uid, out var c) && c.CuffedHandCount > 0),
            PaperSignedCondition paper => AnyAnchor(player, paper.AnchorId, IsSigned),
            PoweredAnchorCondition powered => AnyAnchor(player, powered.AnchorId, uid => _power.IsPowered(uid)),
            AmeInjectingCondition ame => AnyAnchor(player, ame.AnchorId, uid => _ame.IsInjecting(uid)),
            WearingSlotCondition wear => _inventory.TryGetSlotEntity(player, wear.Slot, out _),
            PuddlesClearedCondition puddles => CheckPuddlesCleared(player, puddles),
            DisposalFlushedWithAnchorCondition flush => session.Flags.Contains(FlushFlag(flush)),
            HealthAnalyzedCondition scan => session.Flags.Contains($"analyzed:{scan.AnchorId}"),
            MeleeHitAnchorCondition melee => session.Flags.Contains($"melee:{melee.AnchorId}"),
            AnchorDamagedCondition dmg => CheckDamaged(player, session, dmg.AnchorId),
            DeadAnchorCondition dead => AnyAnchor(player, dead.AnchorId, uid => _mobs.IsDead(uid)),
            AnchorAliveCondition alive => AnyAnchor(player, alive.AnchorId,
                uid => HasComp<MobStateComponent>(uid) && _mobs.IsAlive(uid)),
            CraftingMenuOpenedCondition => session.Flags.Contains("crafting"),
            ConstructionGhostOnAnchorCondition ghost => session.Flags.Contains(
                ghost.Stray ? $"ghost-stray:{ghost.AnchorId}" : $"ghost:{ghost.AnchorId}"),
            ExaminedNearAnchorCondition examinedNear => CheckExaminedNear(player, session, examinedNear),
            MachineFrameCompleteNearAnchorCondition frameDone => CheckFrameComplete(player, frameDone),
            AnchorEmptyOrGoneCondition empty => CheckEmptyOrGone(session, empty.AnchorId),
            IngestedReagentCondition ingested => session.Flags.Contains(IngestedReagentCondition.Flag(ingested.Reagent)),
            HoldingAnchorCondition hold => CheckHolding(player, hold.AnchorId, hold.Wielded),
            ShootTargetsCondition shoot => CheckShootTargets(player, session, shoot),
            HoldingPrototypeCondition holdProto => CheckHoldingPrototype(player, holdProto.Prototype),
            PrototypeNearAnchorCondition near => CountPrototype(player, near) >= near.Count,
            PrototypesOnAnchorsCondition onAnchors => CheckPrototypesOnAnchors(player, onAnchors),
            EyeProtectedCondition => IsEyeProtected(player),
            EyesHurtCondition => _eyes.EyesHurt(player),
            LatheMaterialsLoadedCondition loaded => AnyAnchor(player, loaded.AnchorId,
                uid => loaded.Materials.All(material => _materials.GetMaterialAmount(uid, material.Id) > 0)),
            LatheGuideGatheredCondition => CheckLatheGuideGathered(player, session),
            StackAmountNearCondition stackNear => CountStackNear(player, stackNear) >= stackNear.Min,
            MentorFinishedSpeakingCondition => session.Flags.Contains(TutorialMentorSystem.SpokeFlag)
                                               && !_mentor.IsSpeaking(player),
            _ => false,
        };
    }
}
