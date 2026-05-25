// SPDX-FileCopyrightText: 2025 Polonium Station Contributors
//
// SPDX-License-Identifier: MIT

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
        ILocalizationManager loc)
    {
        statusLabel.Text = loc.GetString("ui-bql-results-status", ("count", entities.Length));
        itemList.RemoveAllChildren();

        foreach (var (name, entity) in entities)
        {
            var nameLabel = new Label { Text = name, HorizontalExpand = true };
            var tpButton = new Button { Text = loc.GetString("ui-bql-results-tp") };
            tpButton.OnPressed += _ => console.ExecuteCommand($"tpto {entity}");
            tpButton.ToolTip = loc.GetString("ui-bql-results-tp-tooltip");

            var vvButton = new Button { Text = loc.GetString("ui-bql-results-vv") };
            vvButton.ToolTip = loc.GetString("ui-bql-results-vv-tooltip");
            vvButton.OnPressed += _ => console.ExecuteCommand($"vv {entity}");

            itemList.AddChild(new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                Children = { nameLabel, tpButton, vvButton }
            });
        }
    }
}
