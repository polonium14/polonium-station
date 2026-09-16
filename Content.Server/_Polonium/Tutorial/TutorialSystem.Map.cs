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
        if (args.Handled || !HasComp<CableComponent>(ent))
            return;

        args.Handled = true;
        PopupProtect(args.User, ent.Owner);
    }

    private void OnItemConstruction(Entity<TutorialSessionComponent> ent, ref ConstructionStartAttemptEvent args)
    {
        if (args.Prototype.Type == ConstructionType.Item)
            args.Cancelled = true;
    }

    private void OnGhostRoleStartup(Entity<GhostRoleComponent> ent, ref ComponentStartup args)
    {
        StripGhostRole(ent.Owner, deferred: true);
    }

    private void PrepareTutorialMap(EntityUid mapUid)
    {
        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uid, out var xform))
        {
            if (xform.MapUid != mapUid)
                continue;

            if (HasComp<ConstructionComponent>(uid) || HasComp<CableComponent>(uid))
                EnsureComp<TutorialNoDeconstructComponent>(uid);

            StripGhostRole(uid, deferred: false);
            _npcs.SatiateAndIdle(uid);
        }
    }

    private void StripGhostRole(EntityUid uid, bool deferred)
    {
        if (!IsOnTutorialMap(uid))
            return;

        Strip<GhostRoleRaffleComponent>(uid, deferred);
        Strip<GhostTakeoverAvailableComponent>(uid, deferred);
        Strip<GhostRoleMobSpawnerComponent>(uid, deferred);
        Strip<GhostRoleComponent>(uid, deferred);
    }

    private void Strip<T>(EntityUid uid, bool deferred) where T : IComponent, new()
    {
        if (!HasComp<T>(uid))
            return;

        if (deferred)
            RemCompDeferred<T>(uid);
        else
            RemComp<T>(uid);
    }

    private bool IsOnTutorialMap(EntityUid uid)
    {
        return Transform(uid).MapUid is { } map && HasComp<TutorialMapComponent>(map);
    }
}
