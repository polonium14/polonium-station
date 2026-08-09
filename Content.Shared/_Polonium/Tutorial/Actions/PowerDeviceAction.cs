// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Polonium.Tutorial.Actions;

/// <summary>Turns the APC power of an anchored device on/off (works on any ApcPowerReceiver).</summary>
public sealed partial class PowerDeviceAction : TutorialAction
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField]
    public bool Powered = false;

    /// <summary>Run after this many seconds — useful for sequencing close → bolt → power off.</summary>
    [DataField]
    public float Delay = 0f;
}
