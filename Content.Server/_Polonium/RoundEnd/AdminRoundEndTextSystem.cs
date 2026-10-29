using System.Linq;
using System.Reflection;
using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Components;
using Content.Shared._Polonium.RoundEnd;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Robust.Shared.Player;

namespace Content.Server._Polonium.RoundEnd;

/// <summary>
/// Holds the round outcome written by admins, shown above the automatic round end summary.
/// </summary>
public sealed partial class AdminRoundEndTextSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private GameTicker _ticker = default!;

    // GameRuleSystem<T>.AppendRoundEndText is protected, so nameof cannot reach it.
    private const string AppendTextMethod = "AppendRoundEndText";

    private readonly HashSet<AdminRoundEndTextEui> _openEuis = new();
    private HashSet<Type>? _textRuleComponents;

    public string Text { get; private set; } = string.Empty;

    /// <summary>
    /// Name of the admin who last changed the text, null if nobody did this round.
    /// </summary>
    public string? Editor { get; private set; }

    public TimeSpan EditTime { get; private set; }

    public bool CanEdit => _ticker.RunLevel == GameRunLevel.InRound;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<GameRuleAddedEvent>(OnGameRuleAdded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    public void Register(AdminRoundEndTextEui eui)
    {
        _openEuis.Add(eui);
    }

    public void Unregister(AdminRoundEndTextEui eui)
    {
        _openEuis.Remove(eui);
    }

    /// <summary>
    /// Sets the outcome text, an empty one removes it from the summary.
    /// </summary>
    public bool TrySetText(ICommonSession admin, string text)
    {
        text = text.Trim();
        if (!CanEdit || !AdminRoundEndText.TryValidate(text, out _))
            return false;

        if (text == Text)
            return true;

        Text = text;
        Editor = admin.Name;
        EditTime = _ticker.RoundDuration();

        _adminLog.Add(LogType.AdminCommands, LogImpact.Medium,
            $"{admin:actor} set the round end text: {text}");

        RefreshEuis();
        return true;
    }

    /// <summary>
    /// Puts the admin-written outcome above the round end text gathered from game rules.
    /// </summary>
    public string Prepend(string roundEndText)
    {
        if (Text.Length == 0)
            return roundEndText;

        return $"{Loc.GetString("admin-round-end-text-header")}\n{Text}\n{roundEndText}";
    }

    /// <summary>
    /// Prototype IDs of this round's game rules that add their own part to the round end summary.
    /// </summary>
    public List<string> GetRulesWithText()
    {
        var textRuleComponents = GetTextRuleComponents();
        var rules = new SortedSet<string>();
        var query = EntityQueryEnumerator<GameRuleComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            foreach (var comp in EntityManager.GetComponents(uid))
            {
                if (!textRuleComponents.Contains(comp.GetType()))
                    continue;

                rules.Add(MetaData(uid).EntityPrototype?.ID ?? Name(uid));
                break;
            }
        }

        return rules.ToList();
    }

    /// <summary>
    /// Rule components whose GameRuleSystem overrides AppendRoundEndText.
    /// </summary>
    public HashSet<Type> GetTextRuleComponents()
    {
        if (_textRuleComponents != null)
            return _textRuleComponents;

        _textRuleComponents = new HashSet<Type>();
        foreach (var type in EntityManager.EntitySysManager.GetEntitySystemTypes())
        {
            for (var baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
            {
                if (!baseType.IsGenericType || baseType.GetGenericTypeDefinition() != typeof(GameRuleSystem<>))
                    continue;

                var append = type.GetMethod(AppendTextMethod, BindingFlags.Instance | BindingFlags.NonPublic);
                if (append != null && append.DeclaringType != baseType)
                    _textRuleComponents.Add(baseType.GetGenericArguments()[0]);

                break;
            }
        }

        return _textRuleComponents;
    }

    private void RefreshEuis()
    {
        foreach (var eui in _openEuis)
        {
            eui.StateDirty();
        }
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent args)
    {
        RefreshEuis();
    }

    private void OnGameRuleAdded(ref GameRuleAddedEvent args)
    {
        RefreshEuis();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        Text = string.Empty;
        Editor = null;
        EditTime = TimeSpan.Zero;
    }
}
