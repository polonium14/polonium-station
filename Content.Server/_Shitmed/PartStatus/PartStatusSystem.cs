using System.Linq;
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
    /// Builds the same per-limb status used by the examine verb, without colours, keyed by body part, for
    /// consumers that need it outside the examine flow (e.g. HealthAnalyzerSystem's scan readout).
    /// A health analyzer counts as a HandScanner-tier device, so this additionally surfaces
    /// wounds that hide from plain examine (WoundVisibility.HandScanner, e.g. WoundPoison).
    /// </summary>
    public Dictionary<TargetBodyPart, string> GetPartStatusDescriptions(EntityUid mob)
    {
        var result = new Dictionary<TargetBodyPart, string>();

        foreach (var partStatus in CollectPartStatuses(mob, WoundVisibility.HandScanner))
            result[partStatus.Part] = string.Join(Loc.GetString("inspect-part-status-comma"),
                BuildStatusEntries(partStatus).Select(entry => entry.Text));

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

            var (wounds, isBleeding) = AnalyzeWounds(woundable, maxVisibility);

            partStatusSet.Add(new PartStatus(
                target,
                Loc.GetString($"target-zone-{target.ToString().ToLower()}"),
                woundable.Comp.WoundableSeverity,
                wounds,
                bone.BoneSeverity,
                isBleeding));
        }

        return partStatusSet;
    }

    // Wounds are keyed by damage type ID (not the localized group name, which never matched
    // "Brute"/"Burn"/"Toxin" outside English, so no wound was ever shown), keeping the worst severity per type.
    private (Dictionary<ProtoId<DamageTypePrototype>, PartWound> Wounds, bool IsBleeding) AnalyzeWounds(
        Entity<WoundableComponent> woundable,
        WoundVisibility maxVisibility)
    {
        var wounds = new Dictionary<ProtoId<DamageTypePrototype>, PartWound>();
        var isBleeding = false;

        foreach (var wound in _woundSystem.GetWoundableWounds(woundable))
        {
            if (wound.Comp.DamageGroup is not { } damageGroup
                || wound.Comp.WoundSeverity == WoundSeverity.Healed
                || wound.Comp.WoundVisibility > maxVisibility)
                continue;

            if (!wounds.TryGetValue(wound.Comp.DamageType, out var existing)
                || wound.Comp.WoundSeverity > existing.Severity)
                wounds[wound.Comp.DamageType] = new PartWound(damageGroup, wound.Comp.WoundSeverity);

            if (TryComp<BleedInflicterComponent>(wound, out var bleeds) && bleeds.IsBleeding)
                isBleeding = true;
        }

        return (wounds, isBleeding);
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
        CreateBodyPartMessage(partStatusSet, message);

        if (styling)
        {
            message.Pop();
            message.PushNewline();
        }

        return message;
    }

    // One short "Part: wound, wound" row per limb, each wound coloured by its kind.
    private void CreateBodyPartMessage(HashSet<PartStatus> partStatusSet, FormattedMessage message)
    {
        var orderedParts = PartOrder
            .Select(part => partStatusSet.FirstOrDefault(p => p.Part == part))
            .Where(p => p != null)!
            .Cast<PartStatus>();

        foreach (var partStatus in orderedParts)
        {
            message.AddText("    " + Loc.GetString("inspect-part-status-row", ("part", partStatus.PartName)));

            var entries = BuildStatusEntries(partStatus);
            for (var i = 0; i < entries.Count; i++)
            {
                if (i > 0)
                    message.AddText(Loc.GetString("inspect-part-status-comma"));

                message.PushColor(entries[i].Color);
                message.AddText(entries[i].Text);
                message.Pop();
            }

            message.PushNewline();
        }
    }

    private static readonly Color FineColor = Color.FromHex("#9a9a9a");
    private static readonly Color BleedingColor = Color.FromHex("#e03a3a");
    private static readonly Color BoneColor = Color.FromHex("#e8c547");
    private static readonly Color OtherWoundColor = Color.FromHex("#d0d0d0");

    private static readonly Dictionary<string, Color> GroupColors = new()
    {
        { "Brute", Color.FromHex("#ff6b6b") },
        { "Burn", Color.FromHex("#ff9f43") },
        { "Toxin", Color.FromHex("#7ccf3f") },
    };

    private List<(string Text, Color Color)> BuildStatusEntries(PartStatus partStatus)
    {
        var entries = new List<(string Text, Color Color)>();

        foreach (var (type, wound) in partStatus.Wounds.OrderByDescending(pair => pair.Value.Severity))
        {
            var name = Loc.TryGetString($"inspect-wound-type-{type}", out var typeName)
                ? typeName
                : _proto.Index(type).LocalizedName;
            var severity = wound.Severity > WoundSeverity.Critical ? WoundSeverity.Critical : wound.Severity;
            var text = Loc.GetString("inspect-part-status-wound",
                ("wound", name),
                ("severity", Loc.GetString($"inspect-wound-severity-{severity.ToString().ToLower()}")));
            entries.Add((text, GroupColors.GetValueOrDefault(wound.Group.Id, OtherWoundColor)));
        }

        if (partStatus.Bleeding)
            entries.Add((Loc.GetString("inspect-part-status-bleeding"), BleedingColor));

        if (partStatus.BoneSeverity > BoneSeverity.Normal)
        {
            var bone = partStatus.BoneSeverity == BoneSeverity.Broken
                ? "inspect-part-status-bone-broken"
                : "inspect-part-status-bone-damaged";
            entries.Add((Loc.GetString(bone), BoneColor));
        }

        if (entries.Count == 0)
            entries.Add((Loc.GetString("inspect-part-status-fine"), FineColor));

        return entries;
    }

    private void AddLine(FormattedMessage message)
    {
        message.PushColor(Color.FromHex("#282D31"));
        message.AddText(Loc.GetString("examine-border-line"));
        message.PushNewline();
        message.Pop();
    }
}
