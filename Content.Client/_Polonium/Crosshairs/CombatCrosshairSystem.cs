using Content.Shared._Polonium.Crosshairs;
using Content.Shared._Polonium.Weapons.Ranged.Components;
using Content.Shared._Polonium.Weapons.Ranged.Systems;
using Content.Shared.CCVar;
using Content.Shared.CombatMode;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Crosshairs;

/// <summary>
/// Picks the combat-mode crosshair of the local player and draws it through <see cref="CombatCrosshairOverlay"/>:
/// the spread crosshair of the gun they aim with, or one static crosshair for everything else.
/// </summary>
public sealed partial class CombatCrosshairSystem : EntitySystem
{
    private static readonly ProtoId<CrosshairPrototype> MeleeCrosshair = "Melee";

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IDependencyCollection _deps = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private GunCrosshairSystem _crosshair = default!;
    [Dependency] private SharedCombatModeSystem _combat = default!;
    [Dependency] private SharedGunSystem _gun = default!;

    private const float OpenRate = 25f;
    private const float CloseRate = 10f;

    private bool _indicators;
    private bool _single;
    private bool _smoothing;

    private readonly Dictionary<EntityUid, (ProtoId<CrosshairPrototype> Crosshair, Angle Pellets)> _lastLoaded = new();

    /// <summary>
    /// The gun the crosshair belongs to, or null for the melee crosshair.
    /// </summary>
    public EntityUid? Gun { get; private set; }

    /// <summary>
    /// The crosshair drawn this frame, or null when the stock combat-mode sight is shown instead.
    /// </summary>
    public CrosshairPrototype? Crosshair { get; private set; }

    /// <summary>
    /// The full cone of the next shot of <see cref="Gun"/> in radians, eased when smoothing is on.
    /// </summary>
    public float Spread { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.CombatModeIndicatorsPointShow, v => _indicators = v, true);
        Subs.CVar(_cfg, CCVars.CrosshairSingle, v => _single = v, true);
        Subs.CVar(_cfg, CCVars.CrosshairSmoothing, v => _smoothing = v, true);

        SubscribeLocalEvent<GunComponent, ComponentShutdown>(OnGunShutdown);

        _overlays.AddOverlay(new CombatCrosshairOverlay(_deps, this));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<CombatCrosshairOverlay>();
    }

    private void OnGunShutdown(Entity<GunComponent> ent, ref ComponentShutdown args)
    {
        _lastLoaded.Remove(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_indicators || _player.LocalEntity is not { } player || !_combat.IsInCombatMode(player))
        {
            Gun = null;
            Crosshair = null;
            return;
        }

        if (!_gun.TryGetGun(player, out var gun))
        {
            Gun = null;
            Crosshair = _proto.Index(MeleeCrosshair);
            return;
        }

        if (TryComp<ChamberMagazineAmmoProviderComponent>(gun, out var chamber) && chamber.BoltClosed == false)
        {
            Gun = null;
            Crosshair = null;
            return;
        }

        // An emptied gun keeps the crosshair of the round it was loaded with, many guns have no fill to fall back on.
        if (_crosshair.TryGetCrosshair(gun, out var crosshair, out var pellets))
            _lastLoaded[gun] = (crosshair, pellets);
        else if (_lastLoaded.TryGetValue(gun, out var last))
            (crosshair, pellets) = last;

        if (_single)
            crosshair = GunCrosshairComponent.DefaultCrosshair;

        var target = (float)(_gun.PeekSpread(gun, player).Theta + pellets.Theta);
        if (Gun != gun.Owner || !_smoothing)
        {
            Spread = target;
        }
        else
        {
            var rate = target > Spread ? OpenRate : CloseRate;
            Spread += (target - Spread) * (1f - MathF.Exp(-rate * frameTime));
        }

        Gun = gun.Owner;
        Crosshair = _proto.Index(crosshair);
    }
}
