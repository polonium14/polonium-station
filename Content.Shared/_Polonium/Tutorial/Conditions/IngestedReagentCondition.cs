using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Conditions;

public sealed partial class IngestedReagentCondition : TutorialCondition
{
    public const string FlagPrefix = "ingested:";

    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    public static string Flag(ProtoId<ReagentPrototype> reagent) => FlagPrefix + reagent;
}
