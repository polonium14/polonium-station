using Content.Shared._Polonium.Modules.ModSuit.UI;
using Robust.Client.UserInterface;
using JetBrains.Annotations;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Modules.ModSuit;

[UsedImplicitly]
public sealed partial class ModSuitBoundUserInterface : BoundUserInterface
{
    [Dependency] private IGameTiming _timing = default!;

    private ModSuitWindow? _window;

    private TimeSpan _nextFlashlightUpdate;
    private static readonly TimeSpan FlashlightUpdateRate = TimeSpan.FromSeconds(0.25f);

    public ModSuitBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ModSuitWindow>();
        _window.Refresh();

        _window.OnSealButtonPressed += parts => SendPredictedMessage(new ModSuitSealButtonMessage(parts));
        _window.OnToggleButtonPressed += (module, toggle) => SendPredictedMessage(new ModSuitToggleButtonMessage(module, toggle));
        _window.OnEjectButtonPressed += module => SendPredictedMessage(new ModSuitEjectButtonMessage(module));

        _window.OnFlashlightColorChanged += (module, color) =>
        {
            if (_nextFlashlightUpdate > _timing.RealTime)
                return;

            SendPredictedMessage(new ModSuitFlashlightColorChangedMessage(module, color));
            _nextFlashlightUpdate = _timing.RealTime + FlashlightUpdateRate;
        };
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
