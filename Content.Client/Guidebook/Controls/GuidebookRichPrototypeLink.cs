// SPDX-FileCopyrightText: 2025 Ciarán Walsh <github@ciaranwal.sh>
// SPDX-FileCopyrightText: 2025 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Guidebook.RichText;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Client.UserInterface.ControlExtensions;

namespace Content.Client.Guidebook.Controls;

/// <summary>
/// A RichTextLabel which is a link to a specified IPrototype.
/// The link is activated by the owner if the prototype is represented
/// somewhere in the same document.
/// </summary>
public sealed class GuidebookRichPrototypeLink : RichTextLabel, IPrototypeLinkControl
{
    private bool _linkActive;
    private FormattedMessage? _message;

    public IPrototype? LinkedPrototype { get; set; }

    public GuidebookRichPrototypeLink()
    {
        MouseFilter = MouseFilterMode.Stop;
    }

    public void EnablePrototypeLink()
    {
        if (_message == null)
            return;

        _linkActive = true;
        DefaultCursorShape = CursorShape.Hand;
        SetMessage(_message, null, TextLinkTag.LinkColor);
    }

    public new void SetMessage(FormattedMessage message)
    {
        _message = message;
        base.SetMessage(message);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (!_linkActive)
            return;

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (this.TryGetParentHandler<IAnchorClickHandler>(out var handler))
        {
            handler.HandleAnchor(this);
            args.Handle();
        }
        else
            Logger.Warning("Warning! No valid IAnchorClickHandler found.");
    }
}

public interface IAnchorClickHandler
{
    public void HandleAnchor(IPrototypeLinkControl prototypeLinkControl);
}
