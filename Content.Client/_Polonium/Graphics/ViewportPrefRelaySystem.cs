using Content.Client.Administration.Managers;
using Content.Client.Sandbox;
using Content.Shared._Polonium.Graphics;
using Content.Shared.CCVar;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Ghost;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Client._Polonium.Graphics;

public sealed partial class ViewportPrefRelaySystem : EntitySystem
{
    [Dependency] private IClientAdminManager _admins = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEyeManager _viewports = default!;
    [Dependency] private ILightManager _pipeline = default!;
    [Dependency] private IPlayerManager _sessions = default!;
    [Dependency] private SandboxSystem _placement = default!;

    private bool _enabled = true;
    private bool _lastA;
    private bool _lastB;
    private bool _lastC;
    private bool _lastD;
    private bool _lastE;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, CCVars.DscEnabled, v => _enabled = v, true);
    }

    public override void Update(float frameTime)
    {
        if (!_enabled)
            return;

        if (_sessions.LocalEntity is not { } local)
            return;

        if (_placement.SandboxAllowed || _admins.IsActive() || HasComp<GhostComponent>(local))
            return;

        var occluded = TryComp<BlindableComponent>(local, out var blindable) && blindable.IsBlind;

        var flagA = !occluded && !_pipeline.Enabled;
        var flagB = !_pipeline.DrawLighting;
        var flagC = !_pipeline.DrawHardFov;
        var flagD = !_viewports.CurrentEye.DrawFov;
        var flagE = !_viewports.CurrentEye.DrawLight;

        PushIfChanged(ref _lastA, flagA, ViewportPrefCodes.A);
        PushIfChanged(ref _lastB, flagB, ViewportPrefCodes.B);
        PushIfChanged(ref _lastC, flagC, ViewportPrefCodes.C);
        PushIfChanged(ref _lastD, flagD, ViewportPrefCodes.D);
        PushIfChanged(ref _lastE, flagE, ViewportPrefCodes.E);

        if (flagA)
            _pipeline.Enabled = true;
        if (flagB)
            _pipeline.DrawLighting = true;
        if (flagC)
            _pipeline.DrawHardFov = true;
        if (flagD)
            _viewports.CurrentEye.DrawFov = true;
        if (flagE)
            _viewports.CurrentEye.DrawLight = true;
    }

    private void PushIfChanged(ref bool wasSet, bool isSet, string code)
    {
        if (isSet && !wasSet)
            RaiseNetworkEvent(new ViewportPrefRelayEvent(code));

        wasSet = isSet;
    }
}
