using Content.Shared.Roles;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server.Jobs;

/// <summary>
/// Polonium: adds tags to a mob on job equip. Unlike <see cref="AddComponentSpecial"/> (which skips a
/// TagComponent the mob already has), this merges into the existing tags via <see cref="TagSystem.AddTags"/>,
/// so it works on humans that already carry base tags (e.g. tagging the clown "Clumsy").
/// </summary>
public sealed partial class AddTagSpecial : JobSpecial
{
    [DataField(required: true)]
    public List<ProtoId<TagPrototype>> Tags = new();

    public override void AfterEquip(EntityUid mob)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        entMan.System<TagSystem>().AddTags(mob, Tags);
    }
}
