// SPDX-FileCopyrightText: 2025 beck <163376292+widgetbeck@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Damian Zieliński <zientasek.pl@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CombatMode;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Impstation.Replicator;

/// <summary>
/// Shared replicator interactions that can run under prediction.
/// </summary>
public abstract partial class SharedReplicatorSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<ReplicatorComponent, ToggleCombatActionEvent>(OnCombatToggle);
        SubscribeLocalEvent<ReplicatorComponent, ReplicatorSpawnNestActionEvent>(OnSpawnNestAction);
        SubscribeLocalEvent<ReplicatorComponent, MapInitEvent>(OnMapInit);

        InitializeTeleportPrey();
    }

    private void OnMapInit(Entity<ReplicatorComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        var stunRay = Spawn(ent.Comp.StunRayProto, Transform(ent).Coordinates);
        _hands.TryPickup(ent, stunRay, ent.Comp.StunRayHandId, checkActionBlocker: false);
    }

    private void OnAttackAttempt(Entity<ReplicatorComponent> ent, ref AttackAttemptEvent args)
    {
        if (HasComp<ReplicatorComponent>(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("replicator-on-replicator-attack-fail"), ent, ent, PopupType.MediumCaution);
            args.Cancel();
            return;
        }

        if (HasComp<ReplicatorNestComponent>(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("replicator-on-nest-attack-fail"), ent, ent, PopupType.MediumCaution);
            args.Cancel();
        }
    }

    private void OnCombatToggle(Entity<ReplicatorComponent> ent, ref ToggleCombatActionEvent args)
    {
        if (!TryComp<CombatModeComponent>(ent, out var combat))
            return;

        _appearance.SetData(ent, ReplicatorVisuals.Combat, combat.IsInCombatMode);
    }

    private void OnSpawnNestAction(Entity<ReplicatorComponent> ent, ref ReplicatorSpawnNestActionEvent args)
    {
        if (!_timing.IsFirstTimePredicted || _net.IsClient)
            return;

        var xform = Transform(ent);
        var coords = xform.Coordinates;

        if (!coords.IsValid(EntityManager) || xform.MapID == MapId.Nullspace)
            return;

        var myNest = Spawn("ReplicatorNest", coords);
        var myNestComp = EnsureComp<ReplicatorNestComponent>(myNest);

        HashSet<EntityUid> newMinions = [];
        foreach (var (uid, _) in ent.Comp.RelatedReplicators)
            newMinions.Add(uid);

        myNestComp.SpawnedMinions = newMinions;
        myNestComp.SpawnedMinions.Add(ent);
        ent.Comp.MyNest = myNest;
        ent.Comp.RelatedReplicators.Clear();

        QueueDel(args.Action);
    }
}
