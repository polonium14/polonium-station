// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Client.Paper.UI;
using Content.Shared._Polonium.Paper;
using Content.Shared.Paper;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using static Content.Shared.Paper.PaperComponent;

namespace Content.Client._Polonium.Paper;

/// <summary>
///     Client half of the signature placement flow. When the server asks this
///     client to place a signature, it opens the signature placement gizmo on
///     the paper's UI (once that UI is open).
/// </summary>
public sealed partial class SignatureUiSystem : SharedSignatureSystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IPlayerManager _player = default!;

    private struct Pending
    {
        public EntityUid Pen;
        public float Elapsed;
    }

    private readonly Dictionary<EntityUid, Pending> _pending = new();

    private const float PendingTimeout = 2f;

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

        _pending[paper.Value] = new Pending { Pen = pen.Value };
        TryBeginPlacement(paper.Value);
    }

    private void OnBuiOpened(Entity<PaperComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is PaperUiKey.Key)
            TryBeginPlacement(ent.Owner);
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

        if (!TryComp<SignatureWriterComponent>(pending.Pen, out var signatureComp))
            return true;

        if (_player.LocalEntity is not { } signer)
            return true;

        var info = BuildSignatureInfo(signer, pending.Pen, signatureComp);
        bui.BeginSignaturePlacement(pending.Pen, info);
        return true;
    }
}
