using Content.Server.Chat.Managers;
using Content.Shared._Polonium.Horror;
using Content.Shared._Polonium.Horror.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Alert;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Medical.HealthAnalyzer;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Horror;

/// <summary>
/// Server side of the eldritch horror: infesting, leaving, chemicals and the host control.
/// The infestation flow is ported from the cortical borer of Monolith Station (MPL-2.0).
/// </summary>
public sealed partial class EldritchHorrorSystem : SharedEldritchHorrorSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private HealthAnalyzerSystem _analyzer = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private BodySystem _body = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;

    private static readonly ProtoId<OrganCategoryPrototype> HeadCategory = "Head";
    private static readonly ProtoId<OrganCategoryPrototype> BrainCategory = "Brain";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EldritchHorrorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<EldritchHorrorComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<EldritchHorrorComponent, MindRemovedMessage>(OnMindRemoved);

        InitializeInfest();
        InitializeChemicals();
        InitializeControl();
        InitializeSouls();
        InitializeMutations();
        InitializeAbilities();
        InitializeSpeech();
    }

    private void OnMapInit(Entity<EldritchHorrorComponent> ent, ref MapInitEvent args)
    {
        foreach (var ability in ent.Comp.StartingAbilities)
            ent.Comp.OwnedAbilities.Add(ability);

        ent.Comp.NextRegen = _timing.CurTime + ent.Comp.RegenInterval;
        Dirty(ent);
        RefreshActions(ent);
        UpdateChemicalAlert(ent);
    }

    private void OnShutdown(Entity<EldritchHorrorComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Host is { } host && !TerminatingOrDeleted(host))
        {
            ReleaseControl(ent);
            RemCompDeferred<EldritchHorrorInfestedComponent>(host);
        }
    }

    private void OnMindRemoved(Entity<EldritchHorrorComponent> ent, ref MindRemovedMessage args)
    {
        // a horror without a player doesn't keep the host hostage; while controlling, the mind just sits in the host
        if (ent.Comp.Controlling
            || TerminatingOrDeleted(ent)
            || ent.Comp.Host is not { } host
            || TerminatingOrDeleted(host))
            return;

        TryLeaveHost(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<EldritchHorrorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextRegen)
                continue;

            comp.NextRegen = now + comp.RegenInterval;
            Tick((uid, comp));
        }

        UpdateCaptiveMinds(now);
        UpdateHitHosts();
    }

    /// <summary>
    /// Chemical regeneration and passive healing, every <see cref="EldritchHorrorComponent.RegenInterval"/>.
    /// </summary>
    private void Tick(Entity<EldritchHorrorComponent> ent)
    {
        var regen = 0;
        var heal = FixedPoint2.Zero;
        var withoutHost = false;
        foreach (var upgrade in GetUpgrades(ent))
        {
            regen += upgrade.ChemicalRegenBonus;
            heal += upgrade.Regeneration;
            withoutHost |= upgrade.RegenWithoutHost;
        }

        var dead = _mobState.IsDead(ent);
        if (ent.Comp.Host is { } host)
        {
            if (_mobState.IsDead(host))
            {
                regen = ent.Comp.DeadHostChemicalRegen;
            }
            else
            {
                regen += ent.Comp.ChemicalRegen;
                if (!dead)
                    heal += ent.Comp.HostHealing;
            }
        }
        else if (withoutHost)
        {
            regen += ent.Comp.ChemicalRegen;
        }
        else
        {
            regen = 0;
        }

        if (!dead && heal > 0)
            _damageable.HealEvenly(ent.Owner, -heal);

        var ev = new HorrorTickEvent(regen);
        RaiseLocalEvent(ent, ref ev);

        if (!dead && ev.Regen != 0)
            AddChemicals(ent, ev.Regen);
    }

    /// <summary>
    /// Adds (or with a negative amount removes) chemicals, clamped to the storage.
    /// </summary>
    public void AddChemicals(Entity<EldritchHorrorComponent> ent, int amount)
    {
        var old = ent.Comp.Chemicals;
        ent.Comp.Chemicals = Math.Clamp(old + amount, 0, ent.Comp.MaxChemicals);
        if (old == ent.Comp.Chemicals)
            return;

        Dirty(ent);
        UpdateChemicalAlert(ent);
        UpdateChemicalsUi(ent);
    }

    /// <summary>
    /// Spends chemicals if there are enough, otherwise tells the horror.
    /// </summary>
    public bool TrySpendChemicals(Entity<EldritchHorrorComponent> ent, int amount)
    {
        if (ent.Comp.Chemicals < amount)
        {
            _popup.PopupEntity(Loc.GetString("horror-not-enough-chemicals", ("amount", amount)), ent, ent, PopupType.SmallCaution);
            return false;
        }

        AddChemicals(ent, -amount);
        return true;
    }

    private void UpdateChemicalAlert(Entity<EldritchHorrorComponent> ent)
    {
        _alerts.ShowAlert(ent.Owner, ent.Comp.ChemicalAlert);
    }

    /// <summary>
    /// Grants the owned abilities usable in the horror's current state and takes away the rest.
    /// Abilities usable while controlling go to the host body, everything else stays on the horror.
    /// </summary>
    public void RefreshActions(Entity<EldritchHorrorComponent> ent)
    {
        if (TerminatingOrDeleted(ent))
            return;

        var state = GetState(ent.Comp);
        var performer = state == HorrorActionState.Controlling && ent.Comp.Host is { } host ? host : ent.Owner;

        foreach (var (proto, action) in new Dictionary<EntProtoId, EntityUid>(ent.Comp.ActionEntities))
        {
            var wanted = ent.Comp.OwnedAbilities.Contains(proto) && (GetActionStates(proto) & state) != 0;
            var attached = CompOrNull<ActionComponent>(action)?.AttachedEntity;
            if (wanted && attached == performer)
                continue;

            _actions.RemoveAction(action);
            ent.Comp.ActionEntities.Remove(proto);
        }

        foreach (var proto in ent.Comp.OwnedAbilities)
        {
            if (ent.Comp.ActionEntities.ContainsKey(proto) || (GetActionStates(proto) & state) == 0)
                continue;

            EntityUid? action = null;
            if (_actions.AddAction(performer, ref action, proto, ent.Owner) && action is { } added)
                ent.Comp.ActionEntities[proto] = added;
        }
    }

    private HorrorActionState GetActionStates(EntProtoId proto)
    {
        if (!Proto.TryIndex(proto, out var entProto)
            || !entProto.TryComp<HorrorActionComponent>(out var action, Factory))
            return HorrorActionState.None;

        return action.States;
    }

    /// <summary>
    /// Opens the health analyzer window on the host.
    /// </summary>
    private void OnScanHost(Entity<EldritchHorrorComponent> ent, ref HorrorScanHostActionEvent args)
    {
        if (args.Handled || !TryGetHost(ent, out var host))
            return;

        if (TryComp<HealthAnalyzerComponent>(ent, out var analyzer))
            _analyzer.StartScan((ent, analyzer), ent, host);

        args.Handled = true;
    }

    /// <summary>
    /// Whether the target's mind is the soul the horror is hunting.
    /// </summary>
    public bool IsSoulTarget(Entity<EldritchHorrorComponent> ent, EntityUid target)
    {
        return ent.Comp.SoulTarget is { } soul
               && _mind.TryGetMind(target, out var mind, out _)
               && mind == soul;
    }

    /// <summary>
    /// The host of the horror, with a popup when there is none.
    /// </summary>
    private bool TryGetHost(Entity<EldritchHorrorComponent> ent, out EntityUid host)
    {
        if (ent.Comp.Host is { } found)
        {
            host = found;
            return true;
        }

        host = default;
        _popup.PopupEntity(Loc.GetString("horror-no-host"), ent, ent, PopupType.SmallCaution);
        return false;
    }

}

/// <summary>
/// Raised on a horror every tick before it regenerates chemicals, so abilities can take their upkeep.
/// </summary>
[ByRefEvent]
public record struct HorrorTickEvent(int Regen);
