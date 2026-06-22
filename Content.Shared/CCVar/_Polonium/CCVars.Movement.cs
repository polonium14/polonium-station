// SPDX-FileCopyrightText: 2024 nikitosych <admin@ss14.pl>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Delay between sprints. Zero means there's no delay.
    /// </summary>
    public static readonly CVarDef<float> SecondsBetweenSprints =
        CVarDef.Create("movement.seconds_between_sprints", 0f, CVar.SERVER | CVar.REPLICATED);
}
