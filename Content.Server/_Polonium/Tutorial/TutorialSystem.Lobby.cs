using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Content.Shared.CCVar;
using Content.Shared.Interaction;
using Content.Shared.Ghost.Components;
using Content.Shared.Mobs;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// The way in and out: the lobby tour, starting the practical part, the finale choice and
/// the record of who finished. The console join stays shut while a trainee is still inside.
/// </summary>
public sealed partial class TutorialSystem
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private SolitarySpawningSystem _solitary = default!;

    public void ForceStartFlow(EntityUid player, ProtoId<TutorialFlowPrototype> flowId) =>
        StartFlow(player, flowId, fromBeginning: false);

    private void OnStartRequested(TutorialStartRequestedEvent ev)
    {
        StartFlow(ev.Player, ev.Flow, ev.FromBeginning);
    }

    private void OnStartPractical(TutorialStartPracticalEvent ev, EntitySessionEventArgs args)
    {
        if (ReadIntroMode() != IntroTutorial)
            return;

        _lobbyTour.Remove(args.SenderSession.UserId);
        _solitary.TryJoinFromLobby(args.SenderSession);
    }

    private void OnReturnToLobby(TutorialReturnToLobbyEvent ev, EntitySessionEventArgs args)
    {
        if (ReadIntroMode() != IntroTutorial)
            return;

        var session = args.SenderSession;
        if (session.AttachedEntity is not { } mob || !HasComp<GhostComponent>(mob))
            return;

        EntityManager.System<GameTicker>().Respawn(session);
    }

    private void OnLobbyFlow(TutorialLobbyFlowEvent ev, EntitySessionEventArgs args)
    {
        if (ev.Active)
            _lobbyTour.Add(args.SenderSession.UserId);
        else
            _lobbyTour.Remove(args.SenderSession.UserId);
    }

    public bool TryBlockConsoleJoin(ICommonSession player, IConsoleShell shell)
    {
        if (!IsConsoleJoinBlocked(player))
            return false;

        shell.WriteError(Loc.GetString("cmd-tutorial-lobby-join-blocked"));
        return true;
    }

    public bool IsConsoleJoinBlocked(ICommonSession player)
    {
        var ticker = EntityManager.System<GameTicker>();
        if (ticker.UserHasJoinedGame(player))
            return false;

        if (_lobbyTour.Contains(player.UserId))
            return true;

        return ReadIntroMode() == IntroTutorial;
    }

    private void OnPlayerStatus(object? sender, SessionStatusEventArgs ev)
    {
        if (ev.NewStatus == SessionStatus.Disconnected)
        {
            _lobbyTour.Remove(ev.Session.UserId);
            return;
        }

        if (ev.NewStatus != SessionStatus.Connected)
            return;

        if (ReadIntroMode() != IntroMain)
            return;

        if (string.IsNullOrEmpty(_cfg.GetCVar(CCVars.TutorialSolitaryServerConnectionString)))
            return;

        SendCompletionStatus(ev.Session);
    }

    private async void SendCompletionStatus(ICommonSession session)
    {
        var completed = false;
        try
        {
            (completed, _) = await _db.GetTutorialCompletion(session.UserId);
        }
        catch (Exception e)
        {
            Log.Error($"Tutorial: failed to read completion for {session.UserId}: {e}");
        }

        if (session.Status == SessionStatus.Disconnected)
            return;

        RaiseNetworkEvent(new TutorialPlayerCompletionEvent(completed), session);
    }

    private void OnRestartRequested(TutorialRestartRequestedEvent ev, EntitySessionEventArgs args)
    {
        if (ReadIntroMode() == IntroNone)
            return;

        var session = args.SenderSession;

        if (_solitary.TryRestartTutorial(session, fromBeginning: true))
            return;

        // tutorialforcestart / already in a flow on a dirty map - replay in place
        if (session.AttachedEntity is not { } mob || !TryComp<TutorialSessionComponent>(mob, out var tut))
            return;

        StartFlow(mob, tut.Flow, fromBeginning: true);
    }

    private void OnEnteredFinale(Entity<TutorialSessionComponent> ent)
    {
        // the music is started by the client when the finale bubble shows, a server playlist here would cut it
        if (_player.TryGetSessionByEntity(ent.Owner, out var sess))
            RecordTutorialCompletion(sess, DateTime.UtcNow - ent.Comp.FlowStartedAt);
    }

    private void OnFinaleChoice(TutorialFinaleChoiceEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        if (!TryComp<TutorialSessionComponent>(player, out var session))
            return;

        if (!TryGetCurrentStep(session, out _, out var proto) || !proto.Finale)
            return;

        CompleteFlow((player, session), redial: ev.JoinServer);
    }

    private async void RecordTutorialCompletion(ICommonSession session, TimeSpan duration)
    {
        try
        {
            await _db.SetTutorialCompletion(session.UserId, duration);

            if (session.Status != SessionStatus.Disconnected)
                RaiseNetworkEvent(new TutorialPlayerCompletionEvent(true), session);
        }
        catch (Exception e)
        {
            Log.Error($"Tutorial: failed to save completion for {session.UserId}: {e}");
        }
    }
}
