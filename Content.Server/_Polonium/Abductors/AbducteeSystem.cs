using System.Linq;
using Content.Server._Polonium.Objectives;
using Content.Server.Administration.Managers;
using Content.Shared._Polonium.Abductors;
using Content.Shared._Polonium.Objectives;
using Content.Shared.Administration.Logs;
using Content.Shared.Antag;
using Content.Shared.Database;
using Content.Shared.Mind;
using Content.Shared.Objectives.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Polonium.Abductors;

public sealed partial class AbducteeSystem : EntitySystem
{
    private static readonly ProtoId<ObjectiveIssuerPrototype> Issuer = "AbducteeObsession";
    private static readonly Color BriefingColor = Color.FromHex("#ff0099");
    private static readonly List<ProtoId<AntagPrototype>> BannableRoles = ["Abductee"];
    private static readonly LocId AntagonistBriefing = "abductee-briefing-antagonist";

    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private CustomObjectiveSystem _objectives = default!;
    [Dependency] private IBanManager _ban = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedRoleSystem _role = default!;

    /// <summary>
    /// Leaves a returned specimen with an obsession. Whoever is already an antagonist keeps that role.
    /// </summary>
    public void Assign(EntityUid subject)
    {
        if (TryComp<ActorComponent>(subject, out var actor) && _ban.IsRoleBanned(actor.PlayerSession, BannableRoles))
            return;

        if (!_mind.TryGetMind(subject, out var mindId, out var mind) || PickTier() is not { } tier)
            return;

        var entry = _random.Pick(tier.Objectives);
        var title = Loc.GetString(entry.Title);
        var description = Loc.GetString(entry.Description);
        if (!_objectives.TryAdd((mindId, mind), title, description, Issuer, out var objective))
            return;

        _objectives.TrySetStatus((mindId, mind), objective, CustomObjectiveStatus.Completed);

        var wasAntagonist = _role.MindIsAntagonist(mindId);
        if (!wasAntagonist)
            _role.MindAddRole(mindId, tier.MindRole, mind, true);

        var briefing = wasAntagonist ? AntagonistBriefing : tier.Briefing;
        _antag.SendBriefing(subject, Loc.GetString(briefing, ("title", title), ("description", description)), BriefingColor, null);
        _adminLog.Add(LogType.Mind, LogImpact.Medium, $"{ToPrettyString(subject):player} was left with the abductee objective {entry.Title} ({tier.ID})");
    }

    private AbducteeTierPrototype? PickTier()
    {
        var tiers = _prototype.EnumeratePrototypes<AbducteeTierPrototype>().ToList();
        var roll = _random.NextFloat(tiers.Sum(tier => tier.Weight));
        foreach (var tier in tiers)
        {
            roll -= tier.Weight;
            if (roll <= 0f)
                return tier;
        }

        return tiers.LastOrDefault();
    }
}
