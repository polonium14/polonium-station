using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Shared._Polonium.Xenonids;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared.Bed.Components;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.Body;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Xenonids;

/// <summary>
/// Embrion z NSV13: pięć etapów po 30-45 s, objawy, a na końcu larwa wyrywa się z klatki piersiowej.
/// Embrion jest organem, więc można go wyciąć operacją.
/// </summary>
public sealed partial class XenoEmbryoSystem : EntitySystem
{
    private static readonly EntProtoId EmbryoPrototype = "XenoEmbryo";
    private static readonly ProtoId<OrganCategoryPrototype> TorsoCategory = "Torso";
    private static readonly SoundSpecifier BurstSound = new SoundCollectionSpecifier("XenoChestburst");

    private static readonly DamageSpecifier BurstDamage = new()
    {
        DamageDict = { ["Piercing"] = 100, ["Slash"] = 100 },
    };

    private static readonly DamageSpecifier SurgeryBurstDamage = new()
    {
        DamageDict = { ["Slash"] = 40 },
    };

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoEmbryoComponent, OrganGotInsertedEvent>(OnInserted);
        SubscribeLocalEvent<XenoEmbryoComponent, OrganGotRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<XenoInfectedComponent, ComponentShutdown>(OnInfectedShutdown);
    }

    private void OnInserted(Entity<XenoEmbryoComponent> ent, ref OrganGotInsertedEvent args)
    {
        var infected = EnsureComp<XenoInfectedComponent>(args.Target);
        infected.Embryo = ent;
        infected.Stage = ent.Comp.Stage;
        Dirty(args.Target, infected);
    }

    private void OnRemoved(Entity<XenoEmbryoComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (TryComp<XenoInfectedComponent>(args.Target, out var infected) && infected.Embryo == ent.Owner)
            RemComp<XenoInfectedComponent>(args.Target);

        // Poza ciałem embrion obumiera: zostaje zwykły organ, który nikogo już nie zarazi po wszczepieniu.
        RemCompDeferred<XenoEmbryoComponent>(ent);
    }

    private void OnInfectedShutdown(Entity<XenoInfectedComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.Embryo = null;
    }

    /// <summary>
    /// Wszczepia embrion. Zwraca false, jeśli nosiciel nie ma ciała albo jest już zarażony.
    /// </summary>
    public bool TryInfect(EntityUid target)
    {
        if (HasComp<XenoInfectedComponent>(target) || !TryComp<BodyComponent>(target, out var body) || body.Organs is not { } organs)
            return false;

        var embryo = Spawn(EmbryoPrototype, Transform(target).Coordinates);
        if (_container.Insert(embryo, organs, force: true))
            return true;

        Del(embryo);
        return false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<XenoEmbryoComponent, OrganComponent>();
        while (query.MoveNext(out var uid, out var embryo, out var organ))
        {
            if (organ.Body is not { } host || time < embryo.NextTick)
                continue;

            embryo.NextTick = time + embryo.TickDelay;
            var ent = new Entity<XenoEmbryoComponent>(uid, embryo);

            // Staza i krio zatrzymują rozwój, śmierć nosiciela już nie.
            if (IsInStasis(host) || HasComp<CryostorageContainedComponent>(host))
                continue;

            if (embryo.NextStageAt == null)
            {
                embryo.NextStageAt = time + embryo.FirstStageTime;
                continue;
            }

            if (time >= embryo.NextStageAt && embryo.Stage < embryo.MaxStage)
            {
                embryo.Stage++;
                embryo.NextStageAt = time + RandomStageTime(embryo);

                if (TryComp<XenoInfectedComponent>(host, out var infected))
                {
                    infected.Stage = embryo.Stage;
                    Dirty(host, infected);
                }
            }

            DoSymptoms(ent, host);

            if (embryo.Stage >= embryo.MaxStage && _random.Prob(embryo.BurstChance))
                Burst(ent, host);
        }
    }

    private TimeSpan RandomStageTime(XenoEmbryoComponent embryo)
    {
        var extra = TimeSpan.Zero;
        var xenos = EntityQueryEnumerator<XenoComponent, ActorComponent>();
        while (xenos.MoveNext(out var xeno, out var xenoComp, out _))
        {
            if (xenoComp.Tier <= 0 || _mobState.IsDead(xeno))
                continue;

            extra += embryo.PerXenoStageTime;
        }

        if (extra > embryo.MaxPerXenoStageTime)
            extra = embryo.MaxPerXenoStageTime;

        var seconds = _random.NextDouble(embryo.MinStageTime.TotalSeconds, embryo.MaxStageTime.TotalSeconds);
        return TimeSpan.FromSeconds(seconds) + extra;
    }

    /// <summary>
    /// Objawy z NSV13. Tam Life tyka co 2 s, tu też co 2 s, więc szanse są te same.
    /// </summary>
    private void DoSymptoms(Entity<XenoEmbryoComponent> embryo, EntityUid host)
    {
        if (_mobState.IsDead(host))
            return;

        switch (embryo.Comp.Stage)
        {
            case 2:
            case 3:
                CommonSymptoms(host);
                if (_random.Prob(0.02f))
                    _popup.PopupEntity(Loc.GetString("xeno-embryo-symptom-throat"), host, host, PopupType.SmallCaution);
                if (_random.Prob(0.02f))
                    _popup.PopupEntity(Loc.GetString("xeno-embryo-symptom-mucus"), host, host, PopupType.SmallCaution);
                break;
            case 4:
                CommonSymptoms(host);
                if (_random.Prob(0.04f))
                {
                    _popup.PopupEntity(Loc.GetString("xeno-embryo-symptom-muscles"), host, host, PopupType.SmallCaution);
                    if (_random.Prob(0.2f))
                        _damageable.TryChangeDamage(host, new DamageSpecifier { DamageDict = { ["Blunt"] = 1 } });
                }
                if (_random.Prob(0.04f))
                {
                    _popup.PopupEntity(Loc.GetString("xeno-embryo-symptom-stomach"), host, host, PopupType.SmallCaution);
                    if (_random.Prob(0.2f))
                        _damageable.TryChangeDamage(host, new DamageSpecifier { DamageDict = { ["Poison"] = 1 } });
                }
                break;
            case >= 5:
                _popup.PopupEntity(Loc.GetString("xeno-embryo-symptom-tearing"), host, host, PopupType.LargeCaution);
                _damageable.TryChangeDamage(host, new DamageSpecifier { DamageDict = { ["Poison"] = 10 } }, ignoreResistances: true);
                break;
        }
    }

    private void CommonSymptoms(EntityUid host)
    {
        if (_random.Prob(0.02f))
            _chat.TryEmoteWithChat(host, "Sneeze", ignoreActionBlocker: true);
        if (_random.Prob(0.02f))
            _chat.TryEmoteWithChat(host, "Cough", ignoreActionBlocker: true);
    }

    /// <summary>
    /// Wyklucie. Larwa jest rolą ducha, więc wychodzi od razu i czeka na gracza.
    /// Jeśli klatka piersiowa jest właśnie otwarta na stole, larwa wypełza bez zabijania nosiciela.
    /// Nosiciel w kostnicy, worku albo szafce: larwa zostaje w środku razem z nim.
    /// </summary>
    public void Burst(Entity<XenoEmbryoComponent> embryo, EntityUid host)
    {
        var larva = _container.TryGetContainingContainer(host, out var container)
            ? SpawnInContainerOrDrop(embryo.Comp.LarvaPrototype, container.Owner, container.ID)
            : Spawn(embryo.Comp.LarvaPrototype, _transform.GetMoverCoordinates(host));
        _audio.PlayPvs(BurstSound, host);

        if (IsChestOpen(host))
        {
            _popup.PopupEntity(Loc.GetString("xeno-embryo-wriggle-out", ("larva", larva), ("host", host)), host, PopupType.LargeCaution);
            _damageable.TryChangeDamage(host, SurgeryBurstDamage, ignoreResistances: true);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("xeno-embryo-burst", ("larva", larva), ("host", host)), host, PopupType.LargeCaution);
            _damageable.TryChangeDamage(host, BurstDamage, ignoreResistances: true);
        }

        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(larva):entity} burst out of {ToPrettyString(host):player}");

        RemComp<XenoInfectedComponent>(host);
        QueueDel(embryo);
    }

    private bool IsInStasis(EntityUid host)
    {
        return TryComp<BuckleComponent>(host, out var buckle)
            && buckle.BuckledTo is { } bed
            && HasComp<StasisBedComponent>(bed)
            && _powerReceiver.IsPowered(bed);
    }

    private bool IsChestOpen(EntityUid host)
    {
        return TryComp<BodyComponent>(host, out var body)
            && LimbTargetMap.TryGetOrganByCategory(EntityManager, body, TorsoCategory, out var torso)
            && HasComp<IncisionOpenComponent>(torso);
    }
}
