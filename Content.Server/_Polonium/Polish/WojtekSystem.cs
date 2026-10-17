using Content.Shared._Polonium.Polish;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Polonium.Polish;

/// <summary>
/// Wojtek zjada podane papierosy i wypija piwo, lecząc się. W trybie walki przedmiot działa normalnie (bicie).
/// </summary>
public sealed partial class WojtekSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedCombatModeSystem _combatMode = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WojtekComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<WojtekComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<WojtekComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            _audio.PlayPvs(ent.Comp.DeathSound, ent);
    }

    private void OnInteractUsing(Entity<WojtekComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_mobState.IsAlive(ent) || _combatMode.IsInCombatMode(args.User))
            return;

        if (_tag.HasTag(args.Used, ent.Comp.CigaretteTag))
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("wojtek-eats-cigarette", ("bear", ent.Owner), ("user", args.User), ("item", args.Used)), ent, PopupType.Medium);
            QueueDel(args.Used);
            _damageable.HealEvenly(ent.Owner, -ent.Comp.CigaretteHeal, ent.Comp.HealGroup);
            return;
        }

        if (!_solution.TryGetDrainableSolution(args.Used, out var soln, out var solution)
            || !solution.ContainsPrototype(ent.Comp.Beer))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("wojtek-drinks-beer", ("bear", ent.Owner), ("user", args.User), ("item", args.Used)), ent, PopupType.Medium);
        _solution.RemoveAllSolution(soln.Value);
        _damageable.HealEvenly(ent.Owner, -ent.Comp.BeerHeal, ent.Comp.HealGroup);
        _audio.PlayPvs(ent.Comp.DrinkSound, ent);
    }
}
