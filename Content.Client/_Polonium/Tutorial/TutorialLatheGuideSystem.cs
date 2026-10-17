using System.Linq;
using Content.Client._Polonium.Tutorial.UI;
using Content.Client._Polonium.UserInterface;
using Content.Client.Lathe.UI;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Tutorial;

/// <summary>
/// Keeps a parts list beside the lathe window on a step that names a machine. What is still missing is counted
/// from what the trainee carries, what lies by the lathe and what is already in the frame; those recipes glow,
/// and so does the amount field whenever more than one piece is needed.
/// </summary>
public sealed partial class TutorialLatheGuideSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IUserInterfaceManager _uiMan = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedLatheSystem _lathe = default!;
    [Dependency] private SharedMaterialStorageSystem _materials = default!;
    [Dependency] private TutorialItemCountSystem _count = default!;

    private enum Phase : byte
    {
        Load,
        Order,

        // past the last row, every row reads as done
        Done = 3,
    }

    private string? _step;
    private TutorialGuideCard? _card;
    private UiGlowFrames? _frames;

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
            || step.LatheGuide is not { } guide)
        {
            Reset();
            return;
        }

        // the card names its machine, so every step gets a fresh one
        if (_step != stepId.Id)
        {
            RemoveCard();
            _step = stepId.Id;
        }

        if (FindAnchor(player, guide.Lathe) is not { } lathe
            || !_ui.TryGetOpenUi<LatheBoundUserInterface>(lathe, LatheUiKey.Key, out var bui)
            || bui.Menu is not { IsOpen: true } menu)
        {
            Hide();
            return;
        }

        var places = new List<EntityUid> { lathe };
        foreach (var id in guide.CountAt)
        {
            if (FindAnchor(player, id) is { } place)
                places.Add(place);
        }

        var gathered = _count.Gather(player, places);

        var lines = new List<string>();
        var missing = new List<(TutorialLatheItem Item, int Left)>();
        foreach (var item in guide.Items)
        {
            var have = Math.Min(_count.CountRecipe(item.Recipe, gathered), item.Count);
            var done = have >= item.Count;

            lines.Add(Loc.GetString(done ? "tutorial-lathe-guide-item-done" : "tutorial-lathe-guide-item-missing",
                ("name", _lathe.GetRecipeName(item.Recipe)),
                ("have", have),
                ("need", item.Count)));

            if (!done)
                missing.Add((item, item.Count - have));
        }

        var phase = _materials.GetTotalMaterialAmount(lathe) <= 0 ? Phase.Load
            : missing.Count > 0 ? Phase.Order
            : Phase.Done;

        var targets = new List<Control>();
        string? warning = null;
        if (phase == Phase.Order)
        {
            foreach (var control in Descendants<RecipeControl>(menu))
            {
                if (missing.Any(m => m.Item.Recipe == control.RecipeId))
                    targets.Add(control);
            }

            if (missing.Any(m => m.Left > 1)
                && Descendants<LineEdit>(menu).FirstOrDefault(edit => edit.Name == "AmountLineEdit") is { } amount)
                targets.Add(amount);

            foreach (var (item, left) in missing)
            {
                if (_lathe.CanProduce(lathe, item.Recipe, left))
                    continue;

                warning = Loc.GetString("tutorial-lathe-guide-need-materials", ("name", _lathe.GetRecipeName(item.Recipe)));
                break;
            }
        }

        EnsureFrames().SetTargets(targets);

        var status = phase switch
        {
            Phase.Load => "tutorial-lathe-guide-status-load",
            Phase.Order => "tutorial-lathe-guide-status-order",
            _ => "tutorial-lathe-guide-status-done",
        };

        var card = EnsureCard(guide);
        card.SetActiveStep((int) phase);
        card.SetStatus(Loc.GetString(status), string.Join("\n", lines), warning);
        card.PlaceBeside(menu);
    }

    private static IEnumerable<T> Descendants<T>(Control root) where T : Control
    {
        foreach (var child in root.Children)
        {
            if (child is T found)
                yield return found;

            foreach (var deeper in Descendants<T>(child))
                yield return deeper;
        }
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

    private UiGlowFrames EnsureFrames()
    {
        if (_frames != null)
            return _frames;

        _frames = new UiGlowFrames();
        _uiMan.PopupRoot.AddChild(_frames);
        LayoutContainer.SetAnchorPreset(_frames, LayoutContainer.LayoutPreset.Wide);
        return _frames;
    }

    private TutorialGuideCard EnsureCard(TutorialLatheGuide guide)
    {
        if (_card == null)
        {
            _card = new TutorialGuideCard(_cache,
                Loc.GetString("tutorial-lathe-guide-title"),
                Loc.GetString("tutorial-lathe-guide-goal", ("machine", Loc.GetString(guide.Machine))));

            _card.AddStep(null,
                Loc.GetString("tutorial-lathe-guide-step-load"),
                Loc.GetString("tutorial-lathe-guide-step-load-body"));
            _card.AddStep(null,
                Loc.GetString("tutorial-lathe-guide-step-order"),
                Loc.GetString("tutorial-lathe-guide-step-order-body"));
            _card.AddStep(null,
                Loc.GetString("tutorial-lathe-guide-step-take"),
                Loc.GetString("tutorial-lathe-guide-step-take-body"));

            _uiMan.PopupRoot.AddChild(_card);
        }

        _card.Visible = true;
        return _card;
    }

    private void Hide()
    {
        _frames?.SetTargets(Array.Empty<Control>());

        if (_card != null)
            _card.Visible = false;
    }

    private void RemoveCard()
    {
        _card?.Orphan();
        _card = null;
    }

    private void Reset()
    {
        RemoveCard();
        _frames?.Orphan();
        _frames = null;
        _step = null;
    }
}
