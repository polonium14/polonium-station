using Content.Server._Polonium.Contributors;
using Content.Server._Polonium.Supporters;
using Content.Server.Administration.Managers;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._Polonium.GhostThemes;
using Content.Shared.CCVar;
using Content.Shared.Ghost.Components;
using Content.Shared.Ghost.Systems;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.GhostThemes;

/// <summary>
/// Decides which ghost themes a player can use and puts the picked one on their ghost.
/// The pick itself is kept by the client, which sends it every time the player becomes a ghost.
/// </summary>
public sealed partial class GhostThemeSystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ContributorManager _contributors = default!;
    [Dependency] private PlayTimeTrackingManager _playTime = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedGhostSystem _ghost = default!;
    [Dependency] private SupporterSystem _supporters = default!;

    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RequestGhostThemesEvent>(OnRequest);
        SubscribeNetworkEvent<SelectGhostThemeEvent>(OnSelect);

        Subs.CVar(_cfg, CCVars.GhostThemeEnabled, OnEnabledChanged, true);
    }

    /// <summary>
    /// Whether the player can use the theme right now.
    /// </summary>
    public bool IsAvailable(ICommonSession player, GhostThemePrototype theme)
    {
        if (!_proto.TryIndex(theme.Category, out var category))
            return false;

        var access = category.Access switch
        {
            GhostThemeAccess.Everyone => true,
            GhostThemeAccess.Supporters => _supporters.IsSupporter(player),
            GhostThemeAccess.Contributors => _contributors.IsContributor(player),
            GhostThemeAccess.Admins => _admin.IsAdmin(player, includeDeAdmin: true),
            _ => false,
        };

        return access && HasPlaytime(player, theme.Playtime);
    }

    /// <summary>
    /// The themes the player can use right now.
    /// </summary>
    public HashSet<ProtoId<GhostThemePrototype>> GetAvailable(ICommonSession player)
    {
        var available = new HashSet<ProtoId<GhostThemePrototype>>();
        foreach (var theme in _proto.EnumeratePrototypes<GhostThemePrototype>())
        {
            if (IsAvailable(player, theme))
                available.Add(theme.ID);
        }

        return available;
    }

    private bool HasPlaytime(ICommonSession player, TimeSpan needed)
    {
        // Off on test and development servers, like the playtime requirements of jobs.
        if (needed <= TimeSpan.Zero || !_cfg.GetCVar(CCVars.GameRoleTimers))
            return true;

        // Until the playtime is loaded the player has none.
        return _playTime.TryGetTrackerTimes(player, out var times)
            && times.GetValueOrDefault(PlayTimeTrackingShared.TrackerOverall) >= needed;
    }

    private void OnRequest(RequestGhostThemesEvent msg, EntitySessionEventArgs args)
    {
        var available = _enabled ? GetAvailable(args.SenderSession) : new HashSet<ProtoId<GhostThemePrototype>>();
        RaiseNetworkEvent(new GhostThemesAvailableEvent(available), args.SenderSession);
    }

    private void OnSelect(SelectGhostThemeEvent msg, EntitySessionEventArgs args)
    {
        if (!_enabled
            || args.SenderSession.AttachedEntity is not { } uid
            || !TryComp<GhostComponent>(uid, out var ghost))
            return;

        // The client only offers the themes it was told about, but the list may be stale, e.g. a supporter whose support ran out.
        if (string.IsNullOrEmpty(msg.Theme.Id)
            || !_proto.TryIndex(msg.Theme, out var theme)
            || !IsAvailable(args.SenderSession, theme))
            return;

        if (!float.IsFinite(msg.Color.R) || !float.IsFinite(msg.Color.G) || !float.IsFinite(msg.Color.B))
        {
            Log.Warning($"{args.SenderSession} sent an invalid ghost theme color {msg.Color}");
            return;
        }

        // A see-through color would let a player hide their ghost from other ghosts.
        var color = theme.Colorizable
            ? new Color(Math.Clamp(msg.Color.R, 0f, 1f), Math.Clamp(msg.Color.G, 0f, 1f), Math.Clamp(msg.Color.B, 0f, 1f))
            : Color.White;

        var comp = EnsureComp<GhostThemeComponent>(uid);
        comp.Theme = theme.ID;
        comp.Color = color;
        Dirty(uid, comp);

        // The default sprite is colored by the ghost itself, which the client keeps reapplying.
        // Other themes hide that sprite, so the color goes back to white for when themes are switched off.
        _ghost.SetColor((uid, ghost), theme.KeepGhostSprite ? color : Color.White);
    }

    private void OnEnabledChanged(bool enabled)
    {
        _enabled = enabled;
        if (enabled)
            return;

        var query = EntityQueryEnumerator<GhostThemeComponent, GhostComponent>();
        while (query.MoveNext(out var uid, out _, out var ghost))
        {
            _ghost.SetColor((uid, ghost), Color.White);
            RemCompDeferred<GhostThemeComponent>(uid);
        }
    }
}
