using Content.Client.Paper.UI;
using Content.Shared._DV.Paper;
using Content.Shared.Paper;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using static Content.Shared.Paper.PaperComponent;

namespace Content.Client._DV.Paper;

/// <summary>
///     Client half of the signature placement flow. When the server asks this
///     client to place a signature, it opens the signature placement gizmo on
///     the paper's UI (once that UI is open).
/// </summary>
public sealed class SignatureUiSystem : SharedSignatureSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly Dictionary<EntityUid, EntityUid> _pending = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PaperSignRequestEvent>(OnSignRequest);
        SubscribeLocalEvent<PaperComponent, BoundUIOpenedEvent>(OnBuiOpened);
        SubscribeLocalEvent<PaperComponent, BoundUIClosedEvent>(OnBuiClosed);
    }

    private void OnBuiClosed(Entity<PaperComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is PaperUiKey.Key)
            _pending.Remove(ent.Owner);
    }

    private void OnSignRequest(PaperSignRequestEvent ev)
    {
        if (!TryGetEntity(ev.Paper, out var paper) || !TryGetEntity(ev.Pen, out var pen))
            return;

        _pending[paper.Value] = pen.Value;
        TryBeginPlacement(paper.Value);
    }

    private void OnBuiOpened(Entity<PaperComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is PaperUiKey.Key)
            TryBeginPlacement(ent.Owner);
    }

    private void TryBeginPlacement(EntityUid paper)
    {
        if (!_pending.TryGetValue(paper, out var pen))
            return;

        if (!_ui.TryGetOpenUi<PaperBoundUserInterface>(paper, PaperUiKey.Key, out var bui))
            return;

        _pending.Remove(paper);

        if (!TryComp<SignatureWriterComponent>(pen, out var signatureComp))
            return;

        if (_player.LocalEntity is not { } signer)
            return;

        var info = BuildSignatureInfo(signer, pen, signatureComp);
        bui.BeginSignaturePlacement(pen, info);
    }
}
