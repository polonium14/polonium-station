// SPDX-FileCopyrightText: 2021 ike709 <ike709@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2022 Vordenburg <114301317+Vordenburg@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 mirrorcult <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2023 TemporalOroboros <TemporalOroboros@gmail.com>
// SPDX-FileCopyrightText: 2023 Tom Leys <tom@crump-leys.com>
// SPDX-FileCopyrightText: 2023 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <comedian_vs_clown@hotmail.com>
// SPDX-FileCopyrightText: 2024 Kot <1192090+koteq@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 slarticodefast <161409025+slarticodefast@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Absotively <jen@jenpollock.ca>
// SPDX-FileCopyrightText: 2026 Szyszkrzyneczka <52501307+Szyszkrzyneczka@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 psykana <36602558+psykana@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
// SPDX-FileCopyrightText: 2026 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

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

    private void OnFilterGasesChanged(HashSet<Gas> gases)
    {
        SendPredictedMessage(new GasFilterChangeGasesMessage(gases));
    }
}
