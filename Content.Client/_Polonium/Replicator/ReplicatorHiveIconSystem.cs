using Content.Shared._Impstation.Replicator;
using Content.Shared._Polonium.Replicator;
using Content.Shared.Antag;
using Content.Shared.Ghost.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Replicator;

public sealed partial class ReplicatorHiveIconSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ReplicatorModuleSystem _modules = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorModulesComponent, GetStatusIconsEvent>(OnGetStatusIcons);
        SubscribeLocalEvent<ReplicatorColossusComponent, GetStatusIconsEvent>(OnGetColossusIcon);
    }

    private void OnGetColossusIcon(Entity<ReplicatorColossusComponent> ent, ref GetStatusIconsEvent args)
    {
        if (CanSeeIcons() && !_mobState.IsDead(ent) && _prototype.TryIndex(ent.Comp.Icon, out var icon))
            args.StatusIcons.Add(icon);
    }

    private void OnGetStatusIcons(Entity<ReplicatorModulesComponent> ent, ref GetStatusIconsEvent args)
    {
        if (!CanSeeIcons() || _mobState.IsDead(ent))
            return;

        var id = _modules.TryGetClass(ent.AsNullable(), out var prototype) && prototype.Icon is { } classIcon
            ? classIcon
            : ent.Comp.Icon;

        if (_prototype.TryIndex(id, out var icon))
            args.StatusIcons.Add(icon);

        if (CompOrNull<ReplicatorComponent>(ent)?.Queen == true && _prototype.TryIndex(ent.Comp.QueenIcon, out var queen))
            args.StatusIcons.Add(queen);
    }

    // Other antagonists can carry ShowAntagIcons too, so the prototype's whitelist alone would let them in.
    private bool CanSeeIcons()
    {
        var viewer = _player.LocalEntity;
        return HasComp<ReplicatorComponent>(viewer)
               || HasComp<GhostComponent>(viewer) && HasComp<ShowAntagIconsComponent>(viewer);
    }
}
