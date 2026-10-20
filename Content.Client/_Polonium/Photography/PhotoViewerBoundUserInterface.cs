using System;
using Content.Shared._Polonium.Photography;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Photography;

[UsedImplicitly]
public sealed class PhotoViewerBoundUserInterface : BoundUserInterface
{
    private PhotoViewerWindow? _window;

    /// <summary>Pen the server offered for signing, echoed back so it can re-validate it.</summary>
    private NetEntity? _pen;

    public PhotoViewerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<PhotoViewerWindow>();
        _window.OnSign += caption =>
        {
            if (_pen is { } pen)
                SendMessage(new PhotoSignMessage(pen, caption));
        };
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        if (message is not PhotoSignStartMessage start)
            return;

        _pen = start.Pen;
        _window?.BeginSign(start.Color, start.Font);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is PhotoViewerBoundUserInterfaceState cast)
            _window?.Populate(cast);
    }
}
