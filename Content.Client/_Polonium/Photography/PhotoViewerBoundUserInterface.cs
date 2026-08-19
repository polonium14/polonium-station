using System;
using Content.Shared._Polonium.Photography;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Photography;

[UsedImplicitly]
public sealed class PhotoViewerBoundUserInterface : BoundUserInterface
{
    private PhotoViewerWindow? _window;

    public PhotoViewerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<PhotoViewerWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is PhotoViewerBoundUserInterfaceState cast)
            _window?.Populate(cast.Data);
    }
}
