// SPDX-FileCopyrightText: 2026 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2026 SlamBamActionman <83650252+SlamBamActionman@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Whatstone <166147148+whatston3@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Overlays;
using Content.Shared.Access.Systems;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Access.Systems;

public sealed partial class JobStatusSystem : SharedJobStatusSystem
{
    [Dependency] private ShowJobIconsSystem _showJobIcons = default!;
    [Dependency] private ShowCrewIconsSystem _showCrewIcons = default!;

    private static readonly ProtoId<StatusIconPrototype> CrewBorderIcon = "CrewBorderIcon";
    private static readonly ProtoId<StatusIconPrototype> CrewUncertainBorderIcon = "CrewUncertainBorderIcon";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JobStatusComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    // show the status icons if the player has the correponding HUDs
    private void OnGetStatusIconsEvent(Entity<JobStatusComponent> ent, ref GetStatusIconsEvent ev)
    {
        if (_showJobIcons.IsActive && ent.Comp.JobStatusIcon != null)
            ev.StatusIcons.Add(ProtoMan.Index(ent.Comp.JobStatusIcon));

        if (_showCrewIcons.IsActive)
        {
            if (_showCrewIcons.UncertainCrewBorder)
                ev.StatusIcons.Add(ProtoMan.Index(CrewUncertainBorderIcon));
            else if (ent.Comp.IsCrew)
                ev.StatusIcons.Add(ProtoMan.Index(CrewBorderIcon));
        }
    }
}
