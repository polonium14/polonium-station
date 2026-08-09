// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

#if TOOLS
using Content.Server.Power.Components;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Administration;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server._Polonium.Tutorial.Commands;

/// <summary>Dev-only: unbolts + powers on every TutorialAnchor door. Rescues stuck players.</summary>
[AnyCommand]
public sealed class TutorialResetDoorsCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _ent = default!;

    public string Command => "tutorialresetdoors";
    public string Description => "Unbolts and re-powers every TutorialAnchor door on the map.";
    public string Help => "Usage: tutorialresetdoors";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var door = _ent.System<SharedDoorSystem>();
        var fixedCount = 0;

        var query = _ent.EntityQueryEnumerator<TutorialAnchorComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_ent.TryGetComponent<DoorBoltComponent>(uid, out var bolt) && bolt.BoltsDown)
            {
                door.SetBoltsDown((uid, bolt), false);
                fixedCount++;
            }

            if (_ent.TryGetComponent<ApcPowerReceiverComponent>(uid, out var receiver) && receiver.PowerDisabled)
            {
                receiver.PowerDisabled = false;
                fixedCount++;
            }
        }

        shell.WriteLine($"Reset {fixedCount} door state(s).");
    }
}
#endif
