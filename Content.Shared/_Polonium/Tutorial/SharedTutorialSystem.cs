// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Polonium.Tutorial.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial;

public abstract class SharedTutorialSystem : EntitySystem
{
}

/// <summary>Fires after the player spawns on a solitary map. TutorialSystem picks it up.</summary>
public sealed class TutorialStartRequestedEvent : EntityEventArgs
{
    public EntityUid Player { get; }
    public ProtoId<Prototypes.TutorialFlowPrototype> Flow { get; }

    public TutorialStartRequestedEvent(EntityUid player, ProtoId<Prototypes.TutorialFlowPrototype> flow)
    {
        Player = player;
        Flow = flow;
    }
}
