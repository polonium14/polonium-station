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
    private const int HoldTicks = 10;

    [Dependency] private IClientAdminManager _admins = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEyeManager _viewports = default!;
    [Dependency] private ILightManager _pipeline = default!;
    [Dependency] private IPlayerManager _sessions = default!;
    [Dependency] private SandboxSystem _placement = default!;

    private bool _enabled = true;
    private int _holdA;
    private int _holdB;
    private int _holdC;
    private int _holdD;
    private int _holdE;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, CCVars.DscEnabled, v => _enabled = v, true);
    }

    public override void Update(float frameTime)
    {
        if (!_enabled)
        {
            ResetState();
            return;
        }

        if (_sessions.LocalEntity is not { } local)
        {
            ResetState();
            return;
        }

        if (_placement.SandboxAllowed || _admins.IsActive() || HasComp<GhostComponent>(local))
        {
            ResetState();
            return;
        }

        var overlayOwns = TryComp<BlindableComponent>(local, out var blindable)
            && (blindable.IsBlind || blindable.LightSetup || blindable.GraceFrame);

        var bound = TryBindEye(out var wantFov, out var wantLight);

        var flagA = !overlayOwns && !_pipeline.Enabled;
        var flagB = !_pipeline.DrawLighting;
        var flagC = !_pipeline.DrawHardFov;
        var flagD = bound && wantFov && !_viewports.CurrentEye.DrawFov;
        var flagE = bound && wantLight && !_viewports.CurrentEye.DrawLight;

        Track(ref _holdA, flagA, ViewportPrefCodes.A, () => _pipeline.Enabled = true);
        Track(ref _holdB, flagB, ViewportPrefCodes.B, () => _pipeline.DrawLighting = true);
        Track(ref _holdC, flagC, ViewportPrefCodes.C, () => _pipeline.DrawHardFov = true);
        Track(ref _holdD, flagD, ViewportPrefCodes.D, () => _viewports.CurrentEye.DrawFov = true);
        Track(ref _holdE, flagE, ViewportPrefCodes.E, () => _viewports.CurrentEye.DrawLight = true);
    }

    private void ResetState()
    {
        _holdA = 0;
        _holdB = 0;
        _holdC = 0;
        _holdD = 0;
        _holdE = 0;
    }

    private void Track(ref int hold, bool isSet, string code, Action restore)
    {
        if (!isSet)
        {
            hold = 0;
            return;
        }

        if (hold < HoldTicks)
        {
            hold++;
            if (hold < HoldTicks)
                return;

            RaiseNetworkEvent(new ViewportPrefRelayEvent(code));
        }

        restore();
    }

    private bool TryBindEye(out bool drawFov, out bool drawLight)
    {
        drawFov = true;
        drawLight = true;

        var eye = _viewports.CurrentEye;

        if (_sessions.LocalEntity is { } local
            && TryComp<EyeComponent>(local, out var localEye)
            && ReferenceEquals(localEye.Eye, eye))
        {
            drawFov = localEye.DrawFov;
            drawLight = localEye.DrawLight;
            return true;
        }

        var query = EntityQueryEnumerator<EyeComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (!ReferenceEquals(comp.Eye, eye))
                continue;

            drawFov = comp.DrawFov;
            drawLight = comp.DrawLight;
            return true;
        }

        return false;
    }
}
