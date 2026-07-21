using System.Linq;
using System.Text;
using Content.Server.Chat.Managers;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.PartStatus.Events;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Content.Shared.Chat;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Examine;
using Content.Shared.HealthExaminable;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Shitmed.PartStatus;

public sealed partial class PartStatusSystem : EntitySystem
{
    [Dependency] private WoundSystem _woundSystem = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ExamineSystemShared _examineSystem = default!;
    [Dependency] private HealthExaminableSystem _healthExaminable = default!;
    [Dependency] private MobStateSystem _mobStateSystem = default!;
    [Dependency] private IChatManager _chat = default!;

    private static readonly TargetBodyPart[] PartOrder =
    {
        TargetBodyPart.Head,
        TargetBodyPart.Chest,
        TargetBodyPart.LeftArm,
        TargetBodyPart.RightArm,
        TargetBodyPart.LeftLeg,
        TargetBodyPart.RightLeg,
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HealthExaminableComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
        SubscribeNetworkEvent<GetPartStatusEvent>(OnGetPartStatus);
    }

    private void OnGetPartStatus(GetPartStatusEvent message, EntitySessionEventArgs args)
    {
        var entity = GetEntity(message.Uid);

        if (args.SenderSession.AttachedEntity != entity)
            return;

        if (_mobStateSystem.IsIncapacitated(entity) || !TryComp<ActorComponent>(entity, out var actor))
            return;

        var partStatusSet = CollectPartStatuses(entity, WoundVisibility.Always);
        var text = GetExamineText(entity, entity, partStatusSet);

        _chat.ChatMessageToOne(
            ChatChannel.Emotes,
            text.ToMarkup(),
            text.ToMarkup(),
            EntityUid.Invalid,
            false,
            actor.PlayerSession.Channel,
            recordReplay: false);
    }

    private void OnGetExamineVerbs(EntityUid uid, HealthExaminableComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!TryComp<BodyComponent>(uid, out var body) || body.Organs is null)
        {
            _healthExaminable.OnGetExamineVerbs(uid, component, args);
            return;
        }

        var detailsRange = _examineSystem.IsInDetailsRange(args.User, uid);

        var verb = new ExamineVerb()
        {
            Act = () =>
            {
                var partStatusSet = CollectPartStatuses(uid, WoundVisibility.Always);
                var text = GetExamineText(uid, args.User, partStatusSet);
                _examineSystem.SendExamineTooltip(args.User, uid, text, false, false);
            },
            Text = Loc.GetString("health-examinable-verb-text"),
            Category = VerbCategory.Examine,
            Disabled = !detailsRange,
            Message = detailsRange ? null : Loc.GetString("health-examinable-verb-disabled"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/rejuvenate.svg.192dpi.png")),
        };

        args.Verbs.Add(verb);
    }

    /// <summary>
    /// Builds the same per-limb status line used by the examine verb, keyed by body part, for
    /// consumers that need it outside the examine flow (e.g. HealthAnalyzerSystem's scan readout).
    /// A health analyzer counts as a HandScanner-tier device, so this additionally surfaces
    /// wounds that hide from plain examine (WoundVisibility.HandScanner, e.g. WoundPoison).
    /// </summary>
    public Dictionary<TargetBodyPart, string> GetPartStatusDescriptions(EntityUid mob)
    {
        var result = new Dictionary<TargetBodyPart, string>();

        foreach (var partStatus in CollectPartStatuses(mob, WoundVisibility.HandScanner))
            result[partStatus.Part] = BuildStatusDescription(partStatus, false).Status;

        return result;
    }

    private HashSet<PartStatus> CollectPartStatuses(EntityUid mob, WoundVisibility maxVisibility)
    {
        var partStatusSet = new HashSet<PartStatus>();

        foreach (var woundable in _woundSystem.GetAllWoundableChildren(mob))
        {
            if (!TryComp<OrganComponent>(woundable, out var organComp)
                || organComp.Category is not { } category
                || !LimbTargetMap.TryGetTarget(category, out var target)
                || !TryComp<BoneComponent>(woundable.Comp.Bone?.ContainedEntities.FirstOrNull(), out var bone))
                continue;

            var (damageSeverities, isBleeding) = AnalyzeWounds(woundable, maxVisibility);

            partStatusSet.Add(new PartStatus(
                target,
                Loc.GetString($"target-zone-{target.ToString().ToLower()}"),
                woundable.Comp.WoundableSeverity,
                damageSeverities,
                bone.BoneSeverity,
                isBleeding));
        }

        return partStatusSet;
    }

    private (Dictionary<string, WoundSeverity> DamageSeverities, bool IsBleeding) AnalyzeWounds(
        Entity<WoundableComponent> woundable,
        WoundVisibility maxVisibility)
    {
        var damageSeverities = new Dictionary<string, WoundSeverity>();
        var isBleeding = false;

        foreach (var wound in _woundSystem.GetWoundableWounds(woundable))
        {
            if (wound.Comp.DamageGroup is not { } damageGroup
                || wound.Comp.WoundSeverity == WoundSeverity.Healed
                || wound.Comp.WoundVisibility > maxVisibility)
                continue;

            if (!damageSeverities.TryGetValue(damageGroup.Id, out var existingSeverity) ||
                wound.Comp.WoundSeverity > existingSeverity)
                damageSeverities[_proto.Index(damageGroup).LocalizedName] = wound.Comp.WoundSeverity;

            if (TryComp<BleedInflicterComponent>(wound, out var bleeds) && bleeds.IsBleeding)
                isBleeding = true;
        }

        return (damageSeverities, isBleeding);
    }

    private FormattedMessage GetExamineText(EntityUid entity,
        EntityUid examiner,
        HashSet<PartStatus> partStatusSet,
        bool styling = true)
    {
        var message = new FormattedMessage();
        var titlestring = entity == examiner
            ? "inspect-part-status-title"
            : "inspect-part-status-title-other";

        if (styling)
        {
            message.PushTag(new MarkupNode("examineborder", null, null));
            message.PushNewline();
        }
        else
        {
            titlestring += "-styleless";
        }

        message.AddText(Loc.GetString(titlestring, ("entity", Identity.Name(entity, EntityManager))));
        message.PushNewline();
        AddLine(message);
        CreateBodyPartMessage(partStatusSet, entity == examiner, ref message, !styling);

        if (styling)
        {
            message.Pop();
            message.PushNewline();
        }

        return message;
    }

    private void CreateBodyPartMessage(HashSet<PartStatus> partStatusSet,
        bool inspectingSelf,
        ref FormattedMessage message,
        bool styleless = false)
    {
        var orderedParts = PartOrder
            .Select(part => partStatusSet.FirstOrDefault(p => p.Part == part))
            .Where(p => p != null)!
            .Cast<PartStatus>();

        foreach (var partStatus in orderedParts)
        {
            var (statusDescription, traumaOnly) = BuildStatusDescription(partStatus, inspectingSelf);
            var possessive = inspectingSelf
                ? Loc.GetString("inspect-part-status-you")
                : Loc.GetString("inspect-part-status-their");

            var locString = traumaOnly ? "inspect-part-status-line-trauma-only" : "inspect-part-status-line";

            if (styleless)
                locString += "-styleless";

            message.AddText("    " + Loc.GetString(locString,
                ("possessive", possessive),
                ("part", partStatus.PartName),
                ("status", statusDescription)));

            message.PushNewline();
        }
    }

    private (string Status, bool TraumaOnly) BuildStatusDescription(PartStatus partStatus, bool inspectingSelf)
    {
        var sb = new StringBuilder();
        var hasStatus = false;

        var overallSeverity = GetOverallWoundSeverity(partStatus.DamageSeverities);
        if (overallSeverity != WoundSeverity.Healed)
        {
            var localeText = $"inspect-wound-{overallSeverity.ToString().ToLower()}";
            sb.Append(Loc.GetString(localeText));
            hasStatus = true;
        }

        var damageDescriptions = GetDamageGroupDescriptions(partStatus.DamageSeverities);
        if (damageDescriptions.Count > 0)
        {
            if (hasStatus)
                sb.Append(Loc.GetString("inspect-part-status-comma"));
            sb.Append(Loc.GetString("inspect-part-status-conjunction"));
            sb.Append(string.Join(" ", damageDescriptions));
            hasStatus = true;
        }

        var traumaDescriptions = GetTraumaDescriptions(partStatus, inspectingSelf);
        var traumaOnly = false;
        if (traumaDescriptions.Count > 0)
        {
            if (hasStatus)
            {
                sb.Append(Loc.GetString("inspect-part-status-conjunction2"));
            }
            else
            {
                // No overall/damage-group text preceded this - the trauma description(s) are
                // the entire status, and the caller will pick a template with no baked-in
                // verb for them, so no conjunction prefix is needed here.
                traumaOnly = true;
            }

            sb.Append(string.Join(Loc.GetString("inspect-part-status-comma"), traumaDescriptions));
            hasStatus = true;
        }

        if (!hasStatus)
            sb.Append(Loc.GetString("inspect-part-status-fine"));

        return (sb.ToString(), traumaOnly);
    }

    private static readonly string[] RenderedDamageGroups = { "Brute", "Burn", "Toxin" };

    private WoundSeverity GetOverallWoundSeverity(Dictionary<string, WoundSeverity> damageSeverities)
    {
        if (damageSeverities.Count == 0)
            return WoundSeverity.Healed;

        var maxSeverity = WoundSeverity.Healed;
        foreach (var (type, severity) in damageSeverities)
        {
            if (!RenderedDamageGroups.Contains(type) || severity <= maxSeverity)
                continue;

            maxSeverity = severity;
        }

        return maxSeverity;
    }

    private List<string> GetDamageGroupDescriptions(Dictionary<string, WoundSeverity> damageSeverities)
    {
        var descriptions = new List<string>();
        foreach (var (type, severity) in damageSeverities)
        {
            if (!RenderedDamageGroups.Contains(type))
                continue;

            var cappedSeverity = severity > WoundSeverity.Severe ? WoundSeverity.Severe : severity;
            var localeText = $"inspect-wound-{type}-{cappedSeverity.ToString().ToLower()}";
            descriptions.Add(Loc.GetString(localeText));
        }

        if (descriptions.Count > 1)
        {
            var lastDescription = descriptions[^1];
            descriptions[^1] = Loc.GetString("inspect-part-status-and") + lastDescription;
        }

        return descriptions;
    }

    private List<string> GetTraumaDescriptions(PartStatus partStatus, bool inspectingSelf)
    {
        var descriptions = new List<string>();

        if (partStatus.BoneSeverity > BoneSeverity.Normal)
        {
            var localeText = inspectingSelf ? "self-inspect-trauma-BoneDamage" : "inspect-trauma-BoneDamage";
            descriptions.Add(Loc.GetString(localeText));
        }

        if (partStatus.Bleeding)
            descriptions.Add(Loc.GetString("inspect-wound-Bleeding-moderate"));

        if (descriptions.Count > 1)
        {
            var lastDescription = descriptions[^1];
            descriptions[^1] = Loc.GetString("inspect-part-status-and") + lastDescription;
        }

        return descriptions;
    }

    private void AddLine(FormattedMessage message)
    {
        message.PushColor(Color.FromHex("#282D31"));
        message.AddText(Loc.GetString("examine-border-line"));
        message.PushNewline();
        message.Pop();
    }
}
