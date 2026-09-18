using Content.Server.Wires;
using Content.Shared._Polonium.Tutorial.Actions;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Interaction;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// Runs the actions a step lists in its YAML. Everything here is the stage crew: it opens the
/// doors the next room needs, puts props back where the trainee left them and never decides
/// anything about the step itself.
/// </summary>
public sealed partial class TutorialActionExecutor : EntitySystem
{
    [Dependency] private TutorialMentorSystem _mentor = default!;
    [Dependency] private SharedEntityStorageSystem _storage = default!;
    [Dependency] private RotateToFaceSystem _rotateToFace = default!;

    public void ExecuteAll(EntityUid player, IReadOnlyList<TutorialAction> actions, bool instant = false)
    {
        foreach (var action in actions)
            Execute(player, action, instant);
    }

    public void Execute(EntityUid player, TutorialAction action, bool instant = false)
    {
        switch (action)
        {
            case GrantAccessAction grant:
                GrantAccess(player, grant.Tags);
                break;

            case SealAnchorAirlockAction seal:
                SealAirlock(player, seal.AnchorId);
                break;
            case UnlockAnchorAirlockAction unlock:
                SetBolt(player, unlock.AnchorId, false);
                break;
            case OpenAnchorAirlockAction open:
                OpenAirlock(player, open.AnchorId);
                break;

            case FaceDirectionAction face:
                _rotateToFace.TryFaceAngle(player, face.Direction.ToAngle());
                break;

            case SetLightsAction lights:
                SetLights(player, lights.AnchorId, lights.On);
                break;

            case PowerDeviceAction power:
                if (instant || power.Delay <= 0f)
                    SetPower(player, power.AnchorId, power.Powered);
                else
                    RunMaybeDelayed(power.Delay, () => SetPower(player, power.AnchorId, power.Powered));
                break;

            case SetPacifiedAction pacify:
                SetPacified(player, pacify.Pacified);
                break;

            case SpeakHolopadAction speak:
                if (!instant)
                    _mentor.Enqueue(player, speak.Lines);
                break;

            case SpawnAtAnchorAction spawn:
                SpawnAt(player, spawn);
                break;

            case ClaimNearbyMobAction claim:
                ClaimNearby(player, claim);
                break;

            case MeteorWindowAction meteor:
                if (instant)
                    BreakWindow(player, meteor.WindowAnchor, meteor.Damage);
                else
                    StartMeteor(player, meteor);
                break;

            case ClampAmeInjectionAction clamp:
                ClampAme(player, clamp.AnchorId, clamp.Clamp);
                break;

            case OpenStorageAction open:
                if (!open.RecoveryOnly)
                    OpenStorage(player, open.AnchorId);
                break;

            case SetAnchorAccessAction setAccess:
                SetAnchorAccess(player, setAccess);
                break;

            case SetDisarmProneAction prone:
                SetDisarmProne(player, prone);
                break;

            case ConfineAnchorAction confine:
                Confine(player, confine);
                break;

            case BonkNpcAction bonk:
                StartBonk(player, bonk, instant);
                break;

            case DrainAnchorSolutionAction drain:
                DrainSolution(player, drain);
                break;

            case DrainPrototypeSolutionAction drainProto:
                DrainPrototypeSolution(player, drainProto);
                break;

            case SetFlagAction flag:
                SetFlag(player, flag);
                break;

            case AnchorMusicAction music:
                AnchorMusic(player, music, instant);
                break;

            case EjectTraineeAction eject:
                Eject(player, eject);
                break;

            case MuteBriefingAction:
                _mentor.DropBriefing(player);
                break;

            case ShotScoreAction score:
                if (!instant)
                    ShotScore(player, score);
                break;

            case RequireGlovesAction gloves:
                if (TryComp<TutorialSessionComponent>(player, out var gloveSession))
                {
                    gloveSession.RequireInsulatedGloves = gloves.Required;
                    gloveSession.GlovesWarned = false;
                }
                break;

            default:
                Log.Warning($"Tutorial: no handler for action type {action.GetType().Name}");
                break;
        }
    }

    private bool TryGetAnchor(EntityUid player, string anchorId, out EntityUid uid)
    {
        uid = default;
        if (!TryComp<TutorialSessionComponent>(player, out var session))
            return false;

        if (session.Anchors.TryGetValue(anchorId, out uid) && !Deleted(uid))
            return true;

        foreach (var other in AnchorsNamed(player, anchorId))
        {
            uid = other;
            return true;
        }

        return false;
    }

    private IEnumerable<(string Id, EntityUid Uid)> AnchorsOf(EntityUid player)
    {
        if (!TryComp<TutorialSessionComponent>(player, out var session))
            yield break;

        foreach (var pair in session.Anchors)
            yield return (pair.Key, pair.Value);
    }

    private IEnumerable<EntityUid> AnchorsNamed(EntityUid player, string anchorId)
    {
        if (!TryComp(player, out TransformComponent? xform) || xform.GridUid is not { } grid)
            yield break;

        var query = EntityQueryEnumerator<TutorialAnchorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var anchor, out var ax))
        {
            if (anchor.AnchorId == anchorId && ax.GridUid == grid && !Deleted(uid))
                yield return uid;
        }
    }

    private static void RunMaybeDelayed(float delaySeconds, Action action)
    {
        if (delaySeconds <= 0f)
        {
            action();
            return;
        }

        Timer.Spawn(TimeSpan.FromSeconds(delaySeconds), action);
    }
}
