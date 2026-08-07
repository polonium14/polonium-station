using Content.Client.Administration.UI.CustomControls;
using Content.Client.ContextMenu.UI;
using Content.Client.Resources;
using Robust.Client.Console;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Administration.UI;

public static class AdminEntityResultsList
{
    private const float IdRatio = 2f;
    private const float NameRatio = 5f;
    private const float ProtoRatio = 4f;
    private const float ActionsRatio = 3f;

    private const string IconsPath = "/Textures/Interface/VerbIcons/";

    public static void PopulateHeader(BoxContainer header, ILocalizationManager loc)
    {
        header.Orientation = BoxContainer.LayoutOrientation.Horizontal;
        header.RemoveAllChildren();
        header.AddChild(StretchLabel(loc.GetString("ui-bql-results-col-id"), IdRatio));
        header.AddChild(new VSeparator());
        header.AddChild(StretchLabel(loc.GetString("ui-bql-results-col-name"), NameRatio));
        header.AddChild(new VSeparator());
        header.AddChild(StretchLabel(loc.GetString("ui-bql-results-col-proto"), ProtoRatio));
        header.AddChild(new VSeparator());
        header.AddChild(StretchLabel(loc.GetString("ui-bql-results-col-actions"), ActionsRatio));
    }

    public static void Populate(
        BoxContainer itemList,
        Label statusLabel,
        (string name, string? proto, NetEntity entity)[] entities,
        IClientConsoleHost console,
        ILocalizationManager loc,
        IClipboardManager clipboard,
        IResourceCache resCache,
        bool hasMore = false,
        int? total = null,
        bool allowDelete = false)
    {
        itemList.RemoveAllChildren();
        Append(itemList, entities, console, loc, clipboard, resCache, allowDelete);
        UpdateStatus(statusLabel, entities.Length, hasMore, loc, total);
    }

    public static void Append(
        BoxContainer itemList,
        (string name, string? proto, NetEntity entity)[] entities,
        IClientConsoleHost console,
        ILocalizationManager loc,
        IClipboardManager clipboard,
        IResourceCache resCache,
        bool allowDelete = false)
    {
        foreach (var (name, proto, entity) in entities)
        {
            itemList.AddChild(CreateRow(name, proto, entity, console, loc, clipboard, resCache, allowDelete));
        }
    }

    public static void UpdateStatus(Label statusLabel, int count, bool hasMore, ILocalizationManager loc, int? total = null)
    {
        if (total is { } t)
        {
            statusLabel.Text = loc.GetString("ui-bql-results-status-total", ("loaded", count), ("total", t));
            return;
        }

        statusLabel.Text = hasMore
            ? loc.GetString("ui-bql-results-status-more", ("count", count))
            : loc.GetString("ui-bql-results-status", ("count", count));
    }

    private static Label StretchLabel(string text, float ratio) => new()
    {
        Text = text,
        HorizontalExpand = true,
        SizeFlagsStretchRatio = ratio,
        ClipText = true,
    };

    private static BoxContainer CreateRow(
        string name,
        string? proto,
        NetEntity entity,
        IClientConsoleHost console,
        ILocalizationManager loc,
        IClipboardManager clipboard,
        IResourceCache resCache,
        bool allowDelete)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        row.AddChild(StretchLabel(entity.ToString(), IdRatio));
        row.AddChild(new VSeparator());
        row.AddChild(StretchLabel(name, NameRatio));
        row.AddChild(new VSeparator());
        row.AddChild(StretchLabel(proto ?? string.Empty, ProtoRatio));
        row.AddChild(new VSeparator());
        row.AddChild(CreateActions(entity, console, loc, clipboard, resCache, allowDelete, () => row.Orphan()));
        return row;
    }

    private static Button CreateActions(
        NetEntity entity,
        IClientConsoleHost console,
        ILocalizationManager loc,
        IClipboardManager clipboard,
        IResourceCache resCache,
        bool allowDelete,
        Action onDeleted)
    {
        var button = new Button
        {
            Text = loc.GetString("ui-bql-results-actions"),
            HorizontalExpand = true,
            SizeFlagsStretchRatio = ActionsRatio,
        };
        button.OnPressed += _ => OpenActionsMenu(entity, console, loc, clipboard, resCache, allowDelete, onDeleted);
        return button;
    }

    private static void OpenActionsMenu(
        NetEntity entity,
        IClientConsoleHost console,
        ILocalizationManager loc,
        IClipboardManager clipboard,
        IResourceCache resCache,
        bool allowDelete,
        Action onDeleted)
    {
        Texture Icon(string file) => resCache.GetTexture(IconsPath + file);

        var popup = new Popup();
        var panel = new PanelContainer();
        panel.SetOnlyStyleClass(ContextMenuPopup.StyleClassContextMenuPopup);
        var menu = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        panel.AddChild(menu);
        popup.AddChild(panel);

        menu.AddChild(MenuElement(loc.GetString("ui-bql-results-follow"), Icon("human-head-eyes.svg.192dpi.png"),
            () => { console.ExecuteCommand($"follow \"{entity}\""); popup.Close(); }));
        menu.AddChild(MenuElement(loc.GetString("ui-bql-results-vv"), Icon("vv.svg.192dpi.png"),
            () => { console.ExecuteCommand($"vv {entity}"); popup.Close(); }));
        menu.AddChild(MenuElement(loc.GetString("ui-bql-results-copy"), Icon("information.svg.192dpi.png"),
            () => { clipboard.SetText(entity.ToString()); popup.Close(); }));

        // Delete is opt-in (default off)
        if (allowDelete)
        {
            // Delete arms inline: first press swaps to red "Confirm?", second press deletes. Menu stays open.
            var delete = new AdminActionMenuElement(RedText(loc.GetString("ui-bql-results-delete")), Icon("delete.svg.192dpi.png"));
            var armed = false;
            delete.OnPressed += _ =>
            {
                if (!armed)
                {
                    armed = true;
                    delete.Text = RedText(loc.GetString("ui-bql-results-delete-confirm"));
                    return;
                }

                console.ExecuteCommand($"delete {entity}");
                onDeleted();
                popup.Close();
            };
            menu.AddChild(delete);
        }

        popup.OpenAtMouse();
    }

    private static AdminActionMenuElement MenuElement(string text, Texture? icon, Action onPressed)
    {
        var element = new AdminActionMenuElement(text, icon);
        element.OnPressed += _ => onPressed();
        return element;
    }

    private static string RedText(string text) => $"[color=#ff4040]{text}[/color]";
}
