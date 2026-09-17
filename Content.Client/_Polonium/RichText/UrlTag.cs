using System.Diagnostics.CodeAnalysis;
using Content.Shared._Polonium.Motd;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Input;
using Robust.Shared.Utility;

namespace Content.Client._Polonium.RichText;

[UsedImplicitly]
public sealed class UrlTag : IMarkupTagHandler
{
    [Dependency] private IUriOpener _uriOpener = default!;

    private static bool _allowed;

    public string Name => MotdHyperlinkMarkup.TagName;

    /// <summary>
    /// Turns [url] into a real link for this using-block.
    /// </summary>
    public static IDisposable Allow() => new AllowScope();

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        if (!_allowed
            || !TryGetLink(node, out var text, out var href)
            || !MotdHyperlinkMarkup.IsSafeHttpUrl(href))
        {
            control = null;
            return false;
        }

        var label = new LinkLabel
        {
            Text = text,
            MouseFilter = Control.MouseFilterMode.Stop,
            FontColorOverride = Color.CornflowerBlue,
            DefaultCursorShape = Control.CursorShape.Hand,
            ToolTip = href,
        };

        label.OnMouseEntered += _ => label.FontColorOverride = Color.LightSkyBlue;
        label.OnMouseExited += _ => label.FontColorOverride = Color.CornflowerBlue;
        label.OnKeyBindDown += args => OnKeybindDown(args, href);

        control = label;
        return true;
    }

    public string TextBefore(MarkupNode node)
    {
        if (!node.Value.TryGetString(out var text) || string.IsNullOrWhiteSpace(text))
            return "";

        if (!TryGetAttribute(node, "href", out var href) && !TryGetAttribute(node, "link", out href))
            return text;

        if (!MotdHyperlinkMarkup.IsSafeHttpUrl(href))
            return text;

        return "";
    }

    private void OnKeybindDown(GUIBoundKeyEventArgs args, string href)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        try
        {
            _uriOpener.OpenUri(href);
        }
        catch (ArgumentException)
        {
        }
    }

    private static bool TryGetLink(MarkupNode node, [NotNullWhen(true)] out string? text, [NotNullWhen(true)] out string? href)
    {
        text = null;
        href = null;

        if (!node.Value.TryGetString(out text) || string.IsNullOrWhiteSpace(text))
            return false;

        if (!TryGetAttribute(node, "href", out href) && !TryGetAttribute(node, "link", out href))
            return false;

        return !string.IsNullOrWhiteSpace(href);
    }

    private static bool TryGetAttribute(MarkupNode node, string name, [NotNullWhen(true)] out string? value)
    {
        if (node.Attributes.TryGetValue(name, out var parameter) && parameter.TryGetString(out value))
            return true;

        value = null;
        return false;
    }

    private sealed class AllowScope : IDisposable
    {
        private readonly bool _old = _allowed;

        public AllowScope() => _allowed = true;

        public void Dispose() => _allowed = _old;
    }
}
