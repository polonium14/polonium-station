using Content.Shared._Polonium.GhostThemes;
using Content.Shared.CCVar;
using Content.Shared.Ghost.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.GhostThemes;

/// <summary>
/// Draws ghost themes and keeps the player's pick, which is sent to the server every time the player becomes a ghost.
/// </summary>
public sealed partial class GhostThemeSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    /// <summary>
    /// The theme used when the player has not picked one.
    /// </summary>
    public static readonly ProtoId<GhostThemePrototype> DefaultTheme = "Default";

    // The layer of the default ghost sprite in MobObserverBase.
    private const string GhostLayer = "ghostVariant";
    private const string UnshadedShader = "unshaded";

    /// <summary>
    /// Raised when the server says which themes the player can use.
    /// </summary>
    public event Action<HashSet<ProtoId<GhostThemePrototype>>>? ThemesReceived;

    /// <summary>
    /// Raised when ghost themes are switched on or off on the server.
    /// </summary>
    public event Action<bool>? EnabledChanged;

    /// <summary>
    /// Whether ghost themes are on on the server.
    /// </summary>
    public bool Enabled { get; private set; }

    /// <summary>
    /// The theme the player picked.
    /// </summary>
    public ProtoId<GhostThemePrototype> SavedTheme
    {
        get
        {
            var theme = _cfg.GetCVar(CCVars.GhostThemeSelected);
            return theme.Length > 0 ? theme : DefaultTheme;
        }
    }

    /// <summary>
    /// The color the player picked.
    /// </summary>
    public Color SavedColor => Color.TryFromHex(_cfg.GetCVar(CCVars.GhostThemeColor), out var color) ? color : Color.White;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GhostThemeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<GhostThemeComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<GhostThemeComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalPlayerAttached);
        SubscribeNetworkEvent<GhostThemesAvailableEvent>(OnThemesAvailable);

        Subs.CVar(_cfg, CCVars.GhostThemesEnabled, OnEnabledChanged, true);
    }

    /// <summary>
    /// Asks the server which themes the player can use. The answer comes in <see cref="ThemesReceived"/>.
    /// </summary>
    public void RequestThemes()
    {
        RaiseNetworkEvent(new RequestGhostThemesEvent());
    }

    /// <summary>
    /// Saves the player's pick and puts it on their ghost.
    /// </summary>
    public void ChooseTheme(ProtoId<GhostThemePrototype> theme, Color color)
    {
        // The default ghost is saved as nothing, so the player gets whatever the default is later.
        _cfg.SetCVar(CCVars.GhostThemeSelected, theme == DefaultTheme ? string.Empty : theme.Id);
        _cfg.SetCVar(CCVars.GhostThemeColor, color.ToHex());
        _cfg.SaveToFile();

        RaiseNetworkEvent(new SelectGhostThemeEvent(theme, color));
    }

    /// <summary>
    /// Draws the theme on a sprite that has the default ghost layer, like a ghost or the preview in the theme window.
    /// </summary>
    public void ApplyTheme(Entity<SpriteComponent?> ent, GhostThemePrototype theme, Color color)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (_sprite.LayerMapTryGet(ent, GhostLayer, out var ghostLayer, false))
        {
            _sprite.LayerSetVisible(ent, ghostLayer, theme.KeepGhostSprite);
            if (theme.KeepGhostSprite)
                _sprite.LayerSetColor(ent, ghostLayer, color);
        }

        if (theme.KeepGhostSprite)
        {
            if (_sprite.LayerMapTryGet(ent, GhostThemeLayer.Theme, out var oldLayer, false))
                _sprite.LayerSetVisible(ent, oldLayer, false);
            return;
        }

        var layer = _sprite.LayerMapReserve(ent, GhostThemeLayer.Theme);
        _sprite.LayerSetRsi(ent, layer, theme.Sprite.RsiPath, theme.Sprite.RsiState);
        _sprite.LayerSetColor(ent, layer, color);
        ent.Comp.LayerSetShader(layer, UnshadedShader);
        _sprite.LayerSetVisible(ent, layer, true);
    }

    private void OnStartup(Entity<GhostThemeComponent> ent, ref ComponentStartup args)
    {
        Apply(ent);
    }

    private void OnState(Entity<GhostThemeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Apply(ent);
    }

    private void OnShutdown(Entity<GhostThemeComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent) || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        // Themes were switched off: back to the default ghost.
        if (_sprite.LayerMapTryGet((ent, sprite), GhostLayer, out var ghostLayer, false))
            _sprite.LayerSetVisible((ent, sprite), ghostLayer, true);

        if (_sprite.LayerMapTryGet((ent, sprite), GhostThemeLayer.Theme, out var themeLayer, false))
            _sprite.LayerSetVisible((ent, sprite), themeLayer, false);
    }

    private void Apply(Entity<GhostThemeComponent> ent)
    {
        if (!_proto.TryIndex(ent.Comp.Theme, out var theme))
            return;

        ApplyTheme(ent.Owner, theme, ent.Comp.Color);
    }

    private void OnLocalPlayerAttached(LocalPlayerAttachedEvent args)
    {
        if (HasComp<GhostComponent>(args.Entity))
            SendSavedTheme();
    }

    private void OnThemesAvailable(GhostThemesAvailableEvent msg)
    {
        ThemesReceived?.Invoke(msg.Available);
    }

    private void OnEnabledChanged(bool enabled)
    {
        Enabled = enabled;
        EnabledChanged?.Invoke(enabled);

        // Switched back on: the server dropped the themes, so the current ghost needs the pick again.
        if (enabled && HasComp<GhostComponent>(_player.LocalEntity))
            SendSavedTheme();
    }

    private void SendSavedTheme()
    {
        // Nothing picked means the default ghost, which the player already has.
        // Replays have no server to send it to.
        if (!Enabled || !_net.IsConnected || (_cfg.GetCVar(CCVars.GhostThemeSelected).Length == 0 && SavedColor == Color.White))
            return;

        RaiseNetworkEvent(new SelectGhostThemeEvent(SavedTheme, SavedColor));
    }

    private enum GhostThemeLayer : byte
    {
        Theme,
    }
}
