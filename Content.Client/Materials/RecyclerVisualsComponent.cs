// SPDX-FileCopyrightText: 2024 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 InsoPL <lukasz.lindert@protonmail.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
// SPDX-FileCopyrightText: 2026 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Client.Materials;

[RegisterComponent]
public sealed partial class RecyclerVisualsComponent : Component
{
    /// <summary>
    /// Key appended to state string if bloody.
    /// </summary>
    [DataField]
    public string BloodyKey = "bld";

    /// <summary>
    /// Base key for the visual state.
    /// </summary>
    [DataField]
    public string BaseKey = "recycler-o";
}
