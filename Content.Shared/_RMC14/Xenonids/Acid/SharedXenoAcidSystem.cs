using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Acid;

public abstract partial class SharedXenoAcidSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] protected IPrototypeManager PrototypeManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private XenoPlasmaSystem _plasma = default!;

    private static readonly ProtoId<DamageTypePrototype> AcidDamageType = "Heat";
    private static readonly TimeSpan TickDelay = TimeSpan.FromSeconds(1);
    private static readonly SoundSpecifier AcidApplySound = new SoundCollectionSpecifier("XenoAcidSizzle");

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoAcidComponent, XenoCorrosiveAcidEvent>(OnCorrosiveAcid);
        SubscribeLocalEvent<XenoAcidComponent, XenoCorrosiveAcidDoAfterEvent>(OnCorrosiveAcidDoAfter);
    }

    private void OnCorrosiveAcid(Entity<XenoAcidComponent> xeno, ref XenoCorrosiveAcidEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;

        if (!TryComp(target, out CorrodibleComponent? corrodible) || !corrodible.IsCorrodible)
        {
            if (!HasComp<DamageableComponent>(target) || !Transform(target).Anchored)
            {
                _popup.PopupClient(Loc.GetString("cm-xeno-acid-not-corrodible", ("target", target)), xeno, xeno);
                return;
            }
        }
        else if (!xeno.Comp.CanMeltStructures && corrodible.Structure)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-acid-not-corrodible", ("target", target)), xeno, xeno);
            return;
        }
        else if (args.Strength < corrodible.MinimumAcidStrength)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-acid-not-corrodible", ("target", target)), xeno, xeno);
            return;
        }

        if (HasComp<DamageableCorrodingComponent>(target))
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-acid-already-corroding", ("target", target)), xeno, xeno);
            return;
        }

        args.Handled = true;

        var delay = (corrodible?.TimeToApply ?? TimeSpan.FromSeconds(4)) * args.ApplyTimeMultiplier;
        var ev = new XenoCorrosiveAcidDoAfterEvent(args);
        var doAfter = new DoAfterArgs(EntityManager, xeno, delay, ev, xeno, target)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnCorrosiveAcidDoAfter(Entity<XenoAcidComponent> xeno, ref XenoCorrosiveAcidDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        if (args.PlasmaCost != 0 && !_plasma.TryRemovePlasmaPopup(xeno.Owner, args.PlasmaCost))
            return;

        args.Handled = true;
        _audio.PlayPredicted(AcidApplySound, target, xeno);

        if (_net.IsClient)
            return;

        EntityUid? acid = null;
        if (PrototypeManager.HasIndex(args.AcidId))
            acid = SpawnAttachedTo(args.AcidId, Transform(target).Coordinates);

        var damageType = PrototypeManager.Index(AcidDamageType);
        var corroding = EnsureComp<DamageableCorrodingComponent>(target);
        corroding.AcidPrototype = args.AcidId;
        corroding.Acid = acid;
        corroding.Dps = args.Dps;
        corroding.Strength = args.Strength;
        corroding.Damage = new DamageSpecifier(damageType, args.Dps * (float)TickDelay.TotalSeconds);
        corroding.NextDamageAt = _timing.CurTime + TickDelay;
        corroding.CorrodesAt = _timing.CurTime + args.Time;
        Dirty(target, corroding);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<DamageableCorrodingComponent>();
        while (query.MoveNext(out var uid, out var corroding))
        {
            if (time >= corroding.CorrodesAt)
            {
                if (corroding.Acid is { } acid && !TerminatingOrDeleted(acid))
                    QueueDel(acid);

                QueueDel(uid);
                continue;
            }

            if (time < corroding.NextDamageAt)
                continue;

            corroding.NextDamageAt = time + TickDelay;
            Dirty(uid, corroding);
            _damageable.TryChangeDamage(uid, corroding.Damage, ignoreResistances: true);
        }
    }
}
