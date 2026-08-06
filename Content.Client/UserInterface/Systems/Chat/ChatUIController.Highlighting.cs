using System.Linq;
using System.Text.RegularExpressions;
using Robust.Client.Audio;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Content.Shared.CCVar;
using Content.Client.CharacterInfo;
using Content.Client.Gameplay;
using static Content.Client.CharacterInfo.CharacterInfoSystem;

namespace Content.Client.UserInterface.Systems.Chat;

/// <summary>
/// A partial class of ChatUIController that handles the saving and loading of highlights for the chatbox.
/// It also makes use of the CharacterInfoSystem to optionally generate highlights based on the character's info.
/// </summary>
public sealed partial class ChatUIController : IOnSystemChanged<CharacterInfoSystem>
{
    [Dependency] private ILocalizationManager _loc = default!;
    [UISystemDependency] private readonly CharacterInfoSystem _characterInfo = default!;

    private string _chatSpeechDoubleQuoteBegin = default!;

    // Polonium - chat highlight ping sound
    private static readonly ResPath HighlightSoundPath = new("/Audio/_Polonium/Interface/HighlightChatPings/Beep.ogg");

    /// <summary>
    ///     Time of the last highlight ping, used to debounce the sound.
    /// </summary>
    private TimeSpan _lastHighlightTime = TimeSpan.Zero;

    private static readonly Regex StartDoubleQuote = new("\"$");
    private static readonly Regex EndDoubleQuote = new("^\"|(?<=^@)\"");
    private static readonly Regex StartAtSign = new("^@");

    // POLONIUM CHANGE: converts a PascalCase job prototype id ("HeadOfSecurity")
    // into the kebab-case slug used by the "highlights-<job>" loc keys ("head-of-security").
    private static readonly Regex JobProtoKeyRegex = new("(?<=[a-z0-9])(?=[A-Z])");

    /// <summary>
    ///     The list of words to be highlighted in the chatbox.
    /// </summary>
    private readonly List<string> _highlights = new();

    /// <summary>
    ///     The string holding the hex color used to highlight words.
    /// </summary>
    private string? _highlightsColor;

    private bool _autoFillHighlightsEnabled;

    /// <summary>
    ///     The boolean that keeps track of the 'OnCharacterUpdated' event, whenever it's a player attaching or opening the character info panel.
    /// </summary>
    private bool _charInfoIsAttach = false;

    public event Action<string>? HighlightsUpdated;

    private void InitializeHighlights()
    {
        _config.OnValueChanged(CCVars.ChatAutoFillHighlights, (value) => { _autoFillHighlightsEnabled = value; }, true);

        _config.OnValueChanged(CCVars.ChatHighlightsColor, (value) => { _highlightsColor = value; }, true);

        // Load highlights if any were saved.
        var highlights = _config.GetCVar(CCVars.ChatHighlights);

        if (!string.IsNullOrEmpty(highlights))
        {
            UpdateHighlights(highlights, true);
        }

        _chatSpeechDoubleQuoteBegin = _loc.GetString("chat-manager-speech-double-quote-begin");
    }

    // Polonium - chat highlight ping sound
    /// <summary>
    ///     Plays the highlight ping sound if it's enabled in the client's settings.
    /// </summary>
    private void PlayHighlightSound()
    {
        // Don't play sounds while the game is still loading (eg. in the lobby).
        if (_state.CurrentState is not GameplayStateBase)
            return;

        if (!_config.GetCVar(CCVars.ChatHighlightSound))
            return;

        var volume = _config.GetCVar(CCVars.ChatHighlightVolume);

        // A volume of 0 would produce -Infinity dB, so treat it as muted.
        if (volume <= 0f)
            return;

        var volumeDb = MathF.Log10(Math.Clamp(volume, 0f, 1f)) * 20f;
        var audioParams = AudioParams.Default.WithVolume(volumeDb);

        _ent.System<AudioSystem>().PlayGlobal(HighlightSoundPath, Filter.Local(), false, audioParams);
    }

    public void OnSystemLoaded(CharacterInfoSystem system)
    {
        system.OnCharacterUpdate += OnCharacterUpdated;
    }

    public void OnSystemUnloaded(CharacterInfoSystem system)
    {
        system.OnCharacterUpdate -= OnCharacterUpdated;
    }

    private void UpdateAutoFillHighlights()
    {
        if (!_autoFillHighlightsEnabled)
            return;

        // If auto highlights are enabled generate a request for new character info
        // that will be used to determine the highlights.
        _charInfoIsAttach = true;
        _characterInfo.RequestCharacterInfo();
    }

    public void UpdateHighlights(string newHighlights, bool firstLoad = false)
    {
        // Do nothing if the provided highlights are the same as the old ones and it is not the first time.
        if (!firstLoad && _config.GetCVar(CCVars.ChatHighlights).Equals(newHighlights, StringComparison.CurrentCultureIgnoreCase))
            return;

        _config.SetCVar(CCVars.ChatHighlights, newHighlights);
        _config.SaveToFile();

        _highlights.Clear();

        // We first subdivide the highlights based on newlines to prevent replacing
        // a valid "\n" tag and adding it to the final regex.
        var splittedHighlights = newHighlights.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 0; i < splittedHighlights.Length; i++)
        {
            // Replace every "\" character with a "\\" to prevent "\n", "\0", etc...
            var keyword = splittedHighlights[i].Replace(@"\", @"\\");

            // Escape the keyword to prevent special characters like "(" and ")" to be considered valid regex.
            keyword = Regex.Escape(keyword);

            // 1. Since the "["s in WrappedMessage are already sanitized, add 2 extra "\"s
            // to make sure it matches the literal "\" before the square bracket.
            keyword = keyword.Replace(@"\[", @"\\\[");

            // If present, replace the double quotes at the edges with tags
            // that make sure the words to match are separated by spaces or punctuation.
            // NOTE: The reason why we don't use \b tags is that \b doesn't match reverse slash characters "\" so
            // a pre-sanitized (see 1.) string like "\[test]" wouldn't get picked up by the \b.
            if (keyword.Any(c => c == '"'))
            {
                // Matches the last double quote character.
                keyword = StartDoubleQuote.Replace(keyword, "(?!\\w)");
                // When matching for the first double quote character we also consider the possibility
                // of the double quote being preceded by a @ character.
                keyword = EndDoubleQuote.Replace(keyword, "(?<!\\w)");
            }

            // Make sure the character's name is highlighted only when mentioned directly (eg. it's said by someone),
            // for example in 'Name Surname says, "..."' 'Name Surname' won't be highlighted.
            keyword = StartAtSign.Replace(keyword,
                $@"(?<=(?<=(L?OOC|DEAD|ADMIN):.*:.*)|(?<=,.*{_chatSpeechDoubleQuoteBegin}.*)|(?<=\n.*))");

            _highlights.Add(keyword);
        }

        // Arrange the list of highlights in descending order so that when highlighting,
        // the full word (eg. "Security") gets picked before the abbreviation (eg. "Sec").
        _highlights.Sort((x, y) => y.Length.CompareTo(x.Length));
    }

    private void OnCharacterUpdated(CharacterData data)
    {
        // If _charInfoIsAttach is false then the opening of the character panel was the one
        // to generate the event, dismiss it.
        if (!_charInfoIsAttach)
            return;

        var (_, job, jobProto, _, _, entityName) = data; // POLONIUM CHANGE: added jobProto

        // Mark this entity's name as our character name for the "UpdateHighlights" function.
        var newHighlights = "@" + entityName;

        // Subdivide the character's name based on spaces or hyphens so that every word gets highlighted.
        if (newHighlights.Count(c => (c == ' ' || c == '-')) == 1)
            newHighlights = newHighlights.Replace("-", "\n@").Replace(" ", "\n@");

        // If the character has a name with more than one hyphen assume it is a lizard name and extract the first and
        // last name eg. "Eats-The-Food" -> "@Eats" "@Food"
        if (newHighlights.Count(c => c == '-') > 1)
            newHighlights = newHighlights.Split('-')[0] + "\n@" + newHighlights.Split('-')[^1];

        // POLONIUM CHANGE START: prefer the locale-independent job prototype id when
        // available. The localized job title differs per server locale (eg. Polish
        // "Główny Inżynier") and cannot match the ASCII "highlights-<job>" loc keys.
        // The proto id ("ChiefEngineer") kebab-cases to the same slug on any locale.
        var jobKey = !string.IsNullOrEmpty(jobProto)
            ? JobProtoKeyRegex.Replace(jobProto, "-").ToLower()
            : job.Replace(' ', '-').ToLower();
        // POLONIUM CHANGE END

        if (_loc.TryGetString($"highlights-{jobKey}", out var jobMatches))
            newHighlights += '\n' + jobMatches.Replace(", ", "\n");

        UpdateHighlights(newHighlights);
        HighlightsUpdated?.Invoke(newHighlights);
        _charInfoIsAttach = false;
    }
}
