using Content.Shared.Inventory;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared.Nutrition.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCreamPieSystem))]
public sealed partial class CreamPieStunImmuneComponent : Component
{
    [DataField]
    public bool RequireAttachedHelmet = true;
}

[ByRefEvent]
public record struct CreamPieStunAttemptEvent : IInventoryRelayEvent
{
    public SlotFlags TargetSlots => SlotFlags.OUTERCLOTHING;

    /// <summary>
    /// If true, creampie will not stun or knock down the target
    /// </summary>
    public bool Cancelled;
}
