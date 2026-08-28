using System.Linq;
using Content.Shared.Administration.Logs;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Atmos.Piping.Trinary.Components;
using Content.Shared.Database;
using Content.Shared.Examine;

namespace Content.Shared.Atmos.Piping.Trinary.EntitySystems;

public abstract partial class SharedGasFilterSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAtmosphereSystem _atmosphereSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GasFilterComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<GasFilterComponent> ent, ref ExaminedEvent args)
    {
        if (Loc.TryGetString("gas-volume-pump-system-examined",
                out var transferRateStr,
                ("statusColor", "lightblue"),
                ("rate", ent.Comp.TransferRate.ToString("G"))
            ))
        {
            args.PushMarkup(transferRateStr);
        }

        var gasName = Loc.GetString("comp-gas-filter-ui-filter-gas-none");
        if (ent.Comp.FilterGases.Count > 0)
            gasName = string.Join(", ", ent.Comp.FilterGases.Select(g => Loc.GetString(_atmosphereSystem.GetGas(g).Name)));
        else if (ent.Comp.FilteredGas.HasValue)
            gasName = Loc.GetString(_atmosphereSystem.GetGas((Gas)ent.Comp.FilteredGas).Name);

        if (Loc.TryGetString("comp-gas-filter-filtered-gas-examine",
                out var filteredGasStr,
                ("statusColor", "lightblue"),
                ("filteredGas", gasName)
            ))
        {
            args.PushMarkup(filteredGasStr);
        }
    }

    [SubscribeLocalEvent]
    private void OnToggleStatusMessage(Entity<GasFilterComponent> ent, ref GasFilterToggleStatusMessage args)
    {
        ent.Comp.Enabled = args.Enabled;
        _adminLogger.Add(LogType.AtmosPowerChanged, LogImpact.Medium,
            $"{ToPrettyString(args.Actor):player} set the power on {ToPrettyString(ent.Owner):device} to {args.Enabled}");

        DirtyField(ent.Owner, ent.Comp, nameof(GasFilterComponent.Enabled));
        UpdateUi(ent);
        UpdateAppearance(ent);
    }

    [SubscribeLocalEvent]
    private void OnTransferRateChangeMessage(Entity<GasFilterComponent> ent, ref GasFilterChangeRateMessage args)
    {
        ent.Comp.TransferRate = Math.Clamp(args.Rate, 0f, ent.Comp.MaxTransferRate);
        _adminLogger.Add(LogType.AtmosVolumeChanged, LogImpact.Medium,
            $"{ToPrettyString(args.Actor):player} set the transfer rate on {ToPrettyString(ent.Owner):device} to {args.Rate}");

        DirtyField(ent.Owner, ent.Comp, nameof(GasFilterComponent.TransferRate));
        UpdateUi(ent);
    }

    [SubscribeLocalEvent]
    private void OnSelectGasMessage(Entity<GasFilterComponent> ent, ref GasFilterSelectGasMessage args)
    {
        if (args.Gas.HasValue)
        {
            if (!Enum.IsDefined(typeof(Gas), args.Gas))
            {
                Log.Warning($"{ToPrettyString(ent.Owner)} received GasFilterSelectGasMessage with an invalid ID: {args.Gas}");
                return;
            }

            ent.Comp.FilteredGas = args.Gas;
            ent.Comp.FilterGases = new HashSet<Gas> { args.Gas.Value };
            _adminLogger.Add(LogType.AtmosFilterChanged, LogImpact.Medium,
                $"{ToPrettyString(args.Actor):player} set the filter on {ToPrettyString(ent.Owner):device} to {args.Gas.ToString()}");
        }
        else
        {
            ent.Comp.FilteredGas = null;
            ent.Comp.FilterGases.Clear();
            _adminLogger.Add(LogType.AtmosFilterChanged, LogImpact.Medium,
                $"{ToPrettyString(args.Actor):player} set the filter on {ToPrettyString(ent.Owner):device} to none");
        }

        DirtyField(ent.Owner, ent.Comp, nameof(GasFilterComponent.FilteredGas));
        DirtyField(ent.Owner, ent.Comp, nameof(GasFilterComponent.FilterGases));
        UpdateUi(ent);
    }

    [SubscribeLocalEvent]
    private void OnChangeGasesMessage(Entity<GasFilterComponent> ent, ref GasFilterChangeGasesMessage args)
    {
        ent.Comp.FilterGases = new HashSet<Gas>(args.Gases);
        Gas? single = null;
        if (args.Gases.Count == 1)
        {
            foreach (var gas in args.Gases)
            {
                single = gas;
                break;
            }
        }
        ent.Comp.FilteredGas = single;
        _adminLogger.Add(LogType.AtmosFilterChanged, LogImpact.Medium,
            $"{ToPrettyString(args.Actor):player} set the filter gases on {ToPrettyString(ent.Owner):device} to {string.Join(", ", args.Gases)}");

        DirtyField(ent.Owner, ent.Comp, nameof(GasFilterComponent.FilterGases));
        DirtyField(ent.Owner, ent.Comp, nameof(GasFilterComponent.FilteredGas));
        UpdateUi(ent);
    }

    protected void UpdateAppearance(Entity<GasFilterComponent> ent)
    {
        _appearance.SetData(ent, FilterVisuals.Enabled, ent.Comp.Enabled);
    }

    protected virtual void UpdateUi(Entity<GasFilterComponent> ent)
    {
    }
}
