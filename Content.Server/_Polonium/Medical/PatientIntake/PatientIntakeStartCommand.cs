// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Shared._Polonium.Medical.PatientIntake;
using Content.Shared.Administration;
using Content.Shared.Storage.Components;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Medical.PatientIntake;

/// <summary>
/// Starts a case on an intake machine right away, skipping the timer and the staffing check.
/// </summary>
[AdminCommand(AdminFlags.Round)]
public sealed partial class PatientIntakeStartCommand : LocalizedEntityCommands
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private PatientIntakeSystem _intake = default!;

    public override string Command => "patientintake_start";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            shell.WriteError(Help);
            return;
        }

        if (!NetEntity.TryParse(args[0], out var netMachine)
            || !EntityManager.TryGetEntity(netMachine, out var machine)
            || !EntityManager.TryGetComponent<PatientIntakeMachineComponent>(machine, out var comp)
            || !EntityManager.TryGetComponent<EntityStorageComponent>(machine, out var storage))
        {
            shell.WriteError(Loc.GetString("cmd-patientintake_start-no-machine", ("uid", args[0])));
            return;
        }

        if (comp.Case != null)
        {
            shell.WriteError(Loc.GetString("cmd-patientintake_start-busy"));
            return;
        }

        if (storage.Open)
        {
            shell.WriteError(Loc.GetString("cmd-patientintake_start-open"));
            return;
        }

        var ent = (machine.Value, comp);
        if (args.Length == 1)
        {
            _intake.StartCase(ent, storage, int.MaxValue);
        }
        else if (_proto.TryIndex<PatientIntakeCasePrototype>(args[1], out var caseProto))
        {
            _intake.StartCase(ent, storage, caseProto);
        }
        else
        {
            shell.WriteError(Loc.GetString("cmd-patientintake_start-no-case", ("case", args[1])));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-patientintake_start-started", ("case", comp.Case?.Proto.Id ?? "?")));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.Components<PatientIntakeMachineComponent>(args[0]),
                Loc.GetString("cmd-patientintake_start-arg-machine")),
            2 => CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<PatientIntakeCasePrototype>(),
                Loc.GetString("cmd-patientintake_start-arg-case")),
            _ => CompletionResult.Empty,
        };
    }
}
