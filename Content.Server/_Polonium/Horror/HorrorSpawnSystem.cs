using Content.Shared.Antag;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Shared._Polonium.Horror.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.GameTicking.Rules;
using Content.Shared.Ghost;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Content.Shared.Objectives.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server._Polonium.Horror;

/// <summary>
/// Where horrors come from: the vent event and the smuggled pet carrier from the uplink.
/// </summary>
public sealed partial class HorrorSpawnSystem : GameRuleSystem<HorrorVentSpawnComponent>
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ServerStationSystem _station = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedObjectivesSystem _objectives = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private Content.Shared.Popups.SharedPopupSystem _popup = default!;

    private static readonly Robust.Shared.Prototypes.EntProtoId ServeObjective = "HorrorServeObjective";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HorrorVentSpawnComponent, AntagSelectLocationEvent>(OnSelectLocation);
        SubscribeLocalEvent<EldritchHorrorComponent, GhostRoleSpawnerUsedEvent>(OnSpawnerUsed);
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAdded);
        SubscribeLocalEvent<HorrorCarrierComponent, UseInHandEvent>(OnCarrierUse);
    }

    private void OnCarrierUse(Entity<HorrorCarrierComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.Woken)
        {
            _popup.PopupEntity(Loc.GetString("horror-carrier-used"), ent, args.User);
            return;
        }

        ent.Comp.Woken = true;
        EntityManager.AddComponents(ent, ent.Comp.WakeComponents);
        _popup.PopupEntity(Loc.GetString("horror-carrier-wake"), ent, args.User);
    }

    protected override void Added(Entity<HorrorVentSpawnComponent, GameRuleComponent> rule, ref GameRuleAddedEvent args)
    {
        base.Added(rule, ref args);

        // picked once, because the location event is raised both for the ghost role spawner and the taken role
        var vents = _station.GetEntitiesWithComponentOnStation<VentCritterSpawnLocationComponent>(true);
        if (vents.Count > 0)
            rule.Comp1.Coords = Transform(_random.Pick(vents)).Coordinates;
        else if (_station.TryFindRandomTile(out _, out _, out _, out var coords))
            rule.Comp1.Coords = coords;
    }

    private void OnSelectLocation(Entity<HorrorVentSpawnComponent> ent, ref AntagSelectLocationEvent args)
    {
        if (ent.Comp.Coords is { } coords)
            args.Coordinates.Add(_transform.ToMapCoordinates(coords));
    }

    /// <summary>
    /// A horror woken from the carrier serves whoever holds the carrier.
    /// </summary>
    private void OnSpawnerUsed(Entity<EldritchHorrorComponent> ent, ref GhostRoleSpawnerUsedEvent args)
    {
        if (!TryComp<HorrorCarrierComponent>(args.Spawner, out var carrier))
            return;

        var servant = EnsureComp<HorrorServantComponent>(ent);
        servant.PendingObjectives = new(carrier.Objectives);
        if (_container.TryGetContainingContainer(args.Spawner, out var container)
            && _mind.TryGetMind(container.Owner, out var master, out _))
        {
            servant.Master = master;
        }

        Dirty(ent, servant);
        QueueDel(args.Spawner);
    }

    /// <summary>
    /// The servant objectives need the horror role, which the ghost role adds only after moving the mind in,
    /// so they go in once a role lands on the mind of a servant horror.
    /// </summary>
    private void OnRoleAdded(RoleAddedEvent args)
    {
        if (args.Mind.OwnedEntity is { } body)
            GiveServantObjectives(body, args.MindId, args.Mind);
    }

    /// <summary>
    /// Hands the pending servant objectives to the mind. Objectives the mind can't take yet stay pending.
    /// </summary>
    public void GiveServantObjectives(EntityUid horror, EntityUid mindId, MindComponent mind)
    {
        if (!TryComp<HorrorServantComponent>(horror, out var servant) || servant.PendingObjectives.Count == 0)
            return;

        var masterName = servant.Master is { } master
            ? CompOrNull<MindComponent>(master)?.CharacterName
            : null;

        foreach (var objective in new List<Robust.Shared.Prototypes.EntProtoId>(servant.PendingObjectives))
        {
            if (!_objectives.TryCreateObjective((mindId, mind), objective, out var created))
                continue;

            servant.PendingObjectives.Remove(objective);
            _mind.AddObjective(mindId, mind, created.Value);
            if (objective == ServeObjective && masterName != null)
                _metaData.SetEntityName(created.Value, Loc.GetString("objective-horror-serve-title", ("name", masterName)));
        }
    }
}
