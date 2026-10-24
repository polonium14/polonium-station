using Content.Shared._Polonium.Modules.ModSuit.UI;
using Robust.Client.UserInterface;
using JetBrains.Annotations;

namespace Content.Client._Polonium.Modules.ModSuit;

[UsedImplicitly]
public sealed partial class ModSuitBoundUserInterface : BoundUserInterface
{
    private ModSuitWindow? _window;

    public ModSuitBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ModSuitWindow>();
        _window.Refresh();

        _window.OnSealButtonPressed += parts => SendPredictedMessage(new ModSuitSealButtonMessage(parts));
        _window.OnToggleButtonPressed += (module, toggle) => SendPredictedMessage(new ModSuitToggleButtonMessage(module, toggle));
        _window.OnEjectButtonPressed += module => SendPredictedMessage(new ModSuitEjectButtonMessage(module));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not ModSuitBoundUserInterfaceState modSuitState || _window == null)
            return;

        foreach (var entry in modSuitState.Entries)
        {
            switch (entry)
            {
                case ModSuitSealableBuiEntry sealable:
                    _window.UpdateSealed(sealable);
                    break;

                case ModSuitModuleBuiEntry module:
                    _window.UpdateModules(module);
                    break;

                case ModSuitComplexityBuiEntry complexity:
                    _window.UpdateComplexity(complexity);
                    break;

                case BaseModSuitPowerBuiEntry power:
                    _window.UpdatePower(power);
                    break;
            }
        }
    }
}
