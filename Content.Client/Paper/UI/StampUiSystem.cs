using System.Linq;
using Content.Shared.Paper;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using static Content.Shared.Paper.PaperComponent;

namespace Content.Client.Paper.UI;

/// <summary>
///     Client half of the stamp placement flow. When the server asks this client
///     to place a stamp precisely, it opens the placement gizmo on the paper's UI
///     (once that UI is open). Mirrors <see cref="Content.Client._DV.Paper.SignatureUiSystem"/>,
///     but stamps place move + rotate only (no scale).
/// </summary>
public sealed class StampUiSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private readonly Dictionary<EntityUid, EntityUid> _pending = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PaperStampRequestEvent>(OnStampRequest);
        SubscribeLocalEvent<PaperComponent, BoundUIOpenedEvent>(OnBuiOpened);
        SubscribeLocalEvent<PaperComponent, BoundUIClosedEvent>(OnBuiClosed);
    }

    private void OnStampRequest(PaperStampRequestEvent ev)
    {
        if (!TryGetEntity(ev.Paper, out var paper) || !TryGetEntity(ev.Stamp, out var stamp))
            return;

        _pending[paper.Value] = stamp.Value;
        TryBeginPlacement(paper.Value);
    }

    private void OnBuiOpened(Entity<PaperComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is PaperUiKey.Key)
            TryBeginPlacement(ent.Owner);
    }

    private void OnBuiClosed(Entity<PaperComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is PaperUiKey.Key)
            _pending.Remove(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pending.Count == 0)
            return;

        foreach (var paper in _pending.Keys.ToArray())
            TryBeginPlacement(paper);
    }

    private void TryBeginPlacement(EntityUid paper)
    {
        if (!_pending.TryGetValue(paper, out var stamp))
            return;

        if (!_ui.TryGetOpenUi<PaperBoundUserInterface>(paper, PaperUiKey.Key, out var bui))
            return;

        _pending.Remove(paper);

        if (!TryComp<StampComponent>(stamp, out var stampComp))
            return;

        var info = PaperSystem.GetStampInfo(stampComp);
        bui.BeginStampPlacement(stamp, info);
    }
}
