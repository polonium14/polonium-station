using Content.Shared._RMC14.CCVar;
using Content.Shared.CombatMode;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

public abstract class SharedGunPredictionSystem : EntitySystem
{
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;

    public bool GunPrediction { get; private set; }

    public override void Initialize()
    {
        Subs.CVar(_config, RMCCVars.RMCGunPrediction, v => GunPrediction = v, true);
    }

    public List<EntityUid>? ShootRequested(
        NetEntity netGun,
        NetCoordinates coordinates,
        NetEntity? target,
        List<int>? projectiles,
        ICommonSession session,
        bool rearmSemiAuto = false)
    {
        var user = session.AttachedEntity;

        if (user == null ||
            !_combatMode.IsInCombatMode(user) ||
            !_gun.TryGetGun(user.Value, out var gun))
        {
            return null;
        }

        if (gun.Owner != GetEntity(netGun))
            return null;

#pragma warning disable RA0002
        gun.Comp.ShootCoordinates = GetCoordinates(coordinates);
        gun.Comp.Target = GetEntity(target);
#pragma warning restore RA0002

        if (rearmSemiAuto)
            _gun.ResetShotCounter(gun);

        return _gun.AttemptShoot(user.Value, gun, projectiles, session);
    }
}
