using Content.Client.Overlays;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.Body;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Medical.Surgery;

/// <summary>
/// With a medical HUD on, marks patients left with an open surgery, so whoever walks past knows someone has to finish
/// or close them up.
/// </summary>
public sealed partial class ShowSurgeryIconsSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedSurgerySystem _surgery = default!;

    // The medical HUD itself; another HUD system on the same component would subscribe its events twice.
    [Dependency] private ShowHealthIconsSystem _healthIcons = default!;

    private static readonly ProtoId<StatusIconPrototype> SurgeryIcon = "SurgeryInProgressIcon";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, GetStatusIconsEvent>(OnGetStatusIcons);
    }

    private void OnGetStatusIcons(Entity<BodyComponent> ent, ref GetStatusIconsEvent args)
    {
        if (!_healthIcons.IsActive || ent.Comp.Organs is not { } organs)
            return;

        foreach (var organ in organs.ContainedEntities)
        {
            if (!_surgery.HasUnfinishedSurgerySteps(organ))
                continue;

            args.StatusIcons.Add(_proto.Index(SurgeryIcon));
            return;
        }
    }
}
