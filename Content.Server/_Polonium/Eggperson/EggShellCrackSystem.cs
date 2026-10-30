using Content.Shared._Polonium.Eggperson;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._Polonium.Eggperson;

/// <summary>
/// Plays the shell crack on heavy hits and shatters the egg once its brute damage gets too high.
/// </summary>
public sealed partial class EggShellCrackSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EggShellComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<EggShellComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.Shattered || !args.DamageIncreased || args.DamageDelta is not { } delta)
            return;

        var dealt = EggShellSystem.SumTypes(delta, ent.Comp.BruteTypes);
        if (dealt <= 0)
            return;

        var total = EggShellSystem.SumTypes(_damageable.GetAllDamage(ent.Owner), ent.Comp.BruteTypes);
        if (total >= ent.Comp.ShatterBrute)
        {
            ent.Comp.Shattered = true;
            // Coordinates, not the entity: it is deleted by the gib right after.
            _popup.PopupCoordinates(Loc.GetString("eggperson-shatter", ("egg", ent.Owner)), Transform(ent).Coordinates, Filter.Pvs(ent), true, PopupType.LargeCaution);
            _audio.PlayPvs(ent.Comp.ShatterSound, ent);
            _gibbing.Gib(ent);
            return;
        }

        if (dealt >= ent.Comp.CrackThreshold)
            _audio.PlayPvs(ent.Comp.CrackSound, ent);
    }
}
