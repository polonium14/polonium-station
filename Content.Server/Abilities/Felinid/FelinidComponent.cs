// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Server.Abilities.Felinid;

[RegisterComponent]
public sealed partial class FelinidComponent : Component
{
    /// <summary>
    /// The hairball prototype to use.
    /// </summary>
    [DataField]
    public EntProtoId HairballPrototype = "Hairball";

    [DataField]
    public EntProtoId? HairballActionId = "ActionHairball";

    [DataField]
    public EntityUid? HairballAction;

    [DataField]
    public EntProtoId? EatActionId = "ActionEatMouse";

    [DataField]
    public EntityUid? EatAction;

    [DataField]
    public EntityUid? EatActionTarget;
}
