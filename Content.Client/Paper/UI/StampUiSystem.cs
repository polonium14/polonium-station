using System.Linq;
using Content.Shared.Paper;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using static Content.Shared.Paper.PaperComponent;

namespace Content.Client.Paper.UI;

/// <summary>
///     Client half of the stamp placement flow. When the server asks this client
///     to place a stamp precisely, it opens the placement gizmo on the paper's UI
///     (once that UI is open). Mirrors <see cref="Content.Client._Polonium.Paper.SignatureUiSystem"/>,
///     but stamps place move + rotate only (no scale).
/// </summary>
public sealed partial class StampUiSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;

    private struct Pending
    {
        public EntityUid Stamp;
        public float Elapsed;
    }

    private readonly Dictionary<EntityUid, Pending> _pending = new();

    private const float PendingTimeout = 2f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PaperStampRequestEvent>(OnStampRequest);
    }

    private void OnStampRequest(PaperStampRequestEvent ev)
    {
        if (!TryGetEntity(ev.Paper, out var paper) || !TryGetEntity(ev.Stamp, out var stamp))
            return;

        _pending[paper.Value] = new Pending { Stamp = stamp.Value };
        TryBeginPlacement(paper.Value);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pending.Count == 0)
            return;

        foreach (var paper in _pending.Keys.ToArray())
        {
            if (TryBeginPlacement(paper))
                continue;

            var pending = _pending[paper];
            pending.Elapsed += frameTime;
            if (pending.Elapsed >= PendingTimeout)
                _pending.Remove(paper);
            else
                _pending[paper] = pending;
        }
    }

    /// <summary>
    ///     Consumes the pending request for <paramref name="paper"/> if its UI is
    ///     open. Returns true if the request was consumed (or dropped), false if it
    ///     should keep waiting.
    /// </summary>
    private bool TryBeginPlacement(EntityUid paper)
    {
        if (!_pending.TryGetValue(paper, out var pending))
            return true;

        if (!_ui.TryGetOpenUi<PaperBoundUserInterface>(paper, PaperUiKey.Key, out var bui))
            return false;

        _pending.Remove(paper);

        if (!TryComp<StampComponent>(pending.Stamp, out var stampComp))
            return true;

        var info = PaperSystem.GetStampInfo(stampComp);
        bui.BeginStampPlacement(pending.Stamp, info);
        return true;
    }
}
