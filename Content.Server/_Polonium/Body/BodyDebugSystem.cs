using System.Linq;
using Content.Server._Polonium.Medical.PatientIntake;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._Polonium.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Administration;
using Content.Shared.Body;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Body;

/// <summary>
/// Admin tool for damaging, healing, killing and swapping organs without hurting the body by hand.
/// Every action goes through the path the game itself uses, so the result matches real damage and surgery.
/// </summary>
public sealed partial class BodyDebugSystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IDependencyCollection _deps = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private OrganRelationSystem _relation = default!;
    [Dependency] private PatientIntakeSystem _intake = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private TraumaSystem _trauma = default!;

    public const AdminFlags RequiredFlag = AdminFlags.Debug;

    private const string ModifierId = "WoundableDamage";
    private static readonly ProtoId<OrganCategoryPrototype> Brain = "Brain";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!HasComp<BodyComponent>(args.Target)
            || !TryComp<ActorComponent>(args.User, out var actor)
            || !_admin.HasAdminFlag(actor.PlayerSession, RequiredFlag))
            return;

        var session = actor.PlayerSession;
        var target = args.Target;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("body-debug-verb"),
            Category = VerbCategory.Debug,
            Act = () => _eui.OpenEui(new BodyDebugEui(_deps, target), session),
            Impact = LogImpact.Low,
        });
    }

    /// <summary>
    /// Sets an organ's integrity to a percentage of its cap, clamped to the organ's floor.
    /// Going down creates a real organ trauma, going up heals the organ's damage modifiers.
    /// </summary>
    /// <returns>False if the organ is not in the body, has no integrity or nothing could hold the wound.</returns>
    public bool TrySetOrganIntegrity(EntityUid body, EntityUid organ, int percent)
    {
        if (!TryGetOrgan(body, organ, out var organComp) || !TryComp<OrganIntegrityComponent>(organ, out var integrity))
            return false;

        var cap = integrity.IntegrityCap;
        var target = FixedPoint2.Clamp(cap * Math.Clamp(percent, 0, 100) / FixedPoint2.New(100), integrity.IntegrityFloor, cap);

        // Compared against the summed modifiers, not the integrity: the integrity is clamped,
        // so an organ sitting on its floor can hide more damage than it shows.
        var damage = integrity.IntegrityModifiers.Values.Aggregate(FixedPoint2.Zero, (sum, value) => sum + value);
        var wanted = cap - target;

        if (wanted > damage)
            return TryDamageOrgan(body, (organ, organComp, integrity), wanted - damage);

        HealOrgan((organ, integrity), damage - wanted);
        return true;
    }

    private bool TryDamageOrgan(EntityUid body, Entity<OrganComponent, OrganIntegrityComponent> organ, FixedPoint2 amount)
    {
        // Growing a trauma the organ already has keeps every use from adding another wound to the body part.
        foreach (var ((id, owner), value) in organ.Comp2.IntegrityModifiers.ToArray())
        {
            if (value > FixedPoint2.Zero && HasComp<TraumaComponent>(owner))
                return _trauma.TryChangeOrganDamageModifier(organ, amount, owner, id, organ.Comp2);
        }

        if (organ.Comp1.Category is not { } category
            || !_intake.TryGetHousingLimb(body, category, out var limb)
            || _intake.InduceWound(limb) is not { } inflicter)
            return false;

        var trauma = _trauma.AddTrauma(organ, limb, inflicter, TraumaType.OrganDamage, amount);
        return trauma != EntityUid.Invalid && _trauma.TryCreateOrganDamageModifier(organ, amount, trauma, ModifierId, organ.Comp2);
    }

    private void HealOrgan(Entity<OrganIntegrityComponent> organ, FixedPoint2 amount)
    {
        // Trauma damage first, the way surgery heals it. The rest follows, or the organ could never reach its cap.
        var modifiers = organ.Comp.IntegrityModifiers
            .Where(modifier => modifier.Value > FixedPoint2.Zero)
            .OrderByDescending(modifier => HasComp<TraumaComponent>(modifier.Key.Item2))
            .ToArray();

        foreach (var ((id, owner), value) in modifiers)
        {
            if (amount <= FixedPoint2.Zero)
                return;

            if (value <= amount)
            {
                amount -= value;
                _trauma.TryRemoveOrganDamageModifier(organ, owner, id, organ.Comp);
                continue;
            }

            _trauma.TryChangeOrganDamageModifier(organ, -amount, owner, id, organ.Comp);
            return;
        }
    }

    /// <summary>
    /// Destroys an organ the way lethal damage would. The brain is refused, it cannot be destroyed.
    /// </summary>
    public bool TryKillOrgan(EntityUid body, EntityUid organ)
    {
        return TryGetOrgan(body, organ, out var organComp)
               && organComp.Category != Brain
               && TrySetOrganIntegrity(body, organ, 0);
    }

    /// <summary>
    /// Replaces a dead organ with a fresh one from the body's manifest, attached to the same body part.
    /// </summary>
    public bool TryReviveOrgan(EntityUid body, EntityUid organ)
    {
        if (!TryGetOrgan(body, organ, out var organComp)
            || !HasComp<DeadOrganComponent>(organ)
            || organComp.Category is not { } category
            || !TryGetFreshOrgan(body, category, out _)
            || !TryComp<BodyComponent>(body, out var bodyComp))
            return false;

        var parent = CompOrNull<ChildOrganComponent>(organ)?.Parent;
        _container.Remove(organ, bodyComp.Organs!, force: true);
        QueueDel(organ);

        return TryInsertOrgan(body, category, parent);
    }

    /// <summary>
    /// Takes an organ out of the body and leaves it next to it. The brain and body parts are refused.
    /// </summary>
    public bool TryRemoveOrgan(EntityUid body, EntityUid organ)
    {
        if (!TryGetOrgan(body, organ, out var organComp)
            || organComp.Category == Brain
            || HasComp<WoundableComponent>(organ)
            || !_container.Remove(organ, Comp<BodyComponent>(body).Organs!, force: true))
            return false;

        if (HasComp<ChildOrganComponent>(organ))
            _relation.Orphan(organ);

        return true;
    }

    /// <summary>
    /// Puts a fresh organ from the body's manifest into an empty slot.
    /// </summary>
    /// <param name="parent">Body part the organ becomes a child of. Defaults to the part that houses the category.</param>
    public bool TryInsertOrgan(EntityUid body, ProtoId<OrganCategoryPrototype> category, EntityUid? parent = null)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp)
            || bodyComp.Organs is not { } organs
            || organs.ContainedEntities.Any(o => CompOrNull<OrganComponent>(o)?.Category == category)
            || !TryGetFreshOrgan(body, category, out var proto)
            || !TrySpawnInContainer(proto, body, BodyComponent.ContainerID, out var organ))
            return false;

        if (parent == null && _intake.TryGetHousingLimb(body, category, out var limb))
            parent = limb;

        if (parent != null && HasComp<ParentOrganComponent>(parent) && HasComp<ChildOrganComponent>(organ))
            _relation.Relate(parent.Value, organ.Value);

        return true;
    }

    /// <summary>
    /// Lists the body's internal organs: living, dead and the ones it is expected to have but lacks.
    /// </summary>
    public List<BodyDebugOrgan> GetOrgans(EntityUid body)
    {
        var rows = new List<BodyDebugOrgan>();
        if (!TryComp<BodyComponent>(body, out var bodyComp) || bodyComp.Organs is not { } organs)
            return rows;

        var present = new HashSet<ProtoId<OrganCategoryPrototype>>();
        foreach (var organ in organs.ContainedEntities)
        {
            if (!TryComp<OrganComponent>(organ, out var organComp) || organComp.Category is not { } category)
                continue;

            present.Add(category);

            if (HasComp<DeadOrganComponent>(organ))
            {
                rows.Add(new BodyDebugOrgan(GetNetEntity(organ), category, Name(organ), BodyDebugOrganKind.Dead,
                    FixedPoint2.Zero, FixedPoint2.Zero, FixedPoint2.Zero, FixedPoint2.Zero, OrganSeverity.Destroyed, new(), false));
                continue;
            }

            if (!TryComp<OrganIntegrityComponent>(organ, out var integrity))
                continue;

            rows.Add(new BodyDebugOrgan(GetNetEntity(organ), category, Name(organ), BodyDebugOrganKind.Alive,
                integrity.OrganIntegrity,
                integrity.IntegrityCap,
                integrity.IntegrityThresholds.GetValueOrDefault(OrganSeverity.Normal),
                integrity.IntegrityFloor,
                integrity.OrganSeverity,
                integrity.IntegrityModifiers.Select(modifier => (modifier.Key.Item1, modifier.Value)).ToList(),
                category == Brain));
        }

        foreach (var category in bodyComp.ExpectedOrgans)
        {
            if (present.Contains(category))
                continue;

            var name = TryGetFreshOrgan(body, category, out var proto) && _proto.TryIndex(proto, out var entity)
                ? entity.Name
                : category.Id;
            rows.Add(new BodyDebugOrgan(null, category, name, BodyDebugOrganKind.Missing,
                FixedPoint2.Zero, FixedPoint2.Zero, FixedPoint2.Zero, FixedPoint2.Zero, OrganSeverity.Destroyed, new(), false));
        }

        return rows;
    }

    /// <summary>
    /// Looks up the prototype the body was built with for a category.
    /// </summary>
    private bool TryGetFreshOrgan(EntityUid body, ProtoId<OrganCategoryPrototype> category, out EntProtoId proto)
    {
        proto = default;
        if (!TryComp<InitialBodyComponent>(body, out var manifest))
            return false;

        // Enumerated, because InitialBodyComponent only grants other systems read access to the dictionary.
        foreach (var (organCategory, organProto) in manifest.Organs)
        {
            if (organCategory != category)
                continue;

            proto = organProto;
            return true;
        }

        return false;
    }

    private bool TryGetOrgan(EntityUid body, EntityUid organ, out OrganComponent organComp)
    {
        organComp = default!;
        return TryComp<BodyComponent>(body, out var bodyComp)
               && bodyComp.Organs is { } organs
               && organs.Contains(organ)
               && TryComp(organ, out organComp!);
    }
}
