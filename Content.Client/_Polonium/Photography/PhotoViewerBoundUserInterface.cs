using System;
using Content.Shared._Polonium.Photography;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Photography;

[UsedImplicitly]
public sealed class PhotoViewerBoundUserInterface : BoundUserInterface
{
    private PhotoViewerWindow? _window;

    public bool IsSigning => _window?.IsSigning ?? false;

    public PhotoViewerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<PhotoViewerWindow>();
        _window.OnSign += caption => SendMessage(new PhotoSignMessage(caption));

        // Opened by a pen? The server answers with PhotoSignStartMessage if so.
        SendMessage(new PhotoSignQueryMessage());
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        if (message is not PhotoSignStartMessage start)
            return;

        _window?.BeginSign(start.Color, start.Font);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is PhotoViewerBoundUserInterfaceState cast)
            _window?.Populate(cast);
    }
}
