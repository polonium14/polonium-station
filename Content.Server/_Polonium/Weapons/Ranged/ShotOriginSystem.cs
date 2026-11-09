using Content.Server.Chat.Managers;
using Content.Shared.CCVar;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Weapons.Ranged;

/// <summary>
/// Checks where a predicting client says it fired from, so the server shot leaves from the same place instead of from wherever the walking shooter has got to by the time the request arrives.
/// </summary>
public sealed partial class ShotOriginSystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TransformSystem _transform = default!;

    private const float OriginTolerance = 0.1f;

    private readonly Dictionary<NetUserId, TimeSpan> _nextAlert = new();
    private bool _alerts;
    private TimeSpan _alertCooldown;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_config, CCVars.GunOriginRejectionAlerts, v => _alerts = v, true);
        Subs.CVar(_config, CCVars.GunOriginRejectionAlertCooldown, v => _alertCooldown = TimeSpan.FromSeconds(v), true);
    }

    /// <summary>
    /// How far ahead of the server a predicting client may be when its shot arrives.
    /// </summary>
    public TimeSpan ShotLead(ICommonSession? session)
    {
        var ping = session?.Channel.Ping ?? 0;
        return TimeSpan.FromMilliseconds(ping * 1.5) + _timing.TickPeriod * 2;
    }

    /// <summary>
    /// Returns the client's shot origin if the shooter could have stood there and nothing solid is in between.
    /// </summary>
    public EntityCoordinates? ValidateOrigin(ICommonSession session, EntityUid shooter, NetCoordinates? netOrigin)
    {
        if (GetCoordinates(netOrigin) is not { } origin || !origin.IsValid(EntityManager))
            return null;

        var originMap = _transform.ToMapCoordinates(origin);
        var shooterMap = _transform.GetMapCoordinates(shooter);
        if (originMap.MapId != shooterMap.MapId)
            return null;

        var speed = _physics.GetMapLinearVelocity(shooter).Length();
        if (TryComp(shooter, out MovementSpeedModifierComponent? movement))
            speed = MathF.Max(speed, movement.CurrentSprintSpeed);

        var range = speed * (float) ShotLead(session).TotalSeconds + OriginTolerance;
        if (!_interaction.InRangeUnobstructed(shooterMap, originMap, range, predicate: e => e == shooter))
        {
            AlertRejected(session, shooter, (originMap.Position - shooterMap.Position).Length(), range);
            return null;
        }

        return origin;
    }

    private void AlertRejected(ICommonSession session, EntityUid shooter, float distance, float range)
    {
        if (!_alerts)
            return;

        var now = _timing.RealTime;
        if (_nextAlert.TryGetValue(session.UserId, out var next) && now < next)
            return;

        _nextAlert[session.UserId] = now + _alertCooldown;
        _chat.SendAdminAlert(shooter,
            Loc.GetString("shot-origin-rejected-admin-alert",
                ("distance", MathF.Round(distance, 2)),
                ("range", MathF.Round(range, 2))));
    }
}
