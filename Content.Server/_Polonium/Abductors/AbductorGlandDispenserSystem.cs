using Content.Shared._Polonium.Abductors;
using Content.Shared.EntityTable;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Timing.Systems;

namespace Content.Server._Polonium.Abductors;

public sealed partial class AbductorGlandDispenserSystem : EntitySystem
{
    [Dependency] private EntityTableSystem _table = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AbductorGlandDispenserComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnActivate(Entity<AbductorGlandDispenserComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        if (!HasComp<AbductorComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("abductor-unfamiliar"), ent, args.User);
            return;
        }

        if (!_useDelay.TryResetDelay(ent.Owner, true))
            return;

        foreach (var gland in _table.GetSpawns(ent.Comp.Table))
        {
            _hands.PickupOrDrop(args.User, SpawnAtPosition(gland, Transform(ent).Coordinates));
        }
    }
}
