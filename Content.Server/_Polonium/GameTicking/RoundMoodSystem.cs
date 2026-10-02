using System.Linq;
using Content.Server.GameTicking.Presets;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared._Polonium.GameTicking;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.GameTicking;

/// <summary>
/// Remembers the mood vote until the round starts and turns it into preset weights for the secret rule.
/// </summary>
public sealed partial class RoundMoodSystem : EntitySystem
{
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private readonly Dictionary<ProtoId<RoundMoodPrototype>, int> _votes = new();
    private readonly Dictionary<ProtoId<RoundMoodPrototype>, int> _roundVotes = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        WarnAboutPresetsWithoutMoods();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<GamePresetPrototype>())
            WarnAboutPresetsWithoutMoods();
    }

    private void WarnAboutPresetsWithoutMoods()
    {
        var presets = _proto.EnumeratePrototypes<GamePresetPrototype>().ToList();
        if (!presets.Any(UsesMoods))
            return;

        var missing = GetPresetsWithoutMoods(presets);
        if (missing.Count > 0)
            Log.Warning($"Game presets without moods are never picked by the secret rule: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// Presets that no mood lists, so secret will never pick them. Secret presets themselves don't count.
    /// </summary>
    public List<string> GetPresetsWithoutMoods(IEnumerable<GamePresetPrototype> presets)
    {
        return presets
            .Where(preset => preset.Moods.Count == 0 && GetSecretRule(preset) == null)
            .Select(preset => preset.ID)
            .Order()
            .ToList();
    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        _roundVotes.Clear();
        foreach (var (mood, count) in _votes)
        {
            _roundVotes[mood] = count;
        }

        _votes.Clear();
    }

    public List<(RoundMoodPrototype Mood, int Votes)> GetRoundVotes()
    {
        return GetMoods()
            .Where(mood => _roundVotes.GetValueOrDefault(mood.ID) > 0)
            .Select(mood => (mood, _roundVotes[mood.ID]))
            .ToList();
    }

    public int GetVotes(ProtoId<RoundMoodPrototype> mood)
    {
        return _votes.GetValueOrDefault(mood);
    }

    /// <summary>
    /// Players who didn't vote go to the default mood.
    /// </summary>
    public void SetVotes(Dictionary<ProtoId<RoundMoodPrototype>, int> votes, int silent = 0)
    {
        _votes.Clear();
        foreach (var (mood, count) in votes)
        {
            _votes[mood] = Math.Max(count, 0);
        }

        if (silent > 0 && GetDefaultMood() is { } standard)
            _votes[standard.ID] = GetVotes(standard.ID) + silent;
    }

    public void Clear()
    {
        _votes.Clear();
    }

    public RoundMoodPrototype? GetDefaultMood()
    {
        return GetMoods().FirstOrDefault(mood => mood.Default);
    }

    private IEnumerable<RoundMoodPrototype> GetMoods()
    {
        return _proto.EnumeratePrototypes<RoundMoodPrototype>()
            .OrderBy(mood => mood.Order)
            .ThenBy(mood => mood.ID);
    }

    /// <summary>
    /// True for a secret preset that picks by moods.
    /// </summary>
    public bool UsesMoods(GamePresetPrototype? preset)
    {
        return preset != null && GetSecretRule(preset) is { UseMoods: true };
    }

    private SecretRuleComponent? GetSecretRule(GamePresetPrototype preset)
    {
        foreach (var ruleId in preset.Rules)
        {
            if (_proto.TryIndex(ruleId, out var rule) && rule.TryComp<SecretRuleComponent>(out var secret, _factory))
                return secret;
        }

        return null;
    }

    /// <summary>
    /// Moods to offer in the vote with this many players, already in display order.
    /// </summary>
    public List<RoundMoodPrototype> GetVotableMoods(int players)
    {
        var used = new HashSet<ProtoId<RoundMoodPrototype>>();
        foreach (var preset in _proto.EnumeratePrototypes<GamePresetPrototype>())
        {
            used.UnionWith(preset.Moods.Keys);
        }

        return GetMoods()
            .Where(mood => mood.MinPlayers <= players && used.Contains(mood.ID))
            .ToList();
    }

    /// <summary>
    /// Preset weights for the secret rule. Each mood gets as big a share as it got votes.
    /// If a mood has no preset it can offer right now, its votes go to the default mood.
    /// </summary>
    public Dictionary<string, float> GetWeights(Func<GamePresetPrototype, bool>? allowed = null)
    {
        ProtoId<RoundMoodPrototype>? standard = GetDefaultMood()?.ID;
        var weights = new Dictionary<string, float>();
        var total = _votes.Values.Sum();
        var rest = total;

        foreach (var (mood, votes) in _votes)
        {
            if (votes == 0 || mood == standard)
                continue;

            if (AddMood(weights, mood, votes / (float) total, allowed))
                rest -= votes;
        }

        if (standard != null)
            AddMood(weights, standard.Value, total == 0 ? 1f : rest / (float) total, allowed);

        return weights;
    }

    private bool AddMood(
        Dictionary<string, float> weights,
        ProtoId<RoundMoodPrototype> mood,
        float share,
        Func<GamePresetPrototype, bool>? allowed)
    {
        if (share <= 0f)
            return false;

        var presets = new List<(string Id, float Weight)>();
        foreach (var preset in _proto.EnumeratePrototypes<GamePresetPrototype>())
        {
            if (preset.Moods.TryGetValue(mood, out var weight) && weight > 0f && (allowed == null || allowed(preset)))
                presets.Add((preset.ID, weight));
        }

        var sum = presets.Sum(preset => preset.Weight);
        if (sum <= 0f)
            return false;

        foreach (var (id, weight) in presets)
        {
            weights[id] = weights.GetValueOrDefault(id) + share * weight / sum;
        }

        return true;
    }
}
