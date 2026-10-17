using Content.Client.Markers;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Tutorial;

/// <summary>
/// Puts a white pulsing glow on whatever the current step told the player to touch.
/// Items get the loud version, doors/crates/machines a quieter one so the room doesn't strobe.
/// </summary>
public sealed partial class TutorialHighlightSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    // has to be unique against ContentPostShaderIds
    private const string PostShaderId = "tutorial-highlight";

    private static readonly ProtoId<ShaderPrototype> GlowShader = "PoloniumTutorialHighlight";

    private readonly Dictionary<EntityUid, bool> _glowing = new();

    // this runs every frame, so the working sets are kept around instead of rebuilt
    private readonly Dictionary<EntityUid, bool> _wanted = new();
    private readonly List<EntityUid> _dropped = new();

    private ShaderInstance? _itemShader;
    private ShaderInstance? _targetShader;

    public override void Shutdown()
    {
        ClearAll();

        _itemShader?.Dispose();
        _targetShader?.Dispose();
        _itemShader = null;
        _targetShader = null;

        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_player.LocalEntity is not { } player
            || !TryComp<TutorialSessionComponent>(player, out var session)
            || session.HighlightAnchors.Count == 0)
        {
            ClearAll();
            return;
        }

        if (!TryComp(player, out TransformComponent? xform) || xform.GridUid is not { } grid)
        {
            ClearAll();
            return;
        }

        _wanted.Clear();

        // a shooting drill lights one target at a time, the rest of the row waits its turn
        string? focusRow = null;
        if (session.FocusTarget is { } focus && TryComp<TutorialAnchorComponent>(focus, out var focusAnchor))
            focusRow = focusAnchor.AnchorId;

        var query = EntityQueryEnumerator<TutorialAnchorComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var anchor, out _, out var ax))
        {
            if (ax.GridUid != grid)
                continue;

            if (!session.HighlightAnchors.Contains(anchor.AnchorId))
                continue;

            if (anchor.AnchorId == focusRow && uid != session.FocusTarget)
                continue;

            // pink crosses and other mapping helpers stay invisible
            if (HasComp<MarkerComponent>(uid))
                continue;

            // once it is under you or in your hand the glow has done its job and just nags
            if (IsAlreadyTaken(player, uid))
                continue;

            _wanted[uid] = HasComp<ItemComponent>(uid);
        }

        foreach (var (uid, isItem) in _wanted)
        {
            if (_glowing.TryGetValue(uid, out var current) && current == isItem)
                continue;

            Apply(uid, isItem);
        }

        _dropped.Clear();
        foreach (var uid in _glowing.Keys)
        {
            if (!_wanted.ContainsKey(uid))
                _dropped.Add(uid);
        }

        foreach (var uid in _dropped)
            Clear(uid);
    }

    private bool IsAlreadyTaken(EntityUid player, EntityUid target)
    {
        if (TryComp<BuckleComponent>(player, out var buckle) && buckle.BuckledTo == target)
            return true;

        foreach (var held in _hands.EnumerateHeld(player))
        {
            if (held == target)
                return true;
        }

        return false;
    }

    private void Apply(EntityUid uid, bool isItem)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var shader = isItem ? EnsureItemShader() : EnsureTargetShader();

        _sprite.SetPostShader((uid, sprite), new SpriteComponent.PostShaderArgs(PostShaderId, shader));
        _glowing[uid] = isItem;
    }

    private void Clear(EntityUid uid)
    {
        _glowing.Remove(uid);

        if (TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.RemovePostShader((uid, sprite), PostShaderId);
    }

    private void ClearAll()
    {
        if (_glowing.Count == 0)
            return;

        _dropped.Clear();
        _dropped.AddRange(_glowing.Keys);

        foreach (var uid in _dropped)
            Clear(uid);
    }

    private ShaderInstance EnsureItemShader()
    {
        if (_itemShader != null)
            return _itemShader;

        _itemShader = _proto.Index(GlowShader).InstanceUnique();
        _itemShader.SetParameter("glow_color", Color.White);
        _itemShader.SetParameter("glow_speed", 3.4f);
        _itemShader.SetParameter("glow_min", 0.18f);
        _itemShader.SetParameter("glow_max", 0.7f);
        _itemShader.SetParameter("glow_width", 1.5f);
        return _itemShader;
    }

    private ShaderInstance EnsureTargetShader()
    {
        if (_targetShader != null)
            return _targetShader;

        _targetShader = _proto.Index(GlowShader).InstanceUnique();
        _targetShader.SetParameter("glow_color", Color.White);
        _targetShader.SetParameter("glow_speed", 2.4f);
        _targetShader.SetParameter("glow_min", 0.08f);
        _targetShader.SetParameter("glow_max", 0.34f);
        _targetShader.SetParameter("glow_width", 1f);
        return _targetShader;
    }
}
