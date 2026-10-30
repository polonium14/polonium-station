using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Polonium.Hearing;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Movement.Components;
using Content.Shared.Trigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Polonium.Hearing;

/// <summary>
/// Headphones: switched on over the ears they muffle speech, and worn anywhere on the head they keep off the
/// flashbang's bang, which otherwise slows like a changeling screech.
/// </summary>
[TestOf(typeof(HearingMufflingSystem))]
[TestOf(typeof(BangOnTriggerSystem))]
public sealed class HeadphonesTest : InteractionTest
{
    private const string HeadphonesProto = "ClothingMultipleHeadphones";
    private const string FlashbangProto = "GrenadeFlashBang";
    private const string MobProto = "MobHuman";

    [Test]
    public async Task MuffleOnlyWhenOnAndOverEars()
    {
        var muffling = SEntMan.System<HearingMufflingSystem>();
        var inventory = SEntMan.System<InventorySystem>();
        var toggle = SEntMan.System<ItemToggleSystem>();

        EntityUid mob = default;
        EntityUid phones = default;
        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetCoordinates(TargetCoords);
            mob = SEntMan.SpawnEntity(MobProto, coords);
            phones = SEntMan.SpawnEntity(HeadphonesProto, coords);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(inventory.TryEquip(mob, phones, "neck", true, true), "Headphones must fit around the neck.");
            Assert.That(toggle.TrySetActive(phones, true), "Headphones must switch on.");
            Assert.That(muffling.IsHearingMuffled(mob), Is.False, "Headphones around the neck don't cover the ears.");

            Assert.That(inventory.TryUnequip(mob, "neck", true, true));
            Assert.That(inventory.TryEquip(mob, phones, "ears", true, true), "Headphones must fit over the ears.");
            Assert.That(muffling.IsHearingMuffled(mob), "Switched-on headphones over the ears muffle speech.");

            Assert.That(toggle.TrySetActive(phones, false));
            Assert.That(muffling.IsHearingMuffled(mob), Is.False, "Switched-off headphones let speech through.");
        });
    }

    [Test]
    public async Task FlashbangBangSkipsHeadphones()
    {
        var inventory = SEntMan.System<InventorySystem>();
        var trigger = SEntMan.System<TriggerSystem>();

        EntityUid deaf = default;
        EntityUid hearing = default;
        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetCoordinates(TargetCoords);
            hearing = SEntMan.SpawnEntity(MobProto, coords);
            deaf = SEntMan.SpawnEntity(MobProto, coords);

            // switched off on purpose: the protection doesn't need the headphones on
            var phones = SEntMan.SpawnEntity(HeadphonesProto, coords);
            Assert.That(inventory.TryEquip(deaf, phones, "head", true, true), "Headphones must fit on the head.");

            var bang = SEntMan.SpawnEntity(FlashbangProto, coords.Offset(new System.Numerics.Vector2(1f, 0f)));
            trigger.Trigger(bang, key: "timer");
        });

        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var hearingSpeed = SEntMan.GetComponent<MovementSpeedModifierComponent>(hearing).CurrentWalkSpeed;
            var deafSpeed = SEntMan.GetComponent<MovementSpeedModifierComponent>(deaf).CurrentWalkSpeed;

            // both see the flash, so only the bang's halving tells them apart
            Assert.That(hearingSpeed, Is.LessThan(deafSpeed * 0.75f),
                "The bang must slow whoever hears it, and the headphones must keep it off.");
        });
    }
}
