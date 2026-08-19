using Content.Client.Stylesheets;
using Content.Shared.Item.ItemToggle;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Polonium.Photography;

/// <summary>In-hand item-status line for the camera's flash. Polls ItemToggle each frame so it updates live while held.</summary>
public sealed class CameraStatusControl : Control
{
    private readonly EntityUid _owner;
    private readonly IEntityManager _entMan;
    private readonly RichTextLabel _label;
    private bool? _lastState;

    public CameraStatusControl(EntityUid owner, IEntityManager entMan)
    {
        _owner = owner;
        _entMan = entMan;
        _label = new RichTextLabel { StyleClasses = { StyleNano.StyleClassItemStatus } };
        AddChild(_label);
        Update();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        Update();
    }

    private void Update()
    {
        var on = _entMan.System<ItemToggleSystem>().IsActivated(_owner);
        if (_lastState == on)
            return;

        _lastState = on;
        _label.SetMessage(FormattedMessage.FromMarkupOrThrow(Loc.GetString(on ? "camera-flash-on" : "camera-flash-off")));
    }
}
