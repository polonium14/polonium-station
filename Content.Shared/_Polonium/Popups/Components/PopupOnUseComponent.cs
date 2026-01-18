// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared.Popups.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PopupOnUseComponent : Component
{
    public const float UseDelaySeconds = 1f;

    [DataField(required: true)] public string Message = string.Empty;

    [DataField] public bool ShowToOthers = false;

    [DataField] public string PopupSize = "medium";

    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField] public TimeSpan LastUsed = TimeSpan.MinValue;
}
