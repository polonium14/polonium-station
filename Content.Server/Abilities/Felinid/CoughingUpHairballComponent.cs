// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server.Abilities.Felinid;

[RegisterComponent]
public sealed partial class CoughingUpHairballComponent : Component
{
    [DataField]
    public float Accumulator;

    [DataField]
    public TimeSpan CoughUpTime = TimeSpan.FromSeconds(2.15); // length of hairball.ogg
}
