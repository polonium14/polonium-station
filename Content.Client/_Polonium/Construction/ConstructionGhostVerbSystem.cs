using Content.Client.Construction;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Client._Polonium.Construction;

/// <summary>
/// Adds a context menu verb that removes one of the player's own construction ghosts.
/// </summary>
public sealed partial class ConstructionGhostVerbSystem : EntitySystem
{
    [Dependency] private ConstructionSystem _construction = default!;

    private static readonly SpriteSpecifier RemoveIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/delete.svg.192dpi.png"));

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConstructionGhostComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<ConstructionGhostComponent> ghost, ref GetVerbsEvent<Verb> args)
    {
        var ghostId = ghost.Comp.GhostId;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("construction-ghost-verb-remove"),
            Icon = RemoveIcon,
            ClientExclusive = true,
            Act = () => _construction.ClearGhost(ghostId),
        });
    }
}
