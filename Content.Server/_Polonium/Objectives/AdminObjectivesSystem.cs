using Content.Server.Objectives.Components;
using Content.Shared._Polonium.Objectives;
using Content.Shared.Objectives.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Objectives;

/// <summary>
/// Keeps open admin objective windows up to date, objectives can change at any time (antag roles, progress).
/// </summary>
public sealed partial class AdminObjectivesSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    private const string NameLocPrefix = "admin-objectives-name-";

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    private readonly HashSet<AdminObjectivesEui> _openEuis = new();
    private List<AdminStandardObjective>? _standardObjectives;
    private TimeSpan _accumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_openEuis.Count == 0)
            return;

        _accumulator += TimeSpan.FromSeconds(frameTime);
        if (_accumulator < RefreshInterval)
            return;

        _accumulator = TimeSpan.Zero;
        foreach (var eui in _openEuis)
        {
            eui.Refresh();
        }
    }

    public void Register(AdminObjectivesEui eui)
    {
        _openEuis.Add(eui);
    }

    public void Unregister(AdminObjectivesEui eui)
    {
        _openEuis.Remove(eui);
    }

    /// <summary>
    /// Objective prototypes an admin can assign, with names readable in a list.
    /// </summary>
    public List<AdminStandardObjective> GetStandardObjectives()
    {
        if (_standardObjectives != null)
            return _standardObjectives;

        _standardObjectives = new List<AdminStandardObjective>();
        foreach (var proto in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract ||
                proto.ID == CustomObjectives.Prototype ||
                !proto.TryComp<ObjectiveComponent>(out var objective, EntityManager.ComponentFactory))
            {
                continue;
            }

            _standardObjectives.Add(new AdminStandardObjective(proto.ID, GetListName(proto), objective.Issuer));
        }

        _standardObjectives.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        return _standardObjectives;
    }

    private string GetListName(EntityPrototype proto)
    {
        if (Loc.TryGetString(NameLocPrefix + proto.ID, out var name))
            return name;

        if (!string.IsNullOrWhiteSpace(proto.Name))
            return proto.Name;

        if (proto.TryComp<StealConditionComponent>(out var steal, EntityManager.ComponentFactory) &&
            _prototypes.TryIndex(steal.StealGroup, out var group))
        {
            return Loc.GetString("admin-objectives-steal", ("item", Loc.GetString(group.Name)));
        }

        return proto.ID;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>())
            _standardObjectives = null;
    }
}
