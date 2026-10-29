using Content.Shared._Polonium.Xenonids;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Xenonids;

/// <summary>
/// Ikonka etapu embrionu nad nosicielem. Kto ją widzi, ustala showTo w prototypie ikony.
/// </summary>
public sealed partial class XenoInfectedStatusIconSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoInfectedComponent, GetStatusIconsEvent>(OnGetStatusIcons);
    }

    private void OnGetStatusIcons(Entity<XenoInfectedComponent> ent, ref GetStatusIconsEvent args)
    {
        var icons = ent.Comp.StageIcons;
        if (icons.Count == 0)
            return;

        var stage = Math.Clamp(ent.Comp.Stage, 0, icons.Count - 1);
        if (_prototype.TryIndex(icons[stage], out var icon))
            args.StatusIcons.Add(icon);
    }
}
