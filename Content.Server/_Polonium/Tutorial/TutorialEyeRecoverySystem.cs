using System.Linq;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// A weld without a mask burns the trainee's eyes, and in stock that damage never heals by itself.
/// Inside the tutorial it wears off quietly after a while, so one careless weld does not end the run.
/// Nothing is announced: the holopad only tells them to wait for their sight to come back.
/// </summary>
public sealed partial class TutorialEyeRecoverySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private StatusEffectsSystem _status = default!;

    private static readonly TimeSpan RecoverAfter = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly Dictionary<EntityUid, TimeSpan> _hurtSince = new();
    private TimeSpan _nextPoll;

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextPoll)
            return;

        _nextPoll = _timing.CurTime + PollInterval;

        var hurt = new HashSet<EntityUid>();
        var query = EntityQueryEnumerator<TutorialSessionComponent, BlindableComponent>();
        while (query.MoveNext(out var uid, out _, out var blindable))
        {
            if (!EyesHurt(uid, blindable))
                continue;

            // counted from the first burn, more welding in between does not push it back
            if (!_hurtSince.TryGetValue(uid, out var since))
            {
                _hurtSince[uid] = _timing.CurTime;
                hurt.Add(uid);
                continue;
            }

            if (_timing.CurTime - since < RecoverAfter)
            {
                hurt.Add(uid);
                continue;
            }

            _blindable.AdjustEyeDamage((uid, blindable), -blindable.EyeDamage);
            _status.TryRemoveStatusEffect(uid, BlindnessSystem.BlindingStatusEffect);
        }

        foreach (var uid in _hurtSince.Keys.Where(uid => !hurt.Contains(uid)).ToList())
        {
            _hurtSince.Remove(uid);
        }
    }

    /// <summary>Burnt or flash-blinded eyes. Closed eyes and blindfolds are left out on purpose.</summary>
    public bool EyesHurt(EntityUid uid, BlindableComponent? blindable = null)
    {
        if (!Resolve(uid, ref blindable, false))
            return false;

        return blindable.EyeDamage > 0 || _status.HasStatusEffect(uid, BlindnessSystem.BlindingStatusEffect);
    }
}
