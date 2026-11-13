using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Rules;

public sealed partial class SolitarySpawningSystem
{
    private static readonly ProtoId<TutorialStepPrototype> FourthRoomStep = "TutorialLinearR03";

    private static readonly TimeSpan EarlyAwayCleanup = TimeSpan.FromMinutes(5);

    private TimeSpan GetAwayLimit(NetUserId user, TimeSpan limit)
    {
        return EarlyAwayCleanup < limit && IsStillAtStart(user) ? EarlyAwayCleanup : limit;
    }

    private bool IsStillAtStart(NetUserId user)
    {
        if (!_stations.TryGetValue(user, out var rec)
            || !TryComp<TutorialSessionComponent>(rec.Trainee, out var session))
        {
            return true;
        }

        if (!_proto.TryIndex(session.Flow, out var flow))
            return false;

        var fourthRoom = flow.Steps.IndexOf(FourthRoomStep);
        return fourthRoom >= 0 && session.CurrentStepIndex < fourthRoom;
    }
}
