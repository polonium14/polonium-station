using Content.Server.Body.Systems;
using Content.Shared._Polonium.Body;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Body;

/// <summary>
/// Everyday wear on the organs that no reagent covers: a long thirst strains the kidneys,
/// breathing hot gas burns the lungs.
/// </summary>
public sealed partial class OrganStrainSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private OrganHealingSystem _healing = default!;
    [Dependency] private SatiationSystem _satiation = default!;

    private static readonly HashSet<ProtoId<OrganCategoryPrototype>> Kidneys = [PairedOrgans.Kidneys];
    private static readonly HashSet<ProtoId<OrganCategoryPrototype>> Lungs = [PairedOrgans.Lungs];
    private static readonly SatiationValue Parched = "Parched";

    private static readonly TimeSpan ThirstInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Integrity each kidney loses per minute spent parched: about 90 minutes to sick kidneys.
    /// </summary>
    private static readonly FixedPoint2 ThirstDamage = FixedPoint2.New(0.3);

    /// <summary>
    /// Inhaled gas hotter than this burns the lungs. A plasma fire is far above it, a warm room far below.
    /// </summary>
    private const float HotGasTemperature = 400f;

    /// <summary>
    /// Integrity each lung loses per breath of hot gas.
    /// </summary>
    private static readonly FixedPoint2 HotGasDamage = FixedPoint2.New(0.5);

    /// <summary>
    /// A breath with less gas than this, near vacuum, carries too little heat to burn.
    /// </summary>
    private const float MinBreathMoles = 0.01f;

    private TimeSpan _nextThirstCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidProfileComponent, InhaledGasEvent>(OnInhaled);
    }

    private void OnInhaled(Entity<HumanoidProfileComponent> ent, ref InhaledGasEvent args)
    {
        if (args.Gas.Temperature <= HotGasTemperature || args.Gas.TotalMoles < MinBreathMoles)
            return;

        Strain(ent, Lungs, HotGasDamage);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextThirstCheck)
            return;

        _nextThirstCheck = _timing.CurTime + ThirstInterval;

        var query = EntityQueryEnumerator<HumanoidProfileComponent, SatiationComponent>();
        while (query.MoveNext(out var uid, out _, out var satiation))
        {
            if (_mobState.IsDead(uid)
                || !_satiation.IsValueInRange((uid, satiation), SatiationSystem.Thirst, below: Parched))
                continue;

            Strain(uid, Kidneys, ThirstDamage);
        }
    }

    private void Strain(EntityUid body, HashSet<ProtoId<OrganCategoryPrototype>> categories, FixedPoint2 amount)
    {
        foreach (var organ in _healing.GetLivingOrgans(body, categories, []))
        {
            _healing.DamageOrgan(organ, amount);
        }
    }
}
