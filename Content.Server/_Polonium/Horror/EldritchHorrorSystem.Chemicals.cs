using Content.Shared._Polonium.Horror;
using Content.Shared._Polonium.Horror.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Horror;

public sealed partial class EldritchHorrorSystem
{
    private void InitializeChemicals()
    {
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorChemicalsMenuActionEvent>(OnChemicalsMenu);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorInjectChemicalMessage>(OnInjectMessage);
        SubscribeLocalEvent<EldritchHorrorComponent, BoundUIOpenedEvent>(OnUiOpened);
    }

    private void OnChemicalsMenu(Entity<EldritchHorrorComponent> ent, ref HorrorChemicalsMenuActionEvent args)
    {
        if (args.Handled || !TryGetHost(ent, out _))
            return;

        _ui.TryToggleUi(ent.Owner, HorrorChemicalsUiKey.Key, ent);
        args.Handled = true;
    }

    private void OnUiOpened(Entity<EldritchHorrorComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is HorrorChemicalsUiKey)
            UpdateChemicalsUi(ent);
    }

    private void OnInjectMessage(Entity<EldritchHorrorComponent> ent, ref HorrorInjectChemicalMessage args)
    {
        // the menu only belongs to the horror itself
        if (args.Actor != ent.Owner || !Proto.TryIndex(args.Chemical, out var chemical))
            return;

        TryInject(ent, chemical);
    }

    /// <summary>
    /// Pumps a chemical from the horror's glands into the host's blood.
    /// </summary>
    public bool TryInject(Entity<EldritchHorrorComponent> ent, HorrorChemicalPrototype chemical)
    {
        if (ent.Comp.Controlling || _mobState.IsDead(ent) || !TryGetHost(ent, out var host))
            return false;

        // the client could ask for a chemical the horror hasn't unlocked yet
        if (chemical.RequiredUpgrade is { } upgrade && !ent.Comp.Upgrades.Contains(upgrade))
            return false;

        if (ent.Comp.Chemicals < chemical.Cost)
        {
            _popup.PopupEntity(Loc.GetString("horror-not-enough-chemicals", ("amount", chemical.Cost)), ent, ent, PopupType.SmallCaution);
            return false;
        }

        var solution = new Solution();
        solution.AddReagent(chemical.Reagent, chemical.Amount);
        if (!_bloodstream.TryAddToBloodstream(host, solution))
        {
            _popup.PopupEntity(Loc.GetString("horror-inject-failed"), ent, ent, PopupType.SmallCaution);
            return false;
        }

        AddChemicals(ent, -chemical.Cost);

        var reagentName = Proto.TryIndex(chemical.Reagent, out var reagent) ? reagent.LocalizedName : chemical.Reagent.Id;
        _popup.PopupEntity(Loc.GetString("horror-inject-done",
            ("amount", chemical.Amount),
            ("reagent", reagentName),
            ("target", Identity.Entity(host, EntityManager))), ent, ent);
        _adminLog.Add(LogType.ForceFeed, LogImpact.Low,
            $"{ToPrettyString(ent):horror} injected {chemical.Amount}u of {chemical.Reagent} into {ToPrettyString(host):host}");
        return true;
    }

    private void UpdateChemicalsUi(Entity<EldritchHorrorComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, HorrorChemicalsUiKey.Key))
            return;

        var chemicals = new List<ProtoId<HorrorChemicalPrototype>>();
        foreach (var chem in GetAvailableChemicals(ent))
            chemicals.Add(chem.ID);

        _ui.SetUiState(ent.Owner, HorrorChemicalsUiKey.Key,
            new HorrorChemicalsBoundUserInterfaceState(chemicals, ent.Comp.Chemicals, ent.Comp.Host != null));
    }
}
