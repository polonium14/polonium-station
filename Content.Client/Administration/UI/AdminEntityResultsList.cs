using Robust.Client.Console;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Administration.UI;

public static class AdminEntityResultsList
{
    public static void Populate(
        BoxContainer itemList,
        Label statusLabel,
        (string name, NetEntity entity)[] entities,
        IClientConsoleHost console,
        ILocalizationManager loc,
        bool hasMore = false)
    {
        itemList.RemoveAllChildren();
        Append(itemList, entities, console, loc);
        UpdateStatus(statusLabel, entities.Length, hasMore, loc);
    }

    public static void Append(
        BoxContainer itemList,
        (string name, NetEntity entity)[] entities,
        IClientConsoleHost console,
        ILocalizationManager loc)
    {
        foreach (var (name, entity) in entities)
        {
            itemList.AddChild(CreateRow(name, entity, console, loc));
        }
    }

    public static void UpdateStatus(Label statusLabel, int count, bool hasMore, ILocalizationManager loc)
    {
        statusLabel.Text = hasMore
            ? loc.GetString("ui-bql-results-status-more", ("count", count))
            : loc.GetString("ui-bql-results-status", ("count", count));
    }

    private static BoxContainer CreateRow(
        string name,
        NetEntity entity,
        IClientConsoleHost console,
        ILocalizationManager loc)
    {
        var nameLabel = new Label { Text = name, HorizontalExpand = true };
        var tpButton = new Button { Text = loc.GetString("ui-bql-results-tp") };
        tpButton.OnPressed += _ => console.ExecuteCommand($"tpto {entity}");
        tpButton.ToolTip = loc.GetString("ui-bql-results-tp-tooltip");

        var vvButton = new Button { Text = loc.GetString("ui-bql-results-vv") };
        vvButton.ToolTip = loc.GetString("ui-bql-results-vv-tooltip");
        vvButton.OnPressed += _ => console.ExecuteCommand($"vv {entity}");

        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Children = { nameLabel, tpButton, vvButton }
        };
    }
}
