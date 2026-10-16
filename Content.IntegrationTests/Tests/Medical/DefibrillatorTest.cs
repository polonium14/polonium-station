using System.Linq; // Polonium
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Tests for defibrilators.
/// </summary>
[TestOf(typeof(DefibrillatorComponent))]
public sealed class DefibrillatorTest : InteractionTest
{
    // We need two hands to use a defbrillator.
    protected override string PlayerPrototype => "MobHuman";

    private static readonly EntProtoId DefibrillatorProtoId = "Defibrillator";
    private static readonly EntProtoId TargetProtoId = "MobHuman";
    private static readonly ProtoId<DamageTypePrototype> BluntDamageTypeId = "Blunt";

    /// <summary>
    /// Kills a target mob, heals them and then revives them with a defibrillator.
    /// </summary>
    [Test]
    public async Task KillAndReviveTest()
    {
        var damageableSystem = SEntMan.System<DamageableSystem>();
        var mobThresholdsSystem = SEntMan.System<MobThresholdSystem>();

        // Don't let the player and target suffocate.
        await AddAtmosphere();

        await SpawnTarget(TargetProtoId);

        var targetMobState = Comp<MobStateComponent>();
        var targetDamageable = Comp<DamageableComponent>();

        // Check that the target has no damage and is not crit or dead.
        Assert.Multiple(() =>
        {
            Assert.That(targetMobState.CurrentState, Is.EqualTo(MobState.Alive), "Target mob was not alive when spawned.");
            Assert.That(damageableSystem.GetTotalDamage(STarget!.Value), Is.EqualTo(FixedPoint2.Zero), "Target mob was damaged when spawned.");
        });

        // Get the damage needed to kill or crit the target.
        var critThreshold = mobThresholdsSystem.GetThresholdForState(STarget.Value, MobState.Critical);
        var deathThreshold = mobThresholdsSystem.GetThresholdForState(STarget.Value, MobState.Dead);
        var critDamage = new DamageSpecifier(ProtoMan.Index(BluntDamageTypeId), (critThreshold + deathThreshold) / 2);
        var deathDamage = new DamageSpecifier(ProtoMan.Index(BluntDamageTypeId), deathThreshold);

        // Kill the target by applying blunt damage.
        await Server.WaitPost(() => damageableSystem.SetDamage((STarget.Value, targetDamageable), deathDamage));
        await RunTicks(3);

        // Check that the target is dead.
        Assert.Multiple(() =>
        {
            Assert.That(targetMobState.CurrentState, Is.EqualTo(MobState.Dead), "Target mob did not die from deadly damage amount.");
            Assert.That(damageableSystem.GetTotalDamage(STarget!.Value), Is.EqualTo(deathThreshold), "Target mob had the wrong total damage amount after being killed.");
        });

        // Spawn a defib and activate it.
        var defib = await PlaceInHands(DefibrillatorProtoId, enableToggleable: true);
        var cooldown = Comp<DefibrillatorComponent>(defib).ZapDelay;

        // Wait for the cooldown.
        await RunSeconds((float)cooldown.TotalSeconds);

        // ZAP!
        await Interact();

        // Check that the target is still dead since it is over the crit threshold.
        // And it should have taken some extra damage.
        Assert.Multiple(() =>
        {
            Assert.That(targetMobState.CurrentState, Is.EqualTo(MobState.Dead), "Target mob was revived despite being over the death damage threshold.");
            Assert.That(damageableSystem.GetTotalDamage(STarget!.Value), Is.GreaterThan(deathThreshold), "Target mob did not take damage from being defibrillated.");
        });

        // Set the damage halfway between the crit and death thresholds so that the target can be revived.
        await Server.WaitPost(() => damageableSystem.SetDamage((STarget.Value, targetDamageable), critDamage));
        await RunTicks(3);

        // Check that the target is still dead.
        Assert.That(targetMobState.CurrentState, Is.EqualTo(MobState.Dead), "Target mob revived on its own.");

        // ZAP!
        await RunSeconds((float)cooldown.TotalSeconds);
        await Interact();

        // The target should be revived, but in crit.
        Assert.That(targetMobState.CurrentState, Is.EqualTo(MobState.Critical), "Target mob was not revived from being defibrillated.");
    }

    /// <summary>
    /// A glued defibrillator cannot be dropped when its user shocks themself, so it must keep both hands occupied.
    /// </summary>
    [Test]
    public async Task GluedDefibrillatorSelfZapKeepsBothHandsTest()
    {
        await AddAtmosphere();

        var defib = await PlaceInHands(DefibrillatorProtoId, enableToggleable: true);
        var sDefib = ToServer(defib);

        // Applying glue makes the held item unremoveable. Add the resulting component after pickup without depending
        // on the chemistry interaction in this integration test.
        await Server.WaitPost(() =>
        {
            var unremoveable = SEntMan.EnsureComponent<UnremoveableComponent>(sDefib);
            unremoveable.DeleteOnDrop = false;
            SEntMan.Dirty(sDefib, unremoveable);
        });

        var hands = SEntMan.System<SharedHandsSystem>();
        await RunTicks(1);
        Assert.Multiple(() =>
        {
            Assert.That(hands.EnumerateHeld(SPlayer).Count(), Is.EqualTo(2));
            Assert.That(hands.EnumerateHeld(SPlayer).Count(uid => SEntMan.HasComponent<VirtualItemComponent>(uid)), Is.EqualTo(1));
        });

        var cooldown = Comp<DefibrillatorComponent>(defib).ZapDelay;
        await RunSeconds((float) cooldown.TotalSeconds);

        // Shock the user with the defibrillator.
        await Interact(ToServer(Player), ToServer(PlayerCoords));

        Assert.Multiple(() =>
        {
            Assert.That(hands.EnumerateHeld(SPlayer).Count(), Is.EqualTo(2));
            Assert.That(hands.EnumerateHeld(SPlayer).Count(uid => SEntMan.HasComponent<VirtualItemComponent>(uid)), Is.EqualTo(1));
        });
    }
}
