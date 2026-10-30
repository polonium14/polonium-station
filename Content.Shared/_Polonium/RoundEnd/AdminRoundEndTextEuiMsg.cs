using System.Diagnostics.CodeAnalysis;
using Content.Shared.Eui;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Polonium.RoundEnd;

public static class AdminRoundEndText
{
    public const int MaxLength = 800;

    /// <summary>
    /// Every player sees this text, so tags that act on click (cmdlink, url) or hide it are not allowed.
    /// </summary>
    private static readonly HashSet<string> AllowedTags = ["bold", "italic", "bolditalic", "color", "head", "bullet"];

    /// <summary>
    /// Checks the text before it reaches the round end window, which throws on invalid markup.
    /// </summary>
    public static bool TryValidate(string text, [NotNullWhen(false)] out string? error)
    {
        if (text.Length > MaxLength)
        {
            error = Loc.GetString("admin-round-end-text-too-long", ("max", MaxLength));
            return false;
        }

        if (!FormattedMessage.TryFromMarkup(text, out var message, out var parseError))
        {
            error = Loc.GetString("admin-round-end-text-invalid-markup", ("error", parseError));
            return false;
        }

        foreach (var node in message.Nodes)
        {
            if (node.Name == null || AllowedTags.Contains(node.Name))
                continue;

            error = Loc.GetString("admin-round-end-text-tag-not-allowed", ("tag", node.Name));
            return false;
        }

        error = null;
        return true;
    }
}

/// <param name="Editor">Null while no admin has set the text this round.</param>
/// <param name="EditTime">Round time of the last change.</param>
/// <param name="CanEdit">The text can only be changed during the round, the summary is sent once when it ends.</param>
/// <param name="RulesWithText">Game rules of this round that add their own part to the summary.</param>
[Serializable, NetSerializable]
public sealed class AdminRoundEndTextEuiState(
    string text,
    string? editor,
    TimeSpan editTime,
    bool canEdit,
    List<string> rulesWithText) : EuiStateBase
{
    public readonly string Text = text;
    public readonly string? Editor = editor;
    public readonly TimeSpan EditTime = editTime;
    public readonly bool CanEdit = canEdit;
    public readonly List<string> RulesWithText = rulesWithText;
}

public static class AdminRoundEndTextEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class Save(string text) : EuiMessageBase
    {
        public readonly string Text = text;
    }
}
