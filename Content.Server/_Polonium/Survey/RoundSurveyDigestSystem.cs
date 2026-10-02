using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.Discord;
using Content.Server.GameTicking.Presets;
using Content.Shared._Polonium.Survey;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Server;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Survey;

/// <summary>
/// Compares the survey answers of a period across game presets and round sizes and posts the result to Discord
/// after every period set in <see cref="CCVars.SurveyDigestDays"/>.
/// </summary>
public sealed partial class RoundSurveyDigestSystem : EntitySystem
{
    [Dependency] private IBaseServer _server = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private DiscordWebhook _discord = default!;

    public const int MaxDays = 365;

    private const int LowAnswer = RoundSurveyQuestionPrototype.MinAnswer + 1;
    private const int HighAnswer = RoundSurveyQuestionPrototype.MaxAnswer - 1;
    private const int MaxPresets = 8;
    private const int MaxLabelLength = 18;
    private const int MaxFieldLength = 1024;
    private const int MaxEmbedFields = 25;
    private const int MaxEmbedLength = 5000;
    private const int DigestColor = 0x4F8FD1;
    private const string Unknown = "—";

    // Calendar weeks
    private static readonly DateTime Epoch = new(1970, 1, 5, 0, 0, 0, DateTimeKind.Utc);

    private readonly List<int> _periods = new();
    private readonly Dictionary<int, DateTime> _reported = new();
    private WebhookIdentifier? _webhook;
    private bool _posting;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);

        Subs.CVar(_cfg, CCVars.SurveyDigestDays, OnDaysChanged, true);
        Subs.CVar(_cfg, CCVars.DiscordSurveyDigestWebhook, OnWebhookChanged, true);
    }

    private void OnDaysChanged(string value)
    {
        _periods.Clear();
        _periods.AddRange(ParsePeriods(value));
    }

    public static List<int> ParsePeriods(string value)
    {
        var periods = new List<int>();
        foreach (var entry in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(entry, out var days) && days is >= 1 and <= MaxDays && !periods.Contains(days))
                periods.Add(days);
        }

        return periods;
    }

    private void OnWebhookChanged(string url)
    {
        _webhook = null;

        if (!string.IsNullOrWhiteSpace(url))
            _discord.GetWebhook(url, data => _webhook = data.ToIdentifier());
    }

    private void OnRoundEnd(RoundEndMessageEvent ev)
    {
        if (_posting || _webhook == null || !_cfg.GetCVar(CCVars.SurveyEnabled))
            return;

        var now = DateTime.UtcNow;
        var due = new List<(DateTime Start, int Days)>();
        foreach (var days in _periods)
        {
            var start = GetPeriodStart(now, days).AddDays(-days);
            if (!_reported.TryGetValue(days, out var reported) || reported != start)
                due.Add((start, days));
        }

        if (due.Count > 0)
            PostDue(due);
    }

    private async void PostDue(List<(DateTime Start, int Days)> due)
    {
        _posting = true;

        try
        {
            foreach (var (start, days) in due)
            {
                if (await _db.AddSurveyDigest(start, days)
                    && await GetDigest(start, start.AddDays(days)) is { } digest
                    && !await Post(digest))
                {
                    await _db.RemoveSurveyDigest(start, days);
                    continue;
                }

                _reported[days] = start;
            }
        }
        catch (Exception e)
        {
            Log.Error($"Error while preparing a round survey digest: {e}");
        }
        finally
        {
            _posting = false;
        }
    }

    /// <summary>
    /// Start of the period of this many days that the moment falls into.
    /// </summary>
    public static DateTime GetPeriodStart(DateTime time, int days)
    {
        var passed = (int) Math.Floor((time - Epoch).TotalDays);
        return Epoch.AddDays(passed - passed % days);
    }

    public async Task<RoundSurveyDigest?> GetDigest(DateTime from, DateTime to)
    {
        return BuildDigest(await _db.GetSurveyResponses(from, to), from, to);
    }

    /// <summary>
    /// Sends the digest to the digest channel.
    /// </summary>
    /// <returns>False if there is no such channel or Discord did not take all of it.</returns>
    public async Task<bool> Post(RoundSurveyDigest digest)
    {
        if (_webhook is not { } webhook)
            return false;

        try
        {
            foreach (var payload in GetPayloads(digest))
            {
                var response = await _discord.CreateMessage(webhook, payload);
                if (!response.IsSuccessStatusCode)
                    return false;
            }
        }
        catch (Exception e)
        {
            Log.Error($"Error while sending the round survey digest to Discord: {e}");
            return false;
        }

        return true;
    }

    public RoundSurveyDigest? BuildDigest(List<SurveyResponse> responses, DateTime from, DateTime to)
    {
        if (responses.Count == 0)
            return null;

        var groups = GetGroups(responses);
        var sections = new List<RoundSurveyDigestSection>
        {
            new(Loc.GetString("round-survey-digest-rounds"), DescribeRounds(groups)),
        };

        var asked = responses.Select(response => response.Question).ToHashSet();
        var questions = _proto.EnumeratePrototypes<RoundSurveyQuestionPrototype>()
            .Where(question => asked.Contains(question.ID))
            .OrderBy(question => question.Order)
            .ThenBy(question => question.ID);

        foreach (var question in questions)
        {
            var name = Loc.GetString("round-survey-discord-question",
                ("text", Loc.GetString(question.Text)),
                ("low", Loc.GetString(question.Low)),
                ("high", Loc.GetString(question.High)));

            sections.Add(new RoundSurveyDigestSection(name, DescribeAnswers(groups, question)));
        }

        var first = Day(from);
        var last = Day(to.AddTicks(-1));
        var title = first == last
            ? Loc.GetString("round-survey-digest-title-day", ("day", first))
            : Loc.GetString("round-survey-digest-title", ("from", first), ("to", last));
        var summary = Loc.GetString("round-survey-digest-summary",
            ("rounds", responses.Select(response => response.RoundId).Distinct().Count().ToString()),
            ("people", responses.Select(response => response.PlayerUserId).Distinct().Count().ToString()),
            ("answers", responses.Count.ToString()));

        return new RoundSurveyDigest(title, summary, sections);
    }

    /// <summary>
    /// The answers split by game preset and then, after a null, split again by round size.
    /// </summary>
    private List<Group?> GetGroups(List<SurveyResponse> responses)
    {
        var groups = new List<Group?>();

        var presets = responses
            .GroupBy(response => response.Preset)
            .OrderByDescending(preset => preset.Select(response => response.RoundId).Distinct().Count())
            .ThenBy(preset => preset.Key)
            .ToList();

        foreach (var preset in presets.Take(MaxPresets))
        {
            groups.Add(new Group(preset.Key == string.Empty ? Unknown : Cut(preset.Key), preset.ToList()));
        }

        var rest = presets.Skip(MaxPresets).SelectMany(preset => preset).ToList();
        if (rest.Count > 0)
            groups.Add(new Group(Loc.GetString("round-survey-digest-other"), rest));

        groups.Add(null);

        var sizes = _proto.EnumeratePrototypes<RoundSurveyPlayerGroupPrototype>().OrderBy(size => size.Min).ToList();
        for (var i = 0; i < sizes.Count; i++)
        {
            var min = sizes[i].Min;
            int? next = i + 1 < sizes.Count ? sizes[i + 1].Min : null;

            var inside = responses
                .Where(response => response.PlayerCount >= min && (next == null || response.PlayerCount < next))
                .ToList();

            if (inside.Count == 0)
                continue;

            var label = next == null
                ? Loc.GetString("round-survey-digest-players-from", ("min", min.ToString()))
                : Loc.GetString("round-survey-digest-players-range", ("min", min.ToString()), ("max", (next.Value - 1).ToString()));

            groups.Add(new Group(label, inside));
        }

        return groups;
    }

    private string DescribeRounds(List<Group?> groups)
    {
        var rows = new List<string[]?>();
        foreach (var group in groups)
        {
            if (group == null)
            {
                rows.Add(null);
                continue;
            }

            var rounds = group.Responses.GroupBy(response => response.RoundId).Select(round => round.First()).ToList();
            var counted = rounds.Where(round => round.LeftCount != null && round.PlayerCount > 0).ToList();
            var left = counted.Count == 0
                ? Unknown
                : Percent(counted.Sum(round => round.LeftCount!.Value) / (float) counted.Sum(round => round.PlayerCount));

            rows.Add([
                group.Label,
                rounds.Count.ToString(),
                ((int) Math.Round(rounds.Average(round => round.PlayerCount))).ToString(),
                left,
            ]);
        }

        return Table([
            Loc.GetString("round-survey-digest-column-group"),
            Loc.GetString("round-survey-digest-column-rounds"),
            Loc.GetString("round-survey-digest-column-players"),
            Loc.GetString("round-survey-digest-column-left"),
        ], rows);
    }

    private string DescribeAnswers(List<Group?> groups, RoundSurveyQuestionPrototype question)
    {
        var header = new List<string>
        {
            Loc.GetString("round-survey-digest-column-group"),
            Loc.GetString("round-survey-digest-column-people"),
            Loc.GetString("round-survey-digest-column-average"),
            $"{RoundSurveyQuestionPrototype.MinAnswer}–{LowAnswer}",
            $"{HighAnswer}–{RoundSurveyQuestionPrototype.MaxAnswer}",
        };

        if (question.Target != null)
            header.Add(Loc.GetString("round-survey-digest-column-offset"));

        var rows = new List<string[]?>();
        foreach (var group in groups)
        {
            if (group == null)
            {
                rows.Add(null);
                continue;
            }

            if (GetScore(group.Responses.Where(response => response.Question == question.ID), question) is not { } score)
                continue;

            var row = new List<string>
            {
                group.Label,
                score.People.ToString(),
                score.Average.ToString("0.0", CultureInfo.InvariantCulture),
                Percent(score.Low),
                Percent(score.High),
            };

            if (question.Target != null)
                row.Add(score.Offset.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture));

            rows.Add(row.ToArray());
        }

        return Table(header.ToArray(), rows);
    }

    private Score? GetScore(IEnumerable<SurveyResponse> answers, RoundSurveyQuestionPrototype question)
    {
        // player counts once, no matter in how many rounds they answered.
        var people = answers
            .GroupBy(answer => answer.PlayerUserId)
            .Select(own => new Score(1,
                own.Average(answer => (float) answer.Value),
                own.Average(answer => answer.Value <= LowAnswer ? 1f : 0f),
                own.Average(answer => answer.Value >= HighAnswer ? 1f : 0f),
                own.Average(answer => answer.Value - GetTarget(answer.Preset, question))))
            .ToList();

        if (people.Count == 0)
            return null;

        return new Score(people.Count,
            people.Average(person => person.Average),
            people.Average(person => person.Low),
            people.Average(person => person.High),
            people.Average(person => person.Offset));
    }

    private float GetTarget(string preset, RoundSurveyQuestionPrototype question)
    {
        _proto.TryIndex<GamePresetPrototype>(preset, out var played);
        return RoundSurveySystem.GetTarget(played, question) ?? 0f;
    }

    public List<WebhookPayload> GetPayloads(RoundSurveyDigest digest)
    {
        var payloads = new List<WebhookPayload>();
        var fields = new List<WebhookEmbedField>();
        var length = digest.Title.Length + digest.Summary.Length;

        foreach (var section in digest.Sections)
        {
            var field = new WebhookEmbedField
            {
                Name = section.Name,
                Value = Fence(section.Table),
                Inline = false,
            };

            var size = field.Name.Length + field.Value.Length;
            if (fields.Count == MaxEmbedFields || fields.Count > 0 && length + size > MaxEmbedLength)
            {
                payloads.Add(CreatePayload(digest, fields, payloads.Count == 0));
                fields = new List<WebhookEmbedField>();
                length = digest.Title.Length;
            }

            fields.Add(field);
            length += size;
        }

        payloads.Add(CreatePayload(digest, fields, payloads.Count == 0));
        return payloads;
    }

    private WebhookPayload CreatePayload(RoundSurveyDigest digest, List<WebhookEmbedField> fields, bool first)
    {
        return new WebhookPayload
        {
            Embeds =
            [
                new WebhookEmbed
                {
                    Title = digest.Title,
                    Description = first ? digest.Summary : string.Empty,
                    Color = DigestColor,
                    Footer = new WebhookEmbedFooter { Text = _server.ServerName },
                    Fields = fields,
                },
            ],
        };
    }

    private static string Fence(string table)
    {
        const string open = "```\n";
        const string close = "\n```";

        while (table.Length > MaxFieldLength - open.Length - close.Length)
        {
            table = table[..table.LastIndexOf('\n')];
        }

        return open + table + close;
    }

    /// <summary>
    /// Lines up the rows in columns. A null row leaves a gap between two parts of the table.
    /// </summary>
    private static string Table(string[] header, List<string[]?> rows)
    {
        var widths = header.Select(cell => cell.Length).ToArray();
        foreach (var row in rows)
        {
            for (var i = 0; row != null && i < row.Length; i++)
            {
                widths[i] = Math.Max(widths[i], row[i].Length);
            }
        }

        var lines = new List<string>();
        foreach (var row in rows.Prepend(header))
        {
            if (row == null)
            {
                if (lines.Count > 1 && lines[^1] != string.Empty)
                    lines.Add(string.Empty);

                continue;
            }

            var line = new StringBuilder(row[0].PadRight(widths[0]));
            for (var i = 1; i < row.Length; i++)
            {
                line.Append("  ").Append(row[i].PadLeft(widths[i]));
            }

            lines.Add(line.ToString());
        }

        if (lines[^1] == string.Empty)
            lines.RemoveAt(lines.Count - 1);

        return string.Join('\n', lines);
    }

    private static string Cut(string label)
    {
        return label.Length > MaxLabelLength ? label[..(MaxLabelLength - 1)] + "…" : label;
    }

    private static string Percent(float share)
    {
        return $"{(int) MathF.Round(share * 100f)}%";
    }

    private static string Day(DateTime time)
    {
        return time.ToString("dd.MM", CultureInfo.InvariantCulture);
    }

    private sealed record Group(string Label, List<SurveyResponse> Responses);

    private readonly record struct Score(int People, float Average, float Low, float High, float Offset);
}

public sealed record RoundSurveyDigest(string Title, string Summary, List<RoundSurveyDigestSection> Sections);

public sealed record RoundSurveyDigestSection(string Name, string Table);
