using System.Linq;
using Content.Server._Polonium.GameTicking;
using Content.Server.GameTicking;
using Content.Shared._Polonium.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Voting;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Voting.Managers;

public sealed partial class VoteManager
{
    private int CountConnectedPlayers()
    {
        return _playerManager.Sessions.Count(session => session.Status != SessionStatus.Disconnected);
    }

    private List<RoundMoodPrototype> GetVotableMoods()
    {
        var ticker = _entityManager.System<GameTicker>();
        var mood = _entityManager.System<RoundMoodSystem>();
        return mood.UsesMoods(ticker.Preset) ? mood.GetVotableMoods(CountConnectedPlayers()) : [];
    }

    private bool CanCallMoodVote()
    {
        return GetVotableMoods().Count > 1;
    }

    private bool TryCreateMoodVote(ICommonSession? initiator)
    {
        var moods = GetVotableMoods();
        if (moods.Count < 2)
        {
            _adminLogger.Add(LogType.Vote, LogImpact.Low, $"Mood vote skipped: the selected preset has no moods to choose from.");
            return false;
        }

        var alone = _playerManager.PlayerCount == 1 && initiator != null;
        var options = new VoteOptions
        {
            Title = Loc.GetString("ui-vote-mood-title"),
            VoteType = StandardVoteType.Preset,
            Duration = alone
                ? TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VoteTimerAlone))
                : TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VoteTimerPreset)),
            DisplayVotes = false,
        };

        foreach (var mood in moods)
        {
            options.Options.Add((Loc.GetString(mood.Name), mood.ID));
        }

        if (alone)
            options.InitiatorTimeout = TimeSpan.FromSeconds(10);

        WirePresetVoteInitiator(options, initiator);

        var vote = CreateVote(options);

        vote.OnFinished += (_, _) =>
        {
            if (vote.CastVotes.Count == 0)
            {
                _adminLogger.Add(LogType.Vote, LogImpact.Low, $"Mood vote finished with no votes cast; keeping the current mood.");
                return;
            }

            var votes = moods.ToDictionary(mood => new ProtoId<RoundMoodPrototype>(mood.ID), mood => vote.VotesPerOption[mood.ID]);
            var silent = Math.Max(CountConnectedPlayers() - vote.CastVotes.Count, 0);
            _entityManager.System<RoundMoodSystem>().SetVotes(votes, silent);

            var tally = string.Join(", ", moods.Select(mood => $"{mood.ID} {votes[mood.ID]}"));
            var results = string.Join(", ", moods.Select(mood => $"{Loc.GetString(mood.Name)} {votes[mood.ID]}"));

            _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Mood vote finished: {tally}, not voted {silent}");
            _chatManager.SendAdminAnnouncement(Loc.GetString("ui-vote-mood-admin-result", ("results", results), ("silent", silent)));
            _chatManager.DispatchServerAnnouncement(Loc.GetString("ui-vote-mood-finished"));
        };

        return true;
    }
}
