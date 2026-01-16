// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server.Administration.Components;

[RegisterComponent]
public sealed partial class AdminAlertOnUseComponent : Component
{
    [DataField]
    public string Message = string.Empty;
}
