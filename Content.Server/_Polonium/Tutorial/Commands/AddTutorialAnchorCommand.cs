// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

#if TOOLS
using System.Diagnostics.CodeAnalysis;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server._Polonium.Tutorial.Commands;

[AnyCommand]
public sealed class AddTutorialAnchorCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "addtutorialanchor";
    public string Description => "Adds TutorialAnchor to one or more entities.";
    public string Help => "Usage: addtutorialanchor <AnchorId> <entityUid> [<entityUid> ...]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Help);
            return;
        }

        var anchorId = args[0].Trim();
        if (string.IsNullOrEmpty(anchorId))
        {
            shell.WriteError("AnchorId cannot be empty.");
            return;
        }

        var updated = 0;
        for (var i = 1; i < args.Length; i++)
        {
            if (!TryResolveEntity(args[i], out var uid))
            {
                shell.WriteError($"Entity not found or invalid uid: {args[i]}");
                continue;
            }

            var hadAnchor = _entManager.TryGetComponent(uid.Value, out TutorialAnchorComponent? existing);
            var anchor = _entManager.EnsureComponent<TutorialAnchorComponent>(uid.Value);
            var previousId = hadAnchor ? existing!.AnchorId : null;
            anchor.AnchorId = anchorId;

            if (previousId is { } prev && prev != anchorId)
                shell.WriteLine($"Updated TutorialAnchor on {uid.Value}: '{prev}' -> '{anchorId}'");
            else
                shell.WriteLine($"Set TutorialAnchor '{anchorId}' on {uid.Value}");

            updated++;
        }

        if (updated == 0)
            shell.WriteError("No entities were updated.");
        else
            shell.WriteLine($"Done. {updated} entit(y/ies) updated.");
    }

    private bool TryResolveEntity(string arg, [NotNullWhen(true)] out EntityUid? uid)
    {
        uid = null;

        if (NetEntity.TryParse(arg, out var netUid) && _entManager.TryGetEntity(netUid, out var entity))
        {
            if (!_entManager.EntityExists(entity))
                return false;

            uid = entity;
            return true;
        }

        if (EntityUid.TryParse(arg, out var parsed) && _entManager.EntityExists(parsed))
        {
            uid = parsed;
            return true;
        }

        return false;
    }
}
#endif
