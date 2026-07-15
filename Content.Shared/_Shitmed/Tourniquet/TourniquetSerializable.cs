using Content.Shared.Body;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Shitmed.Tourniquet;

[Serializable, NetSerializable]
public sealed partial class TourniquetDoAfterEvent : SimpleDoAfterEvent
{
    /// <summary>
    /// The category validated (against TourniquetComponent.BlockedCategories) when the DoAfter
    /// started - snapshotted here rather than re-read from the user's TargetingComponent.Target
    /// when the DoAfter completes, since that's a live, client-mutable field the user can freely
    /// change mid-DoAfter to bypass the block that was actually checked.
    /// </summary>
    public readonly ProtoId<OrganCategoryPrototype> Category;

    public TourniquetDoAfterEvent(ProtoId<OrganCategoryPrototype> category)
    {
        Category = category;
    }
}

[Serializable, NetSerializable]
public sealed partial class RemoveTourniquetDoAfterEvent : SimpleDoAfterEvent
{
}
