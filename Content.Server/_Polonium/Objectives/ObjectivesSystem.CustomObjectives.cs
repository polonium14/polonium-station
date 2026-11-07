using System.Text;
using Content.Server._Polonium.Objectives;
using Content.Shared.GameTicking;
using Content.Shared.Mind;

namespace Content.Server.Objectives;

public sealed partial class ObjectivesSystem
{
    /// <summary>
    /// Lists minds that got custom objectives from an admin but are not listed under any game rule,
    /// otherwise those objectives never show up in the round end summary.
    /// </summary>
    private void AppendCustomObjectiveMinds(ref RoundEndTextAppendEvent ev, HashSet<EntityUid> listedMinds)
    {
        var minds = new List<(EntityUid, string)>();
        var query = EntityQueryEnumerator<MindComponent>();
        while (query.MoveNext(out var uid, out var mind))
        {
            if (listedMinds.Contains(uid) || !HasCustomObjective(mind))
                continue;

            var name = mind.CharacterName
                       ?? (mind.OwnedEntity is { } owned ? Name(owned) : string.Empty);
            minds.Add((uid, name));
        }

        if (minds.Count == 0)
            return;

        var result = new StringBuilder();
        result.AppendLine(Loc.GetString("objectives-round-end-custom-result"));
        result.AppendLine();
        AddSummary(result, string.Empty, minds, "objectives-custom-with-objectives");
        ev.AddLine(result.AppendLine().ToString());
    }

    private bool HasCustomObjective(MindComponent mind)
    {
        foreach (var objective in mind.Objectives)
        {
            if (HasComp<CustomObjectiveConditionComponent>(objective))
                return true;
        }

        return false;
    }
}
