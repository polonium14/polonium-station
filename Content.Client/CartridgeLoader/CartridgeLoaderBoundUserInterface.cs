// SPDX-FileCopyrightText: 2022 Julian Giebel <juliangiebel@live.de>
// SPDX-FileCopyrightText: 2022 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 TemporalOroboros <TemporalOroboros@gmail.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 pathetic meowmeow <uhhadd@gmail.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
// SPDX-FileCopyrightText: 2026 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.UserInterface.Fragments;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client.CartridgeLoader;


public abstract class CartridgeLoaderBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private EntityUid? _activeProgram;

    [ViewVariables]
    private UIFragment? _activeCartridgeUI;

    [ViewVariables]
    private Control? _activeUiFragment;

    private IEntityManager _entManager;

    private IGameTiming _timing;

    protected CartridgeLoaderBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _entManager = IoCManager.Resolve<IEntityManager>();
        _timing = IoCManager.Resolve<IGameTiming>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not CartridgeLoaderUiState loaderUiState)
        {
            _activeCartridgeUI?.UpdateState(state);
            return;
        }

        // TODO move this to a component state and ensure the net ids.
        var programs = GetCartridgeComponents(_entManager.GetEntityList(loaderUiState.Programs));
        UpdateAvailablePrograms(programs);

        var activeUI = _entManager.GetEntity(loaderUiState.ActiveUI);

        // Prevent the active program's UI from getting rebuilt and attached multiple times.
        if (_activeProgram == activeUI && _activeUiFragment is not null)
            return;

        _activeProgram = activeUI;

        var ui = RetrieveCartridgeUI(activeUI);
        var comp = RetrieveCartridgeComponent(activeUI);
        var control = ui?.GetUIFragmentRoot();

        if (_activeUiFragment is not null)
            DetachCartridgeUI(_activeUiFragment);

        if (control is not null && _activeProgram.HasValue)
            AttachCartridgeUI(control, Loc.GetString(comp?.ProgramName ?? "default-program-name"));

        _activeCartridgeUI = ui;
        _activeUiFragment?.Dispose();
        _activeUiFragment = control;

        if (control is not null && _activeProgram.HasValue)
            SendCartridgeUiReadyEvent(_activeProgram.Value);
    }

    protected void ActivateCartridge(EntityUid cartridgeUid)
    {
        var message = new CartridgeLoaderUiMessage(_entManager.GetNetEntity(cartridgeUid), CartridgeUiMessageAction.Activate);
        SendPredictedMessage(message);
    }

    protected void DeactivateActiveCartridge()
    {
        if (!_activeProgram.HasValue)
            return;

        var message = new CartridgeLoaderUiMessage(_entManager.GetNetEntity(_activeProgram.Value), CartridgeUiMessageAction.Deactivate);
        SendPredictedMessage(message);
    }

    protected void InstallCartridge(EntityUid cartridgeUid)
    {
        var message = new CartridgeLoaderUiMessage(_entManager.GetNetEntity(cartridgeUid), CartridgeUiMessageAction.Install);
        SendPredictedMessage(message);
    }

    protected void UninstallCartridge(EntityUid cartridgeUid)
    {
        var message = new CartridgeLoaderUiMessage(_entManager.GetNetEntity(cartridgeUid), CartridgeUiMessageAction.Uninstall);
        SendPredictedMessage(message);
    }

    private List<(EntityUid, CartridgeComponent)> GetCartridgeComponents(List<EntityUid> programs)
    {
        var components = new List<(EntityUid, CartridgeComponent)>();

        foreach (var program in programs)
        {
            var component = RetrieveCartridgeComponent(program);
            if (component is not null)
                components.Add((program, component));
        }

        return components;
    }

    /// <summary>
    /// The implementing ui needs to add the passed ui fragment as a child to itself
    /// </summary>
    protected abstract void AttachCartridgeUI(Control cartridgeUIFragment, string? title);

    /// <summary>
    /// The implementing ui needs to remove the passed ui from itself
    /// </summary>
    protected abstract void DetachCartridgeUI(Control cartridgeUIFragment);

    protected abstract void UpdateAvailablePrograms(List<(EntityUid, CartridgeComponent)> programs);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _activeUiFragment?.Dispose();
    }

    protected CartridgeComponent? RetrieveCartridgeComponent(EntityUid? cartridgeUid)
    {
        return EntMan.GetComponentOrNull<CartridgeComponent>(cartridgeUid);
    }

    private void SendCartridgeUiReadyEvent(EntityUid cartridgeUid)
    {
        var message = new CartridgeLoaderUiMessage(_entManager.GetNetEntity(cartridgeUid), CartridgeUiMessageAction.UIReady);
        if (_timing.InPrediction && _timing.IsFirstTimePredicted)
            SendPredictedMessage(message);
        else
            SendMessage(message);
    }

    private UIFragment? RetrieveCartridgeUI(EntityUid? cartridgeUid)
    {
        var component = EntMan.GetComponentOrNull<UIFragmentComponent>(cartridgeUid);
        component?.Ui?.Setup(this, cartridgeUid);
        return component?.Ui;
    }
}
