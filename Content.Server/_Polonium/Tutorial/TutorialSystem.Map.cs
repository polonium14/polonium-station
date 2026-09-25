using Content.Server.Construction.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Power.Components;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Interaction;

namespace Content.Server._Polonium.Tutorial;

public sealed partial class TutorialSystem
{
    private readonly HashSet<EntityUid> _pendingGhostStrips = new();

    private void OnMapCreated(TutorialMapCreatedEvent ev)
    {
        PrepareTutorialMap(ev.MapUid);
    }

    private void OnLockedConstruction(Entity<TutorialNoDeconstructComponent> ent, ref ConstructionInteractAttemptEvent args)
    {
        args.Cancelled = true;
        if (args.ShowPopup && args.User is { } user)
            PopupProtect(user, ent.Owner);
    }

    private void OnLockedCableCut(Entity<TutorialNoDeconstructComponent> ent, ref InteractUsingEvent args)
    {
        // only the cutters, a coil clicked on a cable still has to reach the placer
        if (args.Handled
            || !TryComp<CableComponent>(ent, out var cable)
            || cable.CuttingQuality is not { } quality
            || !_tool.HasQuality(args.Used, quality.Id))
            return;

        args.Handled = true;
        PopupProtect(args.User, ent.Owner);
    }

    private void OnItemConstruction(Entity<TutorialSessionComponent> ent, ref ConstructionStartAttemptEvent args)
    {
        if (args.Prototype.Type == ConstructionType.Item)
            args.Cancelled = true;
    }

    // removing anything while the entity is still spawning trips the lifecycle asserts, so it waits a tick
    private void OnGhostRoleInit(Entity<GhostRoleComponent> ent, ref ComponentInit args)
    {
        _pendingGhostStrips.Add(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingGhostStrips.Count == 0)
            return;

        foreach (var uid in _pendingGhostStrips)
        {
            if (!TerminatingOrDeleted(uid))
                StripGhostRole(uid);
        }

        _pendingGhostStrips.Clear();
    }

    private void PrepareTutorialMap(EntityUid mapUid)
    {
        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uid, out var xform))
        {
            if (xform.MapUid != mapUid)
                continue;

            if ((HasComp<ConstructionComponent>(uid) || HasComp<CableComponent>(uid))
                && !(TryComp<TutorialAnchorComponent>(uid, out var anchor) && anchor.AllowDeconstruct))
                EnsureComp<TutorialNoDeconstructComponent>(uid);

            StripGhostRole(uid);
            _npcs.StopRot(uid);
            _npcs.SatiateAndIdle(uid);
        }
    }

    private void StripGhostRole(EntityUid uid)
    {
        if (!IsOnTutorialMap(uid))
            return;

        RemComp<GhostRoleRaffleComponent>(uid);
        RemComp<GhostTakeoverAvailableComponent>(uid);
        RemComp<GhostRoleMobSpawnerComponent>(uid);
        RemComp<GhostRoleComponent>(uid);
    }

    private bool IsOnTutorialMap(EntityUid uid)
    {
        return Transform(uid).MapUid is { } map && HasComp<TutorialMapComponent>(map);
    }
}
