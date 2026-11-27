using System.Linq;
using Content.Server.Electrocution;
using Content.Shared._Polonium.Horror;
using Content.Shared._Polonium.Horror.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Doors.Components;
using Content.Shared.Emoting;
using Content.Shared.DoAfter;
using Content.Shared.Fluids;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Objectives.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Tools.Systems;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Polonium.Horror;

public sealed partial class EldritchHorrorSystem
{
    [Dependency] private ElectrocutionSystem _electrocution = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPuddleSystem _puddle = default!;
    [Dependency] private SharedStealthSystem _stealth = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private WeldableSystem _weldable = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedObjectivesSystem _objectives = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    private static readonly SoundSpecifier SplortSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");
    private static readonly SoundSpecifier TentacleSound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/blobattack.ogg");
    private static readonly SoundSpecifier ReviveSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_zap.ogg");
    private static readonly SoundSpecifier TripSound = new SoundPathSpecifier("/Audio/Weapons/punchmiss.ogg");
    /// <summary>
    /// Tiles around a closed airlock the horror can come out on.
    /// </summary>
    private const float SqueezeExitRange = 1.5f;

    private void InitializeAbilities()
    {
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorHideActionEvent>(OnHide);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorChameleonActionEvent>(OnChameleon);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorTickEvent>(OnTickUpkeep);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorRevealEvent>(OnReveal);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorTripActionEvent>(OnTrip);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorLubeSpillActionEvent>(OnLubeSpill);
        SubscribeLocalEvent<EldritchHorrorComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<EldritchHorrorComponent, EmoteAttemptEvent>(OnHorrorEmoteAttempt);
        SubscribeLocalEvent<HorrorCaptiveMindComponent, EmoteAttemptEvent>(OnCaptiveEmoteAttempt);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorSqueezeDoAfterEvent>(OnSqueezeDoAfter);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorTentacleActionEvent>(OnTentacle);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorLeftHostEvent>(OnLeftHost);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorTransferHostActionEvent>(OnTransfer);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorTransferDoAfterEvent>(OnTransferDoAfter);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorJumpstartActionEvent>(OnJumpstart);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorViewMemoryActionEvent>(OnViewMemory);
    }

    private object IdentityOf(EntityUid uid)
    {
        return Content.Shared.IdentityManagement.Identity.Entity(uid, EntityManager);
    }

    #region Hiding and chameleon skin

    private void OnHide(Entity<EldritchHorrorComponent> ent, ref HorrorHideActionEvent args)
    {
        if (args.Handled || ent.Comp.Host != null)
            return;

        SetHiding(ent, !ent.Comp.Hiding);
        args.Handled = true;
    }

    private void SetHiding(Entity<EldritchHorrorComponent> ent, bool hiding)
    {
        if (ent.Comp.Hiding == hiding)
            return;

        ent.Comp.Hiding = hiding;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString(hiding ? "horror-hide-on" : "horror-hide-off", ("horror", ent.Owner)), ent);
    }

    private void OnChameleon(Entity<EldritchHorrorComponent> ent, ref HorrorChameleonActionEvent args)
    {
        if (args.Handled || ent.Comp.Host != null)
            return;

        if (!ent.Comp.Invisible && ent.Comp.Chemicals < ent.Comp.InvisibilityStartCost)
        {
            _popup.PopupEntity(Loc.GetString("horror-not-enough-chemicals", ("amount", ent.Comp.InvisibilityStartCost)), ent, ent, PopupType.SmallCaution);
            return;
        }

        SetInvisible(ent, !ent.Comp.Invisible);
        args.Handled = true;
    }

    private void SetInvisible(Entity<EldritchHorrorComponent> ent, bool invisible)
    {
        if (ent.Comp.Invisible == invisible)
            return;

        ent.Comp.Invisible = invisible;
        Dirty(ent);

        var stealth = EnsureComp<StealthComponent>(ent);
        _stealth.SetEnabled(ent, invisible, stealth);
        if (invisible)
            _stealth.SetVisibility(ent, 1f, stealth);

        _popup.PopupEntity(Loc.GetString(invisible ? "horror-chameleon-on" : "horror-chameleon-off"), ent, ent);
    }

    /// <summary>
    /// The chameleon skin fades the horror out and burns chemicals instead of regenerating them.
    /// </summary>
    private void OnTickUpkeep(Entity<EldritchHorrorComponent> ent, ref HorrorTickEvent args)
    {
        UpdateSoulLocator(ent);

        if (!ent.Comp.Invisible)
            return;

        args.Regen = 0;
        if (ent.Comp.Chemicals < ent.Comp.InvisibilityUpkeep)
        {
            SetInvisible(ent, false);
            _popup.PopupEntity(Loc.GetString("horror-chameleon-out"), ent, ent, PopupType.SmallCaution);
            return;
        }

        AddChemicals(ent, -ent.Comp.InvisibilityUpkeep);
        _stealth.ModifyVisibility(ent, -ent.Comp.InvisibilityFade);
    }

    private void OnReveal(Entity<EldritchHorrorComponent> ent, ref HorrorRevealEvent args)
    {
        SetHiding(ent, false);
        SetInvisible(ent, false);
    }

    #endregion

    /// <summary>
    /// A horror in a head can't move a muscle that anyone would see, only the body it drives emotes.
    /// </summary>
    private void OnHorrorEmoteAttempt(Entity<EldritchHorrorComponent> ent, ref EmoteAttemptEvent args)
    {
        if (ent.Comp.Host != null && !ent.Comp.Controlling)
            args.Cancel();
    }

    /// <summary>
    /// The trapped mind has no body to emote with.
    /// </summary>
    private void OnCaptiveEmoteAttempt(Entity<HorrorCaptiveMindComponent> ent, ref EmoteAttemptEvent args)
    {
        args.Cancel();
    }

    #region Trip, lube and airlocks

    private void OnTrip(Entity<EldritchHorrorComponent> ent, ref HorrorTripActionEvent args)
    {
        if (args.Handled || ent.Comp.Host != null)
            return;

        var paralyze = GetUpgrades(ent).Any(u => u.ParalyzingTrip);
        var victims = new List<EntityUid>();
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(ent).Coordinates, ent.Comp.AbilityRange))
        {
            if (mob.Owner == ent.Owner || mob.Comp.CurrentState != MobState.Alive || !_interaction.InRangeUnobstructed(ent.Owner, mob.Owner, ent.Comp.AbilityRange))
                continue;

            victims.Add(mob);
        }

        if (victims.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("horror-trip-nobody"), ent, ent, PopupType.SmallCaution);
            return;
        }

        var reveal = new HorrorRevealEvent();
        RaiseLocalEvent(ent, ref reveal);

        foreach (var victim in victims)
        {
            if (paralyze)
            {
                _stun.TryUpdateParalyzeDuration(victim, ent.Comp.ParalyzeTime);
                _status.TryAddStatusEffectDuration(victim, SleepingSystem.StatusEffectForcedSleeping, ent.Comp.ParalyzeTime);
                _electrocution.TryDoElectrocution(victim, ent, ent.Comp.ParalyzeShockDamage, TimeSpan.FromSeconds(1), true, ignoreInsulation: true);
            }
            else
            {
                _stun.TryKnockdown(victim, ent.Comp.TripKnockdown, true, force: true);
                _stun.TryAddStunDuration(victim, ent.Comp.TripImmobilize);
            }

            _popup.PopupEntity(Loc.GetString("horror-trip-victim"), victim, victim, PopupType.MediumCaution);
            _adminLog.Add(LogType.Stamina, LogImpact.Medium, $"{ToPrettyString(ent):horror} tripped {ToPrettyString(victim):target}");
        }

        _audio.PlayPvs(TripSound, ent);
        args.Handled = true;
    }

    private void OnLubeSpill(Entity<EldritchHorrorComponent> ent, ref HorrorLubeSpillActionEvent args)
    {
        if (args.Handled || ent.Comp.Host != null || !TrySpendChemicals(ent, ent.Comp.LubeCost))
            return;

        var origin = Transform(ent).Coordinates;
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (!_random.Prob(ent.Comp.LubeChance))
                    continue;

                var coords = origin.Offset(new System.Numerics.Vector2(x, y));
                if (!_interaction.InRangeUnobstructed(ent.Owner, coords, ent.Comp.AbilityRange))
                    continue;

                var solution = new Solution();
                solution.AddReagent(ent.Comp.LubeReagent, ent.Comp.LubeAmount);
                _puddle.TrySpillAt(coords, solution, out _, sound: false);
            }
        }

        _audio.PlayPvs(TentacleSound, ent);
        _popup.PopupEntity(Loc.GetString("horror-lube-spill", ("horror", ent.Owner)), ent, PopupType.MediumCaution);
        args.Handled = true;
    }

    /// <summary>
    /// Biting a closed airlock squeezes the horror through it instead.
    /// Only the horror's own bite counts (the attack check of a real melee swing passes the horror as the weapon),
    /// so other systems asking whether it could attack the door don't start anything.
    /// </summary>
    private void OnAttackAttempt(Entity<EldritchHorrorComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Cancelled
            || args.Weapon?.Owner != ent.Owner
            || args.Target is not { } door
            || !TryComp<DoorComponent>(door, out var doorComp)
            || doorComp.State != DoorState.Closed)
            return;

        args.Cancel();
        if (ent.Comp.Host != null)
            return;

        if (_weldable.IsWelded(door))
        {
            _popup.PopupEntity(Loc.GetString("horror-squeeze-welded"), ent, ent, PopupType.SmallCaution);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.SqueezeTime, new HorrorSqueezeDoAfterEvent(), ent, door)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 1.5f,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _audio.PlayPvs(SplortSound, door);
        _popup.PopupEntity(Loc.GetString("horror-squeeze-start", ("horror", ent.Owner)), ent, PopupType.MediumCaution);
    }

    private void OnSqueezeDoAfter(Entity<EldritchHorrorComponent> ent, ref HorrorSqueezeDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } door)
            return;

        if (_weldable.IsWelded(door))
        {
            _popup.PopupEntity(Loc.GetString("horror-squeeze-welded"), ent, ent, PopupType.SmallCaution);
            return;
        }

        // come out on the far side, the closed door itself is solid
        var doorCoords = Transform(door).Coordinates;
        // in the door's own frame, so a rotated grid still steps along its tiles
        var from = _xform.WithEntityId(Transform(ent).Coordinates, doorCoords.EntityId);
        var away = doorCoords.Position - from.Position;
        var step = MathF.Abs(away.X) >= MathF.Abs(away.Y)
            ? new System.Numerics.Vector2(MathF.Sign(away.X), 0)
            : new System.Numerics.Vector2(0, MathF.Sign(away.Y));
        var exit = doorCoords.Offset(step);
        if (step == System.Numerics.Vector2.Zero || !_interaction.InRangeUnobstructed(door, exit, SqueezeExitRange))
        {
            _popup.PopupEntity(Loc.GetString("horror-squeeze-blocked"), ent, ent, PopupType.SmallCaution);
            return;
        }

        _xform.SetCoordinates(ent, exit);
        _popup.PopupEntity(Loc.GetString("horror-squeeze-done", ("horror", ent.Owner)), ent, PopupType.MediumCaution);
        args.Handled = true;
    }

    #endregion

    #region Host abilities

    private void OnTentacle(Entity<EldritchHorrorComponent> ent, ref HorrorTentacleActionEvent args)
    {
        if (args.Handled || !TryGetHost(ent, out var host))
            return;

        args.Handled = true;
        if (RemoveTentacles(host))
        {
            _popup.PopupEntity(Loc.GetString("horror-tentacle-off"), host, PopupType.Medium);
            return;
        }

        if (!_hands.TryGetEmptyHand(host, out _))
        {
            _popup.PopupEntity(Loc.GetString("horror-tentacle-no-hand"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (!TrySpendChemicals(ent, ent.Comp.TentacleCost))
            return;

        var tentacle = SpawnAtPosition(ent.Comp.TentaclePrototype, Transform(host).Coordinates);
        if (!_hands.TryPickupAnyHand(host, tentacle, checkActionBlocker: false))
        {
            Del(tentacle);
            AddChemicals(ent, ent.Comp.TentacleCost);
            return;
        }

        _audio.PlayPvs(TentacleSound, host);
        _popup.PopupEntity(Loc.GetString("horror-tentacle-on", ("target", IdentityOf(host))), host, PopupType.MediumCaution);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(ent):horror} grew a tentacle on {ToPrettyString(host):host}");
    }

    /// <summary>
    /// Turns any tentacle the body holds back into an arm.
    /// </summary>
    private bool RemoveTentacles(EntityUid host)
    {
        var removed = false;
        foreach (var held in _hands.EnumerateHeld(host).ToList())
        {
            if (!HasComp<HorrorTentacleComponent>(held))
                continue;

            QueueDel(held);
            removed = true;
        }

        return removed;
    }

    private void OnLeftHost(Entity<EldritchHorrorComponent> ent, ref HorrorLeftHostEvent args)
    {
        if (RemoveTentacles(args.Host))
            _popup.PopupEntity(Loc.GetString("horror-tentacle-off"), args.Host, PopupType.Medium);
    }

    private void OnTransfer(Entity<EldritchHorrorComponent> ent, ref HorrorTransferHostActionEvent args)
    {
        if (args.Handled || !TryGetHost(ent, out var host))
            return;

        var target = args.Target;
        if (target == host)
            return;

        if (!CanInfestFromHost(ent, target, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason, ("target", IdentityOf(target))), ent, ent, PopupType.SmallCaution);
            return;
        }

        var pulled = TryComp<PullerComponent>(host, out var puller) && puller.Pulling == target;
        var doAfter = new DoAfterArgs(EntityManager, ent, pulled ? ent.Comp.TransferPullTime : ent.Comp.TransferTime,
            new HorrorTransferDoAfterEvent(), ent, target)
        {
            DistanceThreshold = 1.5f,
            BreakOnMove = false,
            RequireCanInteract = false,
            CancelDuplicate = true,
        };

        args.Handled = true;
        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString("horror-transfer-start", ("target", IdentityOf(target))), ent, ent);
    }

    /// <summary>
    /// <see cref="CanInfest"/> for a horror that still sits in its old host, checked as if it were free.
    /// </summary>
    private bool CanInfestFromHost(Entity<EldritchHorrorComponent> ent, EntityUid target, out string reason)
    {
        var oldHost = ent.Comp.Host;
        ent.Comp.Host = null;
        var valid = CanInfest(ent, target, out reason);
        ent.Comp.Host = oldHost;
        return valid;
    }

    private void OnTransferDoAfter(Entity<EldritchHorrorComponent> ent, ref HorrorTransferDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target || ent.Comp.Host is not { } host)
            return;

        var pulled = TryComp<PullerComponent>(host, out var puller) && puller.Pulling == target;

        // the target could have stopped being a valid host during the transfer, check before leaving the old one
        if (!CanInfestFromHost(ent, target, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason, ("target", IdentityOf(target))), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (!TryLeaveHost(ent) || !Infest(ent, target))
            return;

        // pulling the new host up close makes it quick, but loud
        if (pulled)
        {
            _audio.PlayPvs(TentacleSound, target);
            _popup.PopupEntity(Loc.GetString("horror-transfer-noticed"), target, target, PopupType.MediumCaution);
        }

        args.Handled = true;
    }

    private void OnJumpstart(Entity<EldritchHorrorComponent> ent, ref HorrorJumpstartActionEvent args)
    {
        if (args.Handled || !TryGetHost(ent, out var host))
            return;

        if (!_mobState.IsDead(host))
        {
            _popup.PopupEntity(Loc.GetString("horror-jumpstart-alive"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (HasComp<Content.Shared.Traits.Assorted.UnrevivableComponent>(host))
        {
            _popup.PopupEntity(Loc.GetString("horror-jumpstart-unrevivable"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (!TrySpendChemicals(ent, ent.Comp.JumpstartCost))
            return;

        args.Handled = true;
        RaiseLocalEvent(host, new RejuvenateEvent());

        // bring a ghosted owner back into the revived body
        if (_mind.TryGetMind(host, out var mindId, out var mind) && mind.VisitingEntity != null)
            _mind.UnVisit(mindId, mind);

        _damageable.TryChangeDamage(host, ent.Comp.JumpstartDamage, true);
        _electrocution.TryDoElectrocution(host, ent, ent.Comp.JumpstartShockDamage, TimeSpan.FromSeconds(1), true, ignoreInsulation: true);
        _audio.PlayPvs(ReviveSound, host);

        _popup.PopupEntity(Loc.GetString("horror-jumpstart-done"), ent, ent, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("horror-jumpstart-done-host"), host, host, PopupType.LargeCaution);
        _adminLog.Add(LogType.Healed, LogImpact.High, $"{ToPrettyString(ent):horror} revived {ToPrettyString(host):host}");
    }

    private void OnViewMemory(Entity<EldritchHorrorComponent> ent, ref HorrorViewMemoryActionEvent args)
    {
        if (args.Handled || !TryGetHost(ent, out var host))
            return;

        if (_mobState.IsDead(host))
        {
            _popup.PopupEntity(Loc.GetString("horror-memory-dead"), ent, ent, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;
        if (_random.Prob(ent.Comp.MemoryNoticeChance))
            _popup.PopupEntity(Loc.GetString("horror-memory-noticed"), host, host, PopupType.MediumCaution);

        var lines = new List<string> { Loc.GetString("horror-memory-header", ("name", Name(host))) };
        if (_mind.TryGetMind(host, out var mindId, out var mind))
        {
            foreach (var objective in mind.Objectives)
            {
                if (_objectives.GetInfo(objective, mindId, mind) is { } info)
                    lines.Add(Loc.GetString("horror-memory-objective", ("objective", info.Title)));
            }
        }

        if (TryComp<EldritchHorrorInfestedComponent>(host, out var infested) && infested.RecentSpeech.Count > 0)
        {
            lines.Add(Loc.GetString("horror-memory-speech"));
            foreach (var spoken in infested.RecentSpeech)
                lines.Add(Loc.GetString("horror-memory-speech-line", ("line", spoken)));
        }

        SendPrivate(ent, string.Join("\n", lines));
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(ent):horror} read the memories of {ToPrettyString(host):host}");
    }

    #endregion
}
