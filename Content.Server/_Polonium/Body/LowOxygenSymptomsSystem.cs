using Content.Server.Body.Components;
using Content.Server.Chat.Systems;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Body;

/// <summary>
/// A body whose oxygen saturation reads low coughs now and then, and gasps when it reads very low,
/// so a missing lung or heavy smoking shows before the patient starts to suffocate.
/// </summary>
public sealed partial class LowOxygenSymptomsSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private CirculationSystem _circulation = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private static readonly ProtoId<EmotePrototype> Cough = "Cough";
    private static readonly ProtoId<EmotePrototype> Gasp = "Gasp";
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private TimeSpan _next;

    /// <summary>
    /// Whether the body's saturation reads low enough to show: below the cough threshold, awake and alive.
    /// </summary>
    public bool HasSymptoms(EntityUid body, out bool severe)
    {
        severe = false;
        if (_mobState.IsIncapacitated(body)
            || _statusEffects.HasStatusEffect(body, CirculationSystem.Unconscious)
            || _circulation.ReadSpO2(body) is not { } spo2
            || spo2 >= _cfg.GetCVar(CCVars.OrgansCoughSpO2))
            return false;

        severe = spo2 < _cfg.GetCVar(CCVars.OrgansGaspSpO2);
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _next)
            return;

        _next = _timing.CurTime + Interval;

        var chance = _cfg.GetCVar(CCVars.OrgansCoughChance);
        var query = EntityQueryEnumerator<RespiratorComponent, HumanoidProfileComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (!HasSymptoms(uid, out var severe) || !_random.Prob(chance))
                continue;

            _chat.TryEmoteWithChat(uid, severe ? Gasp : Cough, ChatTransmitRange.HideChat, ignoreActionBlocker: true);
        }
    }
}
