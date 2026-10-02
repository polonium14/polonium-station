using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Offer players a short optional survey when the round ends.
    /// </summary>
    public static readonly CVarDef<bool> SurveyEnabled =
        CVarDef.Create("survey.enabled", false, CVar.SERVERONLY);

    /// <summary>
    /// The survey of a round stays open through the lobby and for this many seconds after the next round has started.
    /// </summary>
    public static readonly CVarDef<float> SurveyCloseDelay =
        CVarDef.Create("survey.close_delay", 120f, CVar.SERVERONLY);

    /// <summary>
    /// URL of the Discord webhook which will receive the survey summary of each round.
    /// The summary names the real preset, so keep the channel staff-only. If left empty, disables the webhook.
    /// </summary>
    public static readonly CVarDef<string> DiscordSurveyWebhook =
        CVarDef.Create("discord.survey_webhook", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    /// URL of the Discord webhook which will receive each player's own answers, one message per player and round.
    /// The messages name the player, so keep the channel staff-only. If left empty, disables the webhook.
    /// </summary>
    public static readonly CVarDef<string> DiscordSurveyResponsesWebhook =
        CVarDef.Create("discord.survey_responses_webhook", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    /// Lengths in days of the periods a survey digest is posted for, separated by commas:
    /// "7" gives a weekly digest, "1,7,30" a daily, a weekly and a monthly one.
    /// </summary>
    public static readonly CVarDef<string> SurveyDigestDays =
        CVarDef.Create("survey.digest_days", "7", CVar.SERVERONLY);

    /// <summary>
    /// URL of the Discord webhook which will receive the survey digests. Disabled when empty
    /// </summary>
    public static readonly CVarDef<string> DiscordSurveyDigestWebhook =
        CVarDef.Create("discord.survey_digest_webhook", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);
}
