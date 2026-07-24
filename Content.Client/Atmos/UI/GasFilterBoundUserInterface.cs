using Content.Client.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Piping.Trinary.Components;
using Content.Shared.Localizations;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Atmos.UI;

/// <summary>
/// Initializes a <see cref="GasFilterWindow"/> and updates it from the entity's <see cref="GasFilterComponent"/>.
/// </summary>
[UsedImplicitly]
public sealed partial class GasFilterBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;

    [ViewVariables]
    private GasFilterWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<GasFilterWindow>();
        _window.PopulateGasList(_atmosphere.Gases);

        _window.ToggleStatusButtonPressed += OnToggleStatusButtonPressed;
        _window.FilterTransferRateChanged += OnFilterTransferRatePressed;
        _window.FilterGasesChanged += OnFilterGasesChanged;

        Update();
    }

    public override void Update()
    {
        base.Update();

        if (_window == null || !EntMan.TryGetComponent(Owner, out GasFilterComponent? filter))
            return;

        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.SetFilterStatus(filter.Enabled);
        _window.SetTransferRate(filter.TransferRate);

        if (filter.FilterGases.Count > 0)
        {
            _window.SetFilteredGases(filter.FilterGases);
        }
        else if (filter.FilteredGas is { } filtered)
        {
            _window.SetFilteredGases(new HashSet<Gas> { filtered });
        }
        else
        {
            _window.SetFilteredGases(new HashSet<Gas>());
        }
    }

    private void OnToggleStatusButtonPressed()
    {
        if (_window is null)
            return;

        SendPredictedMessage(new GasFilterToggleStatusMessage(_window.FilterStatus));
    }

    private void OnFilterTransferRatePressed(string value)
    {
        var rate = UserInputParser.TryFloat(value, out var parsed) ? parsed : 0f;

        SendPredictedMessage(new GasFilterChangeRateMessage(rate));
    }

    private void OnFilterGasesChanged(HashSet<Gas> gases) // Funky - for filtering of multiple gases
    {
        SendPredictedMessage(new GasFilterChangeGasesMessage(gases));
    }
}
