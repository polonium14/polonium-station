using System.Linq;
using System.Numerics;
using Content.Client._Polonium.Tutorial.UI;
using Content.Client.Resources;
using Content.Client.Wires.UI;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Power;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Wires;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Tutorial;

public enum WiresGuidePhase : byte
{
    /// <summary>Pulse contacts until the lights go out.</summary>
    Find,

    /// <summary>A pulse took the power, the crowbar gets in right now.</summary>
    Window,

    /// <summary>A power wire is known and the power is back: pulse it again, or cut both for good.</summary>
    Found,

    /// <summary>Both power wires are cut, the door stays dead until someone mends them.</summary>
    Dead,
}

/// <summary>
/// Walks the trainee through getting past an airlock inside the real wires window. The quick way is the
/// stock one: a pulse on a power wire kills the door for 15 or 30 seconds, and an unpowered door gives to a
/// crowbar in a second and a half. Cutting both power wires is shown as the permanent way. The server only
/// waits for the door to open; which wire is power and whether the door is dark is read here from the window.
/// </summary>
public sealed partial class TutorialWiresGuideSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IUserInterfaceManager _uiMan = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    private static readonly ProtoId<ToolQualityPrototype> Pulsing = "Pulsing";
    private static readonly ProtoId<ToolQualityPrototype> Prying = "Prying";

    private static readonly EntProtoId MultitoolIcon = "Multitool";
    private static readonly EntProtoId CrowbarIcon = "Crowbar";

    private const string ContactTexture = "/Textures/Interface/WireHacking/contact.svg.96dpi.png";
    private const string WireTexture = "/Textures/Interface/WireHacking/wire_1.svg.96dpi.png";

    // an airlock layout carries a main and a backup power wire
    private const int PowerWireCount = 2;

    // a wire action lands after a short tool do-after, a light change much later is not its doing
    private static readonly TimeSpan ActionWindow = TimeSpan.FromSeconds(4);

    // the longer of the two airlock pulses keeps the lights off for 30 seconds, dark past that is for good
    private static readonly TimeSpan DarkForGood = TimeSpan.FromSeconds(33);

    private string? _anchor;
    private EntityUid? _door;
    private WiresMenu? _menu;
    private TutorialGuideCard? _card;

    private readonly HashSet<int> _powerWires = new();
    private int? _lastWire;
    private TimeSpan _lastActionAt;
    private StatusLightState? _lastLight;
    private TimeSpan? _darkSince;

    public override void Shutdown()
    {
        Reset();
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_player.LocalEntity is not { } player
            || !TryComp<TutorialSessionComponent>(player, out var session)
            || session.CurrentStep is not { } stepId
            || !_proto.TryIndex(stepId, out var step)
            || step.WiresGuideAnchor is not { } anchor)
        {
            Reset();
            return;
        }

        if (anchor != _anchor)
        {
            Reset();
            _anchor = anchor;
        }

        if (_door is not { } known || Deleted(known))
            _door = FindAnchor(player, anchor);

        if (_door is not { } door
            || !_ui.TryGetOpenUi<WiresBoundUserInterface>(door, WiresUiKey.Key, out var bui)
            || bui.Menu is not { IsOpen: true } menu)
        {
            Detach();
            return;
        }

        if (!ReferenceEquals(menu, _menu))
            Attach(menu);

        if (menu.LastState is not { } state)
            return;

        var light = PowerLight(state);
        _darkSince = light == StatusLightState.Off ? _darkSince ?? _timing.RealTime : null;

        var cut = CutPowerWires(state);
        var phase = PickPhase(light, cut);

        ApplyGlow(menu, state, phase, cut);

        var card = EnsureCard();
        card.SetActiveStep(phase == WiresGuidePhase.Find ? 0 : 1);

        var (status, progress) = phase switch
        {
            WiresGuidePhase.Find => (Loc.GetString("tutorial-wires-guide-status-find",
                ("power", Loc.GetString("wire-name-power"))), null),
            WiresGuidePhase.Window => (Loc.GetString("tutorial-wires-guide-status-window"),
                Loc.GetString("tutorial-wires-guide-dark-for",
                    ("seconds", (int) (_timing.RealTime - _darkSince!.Value).TotalSeconds))),
            WiresGuidePhase.Found when cut > 0 => (Loc.GetString("tutorial-wires-guide-status-one-cut"), null),
            WiresGuidePhase.Found => (Loc.GetString("tutorial-wires-guide-status-found"),
                Loc.GetString("tutorial-wires-guide-for-good")),
            _ => (Loc.GetString("tutorial-wires-guide-status-dead"), null),
        };

        card.SetStatus(status, progress, Warning(player, phase));
        card.PlaceBeside(menu);
    }

    private WiresGuidePhase PickPhase(StatusLightState? light, int cut)
    {
        if (cut >= PowerWireCount)
            return WiresGuidePhase.Dead;

        if (light == StatusLightState.Off)
        {
            // both got cut without us seeing which, the lights simply never come back
            return _darkSince is { } since && _timing.RealTime - since > DarkForGood
                ? WiresGuidePhase.Dead
                : WiresGuidePhase.Window;
        }

        return _powerWires.Count > 0 ? WiresGuidePhase.Found : WiresGuidePhase.Find;
    }

    private void ApplyGlow(WiresMenu menu, WiresBoundUserInterfaceState state, WiresGuidePhase phase, int cut)
    {
        var contacts = Enumerable.Empty<int>();
        var wires = Enumerable.Empty<int>();
        var statuses = Enumerable.Empty<object>();

        switch (phase)
        {
            case WiresGuidePhase.Find:
                contacts = UnknownContacts(state);
                statuses = new object[] { PowerWireActionKey.Status };
                break;
            case WiresGuidePhase.Window:
                statuses = new object[] { PowerWireActionKey.Status };
                break;
            case WiresGuidePhase.Found:
                // a pulse through any whole power wire still takes the door down, so point at the known one;
                // with it already cut the other one has to be found first
                var known = _powerWires.Where(id => !IsCut(state, id)).ToList();
                contacts = known.Count > 0 && cut == 0 ? known : UnknownContacts(state);
                break;
        }

        menu.SetTutorialGlow(contacts, wires, statuses);
    }

    private IEnumerable<int> UnknownContacts(WiresBoundUserInterfaceState state)
    {
        return state.WiresList
            .Where(w => !w.IsCut && !_powerWires.Contains(w.Id))
            .Select(w => w.Id);
    }

    private string? Warning(EntityUid player, WiresGuidePhase phase)
    {
        var (quality, message) = phase is WiresGuidePhase.Window or WiresGuidePhase.Dead
            ? (Prying, "tutorial-wires-guide-need-crowbar")
            : (Pulsing, "tutorial-wires-guide-need-multitool");

        if (_hands.TryGetActiveItem((player, null), out var held) && _tool.HasQuality(held.Value, quality.Id))
            return null;

        return Loc.GetString(message);
    }

    private void OnAction(int id, WiresAction action)
    {
        if (action is not (WiresAction.Pulse or WiresAction.Cut))
            return;

        _lastWire = id;
        _lastActionAt = _timing.RealTime;
    }

    private void OnPopulated(WiresBoundUserInterfaceState state)
    {
        var light = PowerLight(state);

        // the power light only ever goes dark, or starts blinking out of a steady glow, because a power
        // wire was pulsed or cut. no other wire in the airlock layout touches it
        if (_lastWire is { } id
            && _timing.RealTime - _lastActionAt < ActionWindow
            && _lastLight is { } before
            && light is { } after
            && before != after
            && (after == StatusLightState.Off
                || before == StatusLightState.On && after == StatusLightState.BlinkingSlow))
        {
            _powerWires.Add(id);
        }

        _lastLight = light;
    }

    private void Attach(WiresMenu menu)
    {
        Detach();

        _menu = menu;
        menu.OnAction += OnAction;
        menu.Populated += OnPopulated;
        _lastLight = menu.LastState is { } state ? PowerLight(state) : null;
    }

    private void Detach()
    {
        if (_menu != null)
        {
            _menu.OnAction -= OnAction;
            _menu.Populated -= OnPopulated;
            _menu.SetTutorialGlow(Array.Empty<int>(), Array.Empty<int>(), Array.Empty<object>());
            _menu = null;
        }

        if (_card != null)
            _card.Visible = false;
    }

    private void Reset()
    {
        Detach();
        _card?.Orphan();
        _card = null;

        _anchor = null;
        _door = null;
        _powerWires.Clear();
        _lastWire = null;
        _lastLight = null;
        _darkSince = null;
    }

    private TutorialGuideCard EnsureCard()
    {
        if (_card == null)
        {
            _card = new TutorialGuideCard(_cache,
                Loc.GetString("tutorial-wires-guide-title"),
                Loc.GetString("tutorial-wires-guide-goal"));

            _card.AddStep(MultitoolIcon,
                Loc.GetString("tutorial-wires-guide-step-find"),
                Loc.GetString("tutorial-wires-guide-step-find-body"));
            _card.AddStep(CrowbarIcon,
                Loc.GetString("tutorial-wires-guide-step-pry"),
                Loc.GetString("tutorial-wires-guide-step-pry-body"));

            _card.AddLegend(_cache.GetTexture(ContactTexture), Color.FromHex("#E1CA76"), new Vector2(16, 16),
                Loc.GetString("tutorial-wires-guide-legend-contact"));
            _card.AddLegend(_cache.GetTexture(WireTexture), Color.FromHex("#D24B4B"), new Vector2(16, 34),
                Loc.GetString("tutorial-wires-guide-legend-wire"));

            _uiMan.PopupRoot.AddChild(_card);
        }

        _card.Visible = true;
        return _card;
    }

    private EntityUid? FindAnchor(EntityUid player, string anchorId)
    {
        var grid = Transform(player).GridUid;
        var query = EntityQueryEnumerator<TutorialAnchorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var anchor, out var xform))
        {
            if (anchor.AnchorId == anchorId && xform.GridUid == grid)
                return uid;
        }

        return null;
    }

    private int CutPowerWires(WiresBoundUserInterfaceState state)
    {
        return _powerWires.Count(id => IsCut(state, id));
    }

    private static bool IsCut(WiresBoundUserInterfaceState state, int id)
    {
        return state.WiresList.Any(w => w.Id == id && w.IsCut);
    }

    private static StatusLightState? PowerLight(WiresBoundUserInterfaceState state)
    {
        foreach (var status in state.Statuses)
        {
            if (status.Key is PowerWireActionKey.Status && status.Value is StatusLightData data)
                return data.State;
        }

        return null;
    }
}
