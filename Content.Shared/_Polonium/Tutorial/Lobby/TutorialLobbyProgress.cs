// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Polonium.Tutorial.Lobby;
public sealed class TutorialLobbyProgress
{
    public string CurrentStepId { get; set; } = string.Empty;
    public bool IsCompleted { get; set; } = false;
    public bool IsPaused { get; set; } = false;
}
