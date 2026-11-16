using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared.Administration.Logs;
using Content.Shared.Body;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Robust.Shared.Containers;

namespace Content.Shared._Polonium.Medical.Surgery;

/// <summary>
/// The chest cavity: through an opened ribcage a surgeon can hide one small item in the torso and take it out again.
/// The item keeps working inside (a primed grenade still goes off) and drops out when the torso is gone.
/// </summary>
public sealed partial class SurgicalCavitySystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public const string ContainerId = "polonium_surgical_cavity";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryCavityInsertStepComponent, SurgeryCanPerformStepEvent>(OnInsertCanPerform);
        SubscribeLocalEvent<SurgeryCavityInsertStepComponent, SurgeryStepEvent>(OnInsertStep);
        SubscribeLocalEvent<SurgeryCavityRemoveStepComponent, SurgeryStepEvent>(OnRemoveStep);
        SubscribeLocalEvent<SurgicalCavityComponent, EntityTerminatingEvent>(OnPartTerminating);
        SubscribeLocalEvent<SurgicalCavityComponent, EntGotRemovedFromContainerMessage>(OnPartRemoved);
    }

    /// <summary>
    /// What is hidden in the part's cavity, if anything.
    /// </summary>
    public EntityUid? GetContents(EntityUid part)
    {
        return _container.TryGetContainer(part, ContainerId, out var cavity) && cavity.ContainedEntities.Count > 0
            ? cavity.ContainedEntities[0]
            : null;
    }

    /// <summary>
    /// The insert step is done once something is in the cavity, the remove step once it is empty.
    /// </summary>
    public bool IsStepComplete(EntityUid step, EntityUid part)
    {
        if (HasComp<SurgeryCavityInsertStepComponent>(step) && GetContents(part) == null)
            return false;

        if (HasComp<SurgeryCavityRemoveStepComponent>(step) && GetContents(part) != null)
            return false;

        return true;
    }

    private void OnInsertCanPerform(Entity<SurgeryCavityInsertStepComponent> ent, ref SurgeryCanPerformStepEvent args)
    {
        if (args.IsInvalid)
            return;

        // An empty hand gives the surgeon themselves as the tool.
        if (args.Tool == args.User || !TryComp<ItemComponent>(args.Tool, out var item))
        {
            args.Invalid = StepInvalidReason.MissingTool;
            args.Popup = Loc.GetString("surgery-cavity-need-item");
            return;
        }

        if (_item.GetItemSizeWeight(item.Size) > _item.GetItemSizeWeight(ent.Comp.MaxSize))
        {
            args.Invalid = StepInvalidReason.ToolInvalid;
            args.Popup = Loc.GetString("surgery-cavity-too-big", ("item", args.Tool));
        }
    }

    private void OnInsertStep(Entity<SurgeryCavityInsertStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (args.Tool == args.User || !HasComp<ItemComponent>(args.Tool) || GetContents(args.Part) != null)
            return;

        EnsureComp<SurgicalCavityComponent>(args.Part);
        var cavity = _container.EnsureContainer<ContainerSlot>(args.Part, ContainerId);
        if (!_container.Insert(args.Tool, cavity))
            return;

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.User):user} hid {ToPrettyString(args.Tool):item} in the chest cavity of {ToPrettyString(args.Body):target}");
    }

    private void OnRemoveStep(Entity<SurgeryCavityRemoveStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (GetContents(args.Part) is not { } item || !_container.TryRemoveFromContainer(item))
            return;

        _hands.PickupOrDrop(args.User, item);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.User):user} took {ToPrettyString(item):item} out of the chest cavity of {ToPrettyString(args.Body):target}");
    }

    private void OnPartTerminating(Entity<SurgicalCavityComponent> ent, ref EntityTerminatingEvent args)
    {
        DropContents(ent);
    }

    /// <summary>
    /// A torso leaving its body, gibbed or cut away, spills what was hidden in it.
    /// </summary>
    private void OnPartRemoved(Entity<SurgicalCavityComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        DropContents(ent);
    }

    /// <summary>
    /// Drops the hidden item next to the body, or the loose part, rather than into the body's organ container.
    /// </summary>
    private void DropContents(EntityUid part)
    {
        if (GetContents(part) is not { } item || !_container.TryRemoveFromContainer(item))
            return;

        var nextTo = CompOrNull<OrganComponent>(part)?.Body ?? part;
        _transform.DropNextTo(item, nextTo);
    }
}
