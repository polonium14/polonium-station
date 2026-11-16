using Content.Shared._Polonium.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Content.Shared.Body;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Body;

/// <summary>
/// What the body's senses still do, kept on it to undo only what this system changed.
/// </summary>
[RegisterComponent]
[Access(typeof(SensesSystem))]
public sealed partial class SensesComponent : Component
{
    /// <summary>
    /// The eye damage floor this system set for blurry vision.
    /// </summary>
    [DataField]
    public int EyeFloor;
}

/// <summary>
/// Damaged eyes, tongue and ears of a humanoid weaken their sense step by step, in proportion to the organ's
/// integrity times the brain's. Blurry sight and bad hearing start below <see cref="SickBelow"/>;
/// the tongue lisps from the first damage. Dead or missing eyes and tongue blind and mute as before,
/// dead or missing ears deafen.
/// </summary>
public sealed partial class SensesSystem : EntitySystem
{
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private PolishLispSystem _lisp = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private float _lobotomyBrainEfficiency;

    private static readonly ProtoId<OrganCategoryPrototype> Brain = "Brain";
    private static readonly ProtoId<OrganCategoryPrototype> Eyes = "Eyes";
    private static readonly ProtoId<OrganCategoryPrototype> Ears = "Ears";
    private static readonly ProtoId<OrganCategoryPrototype> Tongue = "Tongue";

    /// <summary>
    /// Efficiency at which sight and hearing start to fail, the same line as a sick organ.
    /// </summary>
    public const float SickBelow = 0.35f;

    /// <summary>
    /// Bodies to refresh next tick. Deferred, because organ insertion restores sight only after the body hears of it.
    /// </summary>
    private readonly HashSet<EntityUid> _dirty = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganIntegrityComponent, OrganIntegrityChangedEvent>(OnIntegrityChanged);
        SubscribeLocalEvent<LobotomizedComponent, ComponentStartup>(OnLobotomyChanged);
        SubscribeLocalEvent<LobotomizedComponent, ComponentShutdown>(OnLobotomyChanged);
        Subs.CVar(_cfg, CCVars.OrgansLobotomyBrainEfficiency, value => _lobotomyBrainEfficiency = value, true);
    }

    /// <summary>
    /// The lobotomy surgery marks the head, the mending surgery takes the mark off; either way the senses change.
    /// </summary>
    private void OnLobotomyChanged<T>(Entity<LobotomizedComponent> ent, ref T args)
    {
        if (CompOrNull<OrganComponent>(ent)?.Body is { } body)
            MarkDirty(body);
    }

    private void OnIntegrityChanged(Entity<OrganIntegrityComponent> ent, ref OrganIntegrityChangedEvent args)
    {
        if (CompOrNull<OrganComponent>(ent)?.Body is { } body)
            MarkDirty(body);
    }

    /// <summary>
    /// Refreshes the body's senses next tick.
    /// </summary>
    public void MarkDirty(EntityUid body)
    {
        _dirty.Add(body);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var body in _dirty)
        {
            Refresh(body);
        }

        _dirty.Clear();
    }

    /// <summary>
    /// Efficiency of one sense organ: its integrity times the brain's, 0 if it is dead or missing,
    /// null if the body never had one.
    /// </summary>
    public float? GetEfficiency(EntityUid body, ProtoId<OrganCategoryPrototype> category)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp) || bodyComp.Organs is not { } organs)
            return null;

        var expected = false;
        foreach (var expectedCategory in bodyComp.ExpectedOrgans)
        {
            expected |= expectedCategory == category;
        }

        if (!expected)
            return null;

        var brain = 0f;
        var organ = 0f;
        var lobotomized = false;
        foreach (var uid in organs.ContainedEntities)
        {
            lobotomized |= HasComp<LobotomizedComponent>(uid);
            var organCategory = CompOrNull<OrganComponent>(uid)?.Category;
            if (organCategory != category && organCategory != Brain || !TryComp<OrganIntegrityComponent>(uid, out var integrity))
                continue;

            // In floats: FixedPoint2 division keeps two decimals and would round a sliver of integrity to nothing.
            var fraction = integrity.OrganIntegrity.Float() / integrity.IntegrityCap.Float();
            if (organCategory == Brain)
                brain = fraction;
            else
                organ = fraction;
        }

        if (lobotomized)
            brain = MathF.Min(brain, _lobotomyBrainEfficiency);

        return organ * brain;
    }

    private void Refresh(EntityUid body)
    {
        if (TerminatingOrDeleted(body) || !HasComp<HumanoidProfileComponent>(body))
            return;

        var senses = EnsureComp<SensesComponent>(body);
        RefreshSight((body, senses), GetEfficiency(body, Eyes));
        RefreshSpeech(body, GetEfficiency(body, Tongue));
        RefreshHearing(body, GetEfficiency(body, Ears));
    }

    private void RefreshSight(Entity<SensesComponent> body, float? efficiency)
    {
        if (!TryComp<BlindableComponent>(body, out var blindable))
            return;

        // Dead or missing eyes blind the body through OrganFunctionSystem; leave them be.
        if (efficiency is not > 0f)
        {
            body.Comp.EyeFloor = 0;
            return;
        }

        // Below the line the blur grows until it is as strong as it gets short of blindness.
        var floor = efficiency.Value >= SickBelow
            ? 0
            : (int) MathF.Round((1f - efficiency.Value / SickBelow) * (blindable.MaxDamage - 1));

        if (floor == body.Comp.EyeFloor)
            return;

        var lowered = body.Comp.EyeFloor - floor;
        body.Comp.EyeFloor = floor;
        _blindable.SetMinDamage((body, blindable), floor);

        // Lowering the floor does not lower the damage already sitting on it.
        if (lowered > 0)
            _blindable.AdjustEyeDamage((body, blindable), -lowered);
    }

    private void RefreshSpeech(EntityUid body, float? efficiency)
    {
        // A dead or missing tongue mutes the body through OrganFunctionSystem.
        _lisp.SetChance(body, efficiency is > 0f ? 1f - efficiency.Value : 0f);
    }

    private void RefreshHearing(EntityUid body, float? efficiency)
    {
        var deaf = efficiency is <= 0f;
        var garble = efficiency is > 0f and < SickBelow ? 1f - efficiency.Value / SickBelow : 0f;

        if (!deaf && garble <= 0f)
        {
            RemComp<HearingLossComponent>(body);
            return;
        }

        var hearing = EnsureComp<HearingLossComponent>(body);
        hearing.Deaf = deaf;
        hearing.Garble = garble;
    }
}
