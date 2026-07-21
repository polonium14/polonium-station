using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared._Shitmed.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Effects.Step;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Shitmed.Medical.Surgery;

public sealed partial class SurgerySystem : SharedSurgerySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private readonly Dictionary<NetEntity, List<EntProtoId>> _surgeries = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryTargetComponent, SurgeryStepDamageEvent>(OnSurgeryStepDamage);
        // You might be wondering "why aren't we using StepEvent for these two?" reason being that StepEvent fires off regardless of success on the previous functions
        // so this would heal entities even if you had a used or incorrect organ.
        SubscribeLocalEvent<SurgeryDamageChangeEffectComponent, SurgeryStepDamageChangeEvent>(OnSurgeryDamageChange);
        SubscribeLocalEvent<SurgerySpecialDamageChangeEffectComponent, SurgeryStepDamageChangeEvent>(OnUnimplementedSpecialDamageChange);
        SubscribeLocalEvent<SurgeryStepEmoteEffectComponent, SurgeryStepEvent>(OnStepScreamComplete);
        SubscribeLocalEvent<SurgeryStepSpawnEffectComponent, SurgeryStepEvent>(OnStepSpawnComplete);
    }

    protected override void RefreshUI(EntityUid body)
    {
        _surgeries.Clear();

        if (TryComp<BodyComponent>(body, out var bodyComp) && bodyComp.Organs is not null)
        {
            foreach (var part in bodyComp.Organs.ContainedEntities)
            {
                TryComp<OrganComponent>(part, out var organ);

                if (organ?.Category is not { } category || !LimbTargetMap.TryGetTarget(category, out _))
                    continue;

                var valid = new List<EntProtoId>();
                foreach (var surgery in AllSurgeries)
                {
                    if (GetSingleton(surgery) is not { } surgeryEnt)
                        continue;

                    var ev = new SurgeryValidEvent(body, part, Category: organ?.Category);
                    RaiseLocalEvent(surgeryEnt, ref ev);

                    if (ev.Cancelled)
                        continue;

                    valid.Add(surgery);
                }
                _surgeries[GetNetEntity(part)] = valid;
            }
        }

        _ui.SetUiState(body, SurgeryUIKey.Key, new SurgeryBuiState(_surgeries));
        /*
            Reason we do this is because when applying a BUI State, it rolls back the state on the entity temporarily,
            which just so happens to occur right as we're checking for step completion, so we end up with the UI
            not updating at all until you change tools or reopen the window. I love shitcode.
        */
        _ui.ServerSendUiMessage(body, SurgeryUIKey.Key, new SurgeryBuiRefreshMessage());
    }

    private DamageGroupPrototype? GetDamageGroupByType(string id)
    {
        return (from @group in _prototypes.EnumeratePrototypes<DamageGroupPrototype>() where @group.DamageTypes.Contains(id) select @group).FirstOrDefault();
    }

    private void SetDamage(EntityUid body,
        DamageSpecifier damage,
        float partMultiplier,
        EntityUid user,
        EntityUid part,
        bool affectAll = false)
    {
        if (!HasComp<OrganComponent>(part))
            return;

        if (HasComp<WoundableComponent>(part))
            _wounds.TryHaltAllBleeding(part, force: true);

        var scaled = damage * partMultiplier;

        if (!affectAll)
        {
            _damageable.TryChangeDamage(part, scaled, ignoreResistances: true, interruptsDoAfters: false, origin: user);
        }
        else
        {
            if (!TryComp<BodyComponent>(body, out var bodyComp) || bodyComp.Organs is null)
                return;

            foreach (var organ in bodyComp.Organs.ContainedEntities.ToList())
            {
                _damageable.TryChangeDamage(organ, scaled, ignoreResistances: true, interruptsDoAfters: false, origin: user);
            }
        }
    }

    private void OnSurgeryStepDamage(Entity<SurgeryTargetComponent> ent, ref SurgeryStepDamageEvent args) =>
        SetDamage(args.Body, args.Damage, args.PartMultiplier, args.User, args.Part);

    private void OnSurgeryDamageChange(Entity<SurgeryDamageChangeEffectComponent> ent, ref SurgeryStepDamageChangeEvent args)
    {
        var damageChange = ent.Comp.Damage;
        if (Status.HasEffectComp<ForcedSleepingStatusEffectComponent>(args.Body))
            damageChange = damageChange * ent.Comp.SleepModifier;

        SetDamage(args.Body, damageChange, 0.5f, args.User, args.Part, ent.Comp.AffectAll);
    }

    private void OnUnimplementedSpecialDamageChange(Entity<SurgerySpecialDamageChangeEffectComponent> ent, ref SurgeryStepDamageChangeEvent args)
    {
        Log.Error($"Surgery step {ToPrettyString(args.Step)} references SurgerySpecialDamageChangeEffectComponent (damageType: {ent.Comp.DamageType}), which has no implementation in this fork (or upstream Goob-Station) and does nothing.");
    }

    private void OnStepScreamComplete(Entity<SurgeryStepEmoteEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (Status.HasEffectComp<ForcedSleepingStatusEffectComponent>(args.Body))
            return;

        _chat.TryEmoteWithChat(args.Body, ent.Comp.Emote);
    }

    private void OnStepSpawnComplete(Entity<SurgeryStepSpawnEffectComponent> ent, ref SurgeryStepEvent args) =>
        SpawnAtPosition(ent.Comp.Entity, Transform(args.Body).Coordinates);
}
