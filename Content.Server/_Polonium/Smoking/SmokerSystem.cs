using Content.Server._Polonium.Body;
using Content.Shared._Polonium.Smoking;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Jittering;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Smoking;

/// <summary>
/// Runs the smoker trait: scars the lungs for good when it is applied, then tracks nicotine in the blood and plays out
/// withdrawal (craving, shaking, coughing) while there is none.
/// </summary>
public sealed partial class SmokerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private OrganHealingSystem _organs = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    private static readonly ProtoId<ReagentPrototype> Nicotine = "Nicotine";
    private static readonly ProtoId<EmotePrototype> Cough = "Cough";
    private static readonly HashSet<ProtoId<OrganCategoryPrototype>> Lungs = ["Lungs"];

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan JitterTime = TimeSpan.FromSeconds(2);

    private const int CravingMessages = 3;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmokerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<SmokerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.LastNicotine = _timing.CurTime;
        ent.Comp.NextCheck = _timing.CurTime + CheckInterval;

        foreach (var lung in _organs.GetLivingOrgans(ent, Lungs, []))
        {
            var kept = _random.NextFloat(ent.Comp.MinLungIntegrity, ent.Comp.MaxLungIntegrity);
            _organs.Scar(lung, 1f - kept);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SmokerComponent, BloodstreamComponent>();
        while (query.MoveNext(out var uid, out var smoker, out var bloodstream))
        {
            if (now < smoker.NextCheck)
                continue;

            smoker.NextCheck = now + CheckInterval;
            Check((uid, smoker), bloodstream, now);
        }
    }

    private void Check(Entity<SmokerComponent> ent, BloodstreamComponent bloodstream, TimeSpan now)
    {
        var smoker = ent.Comp;

        if (_solution.TryGetSolution(ent.Owner, bloodstream.BloodSolutionName, out _, out var blood)
            && blood.GetTotalPrototypeQuantity(Nicotine) > 0)
        {
            smoker.LastNicotine = now;
            if (smoker.Stage > 0)
                _popup.PopupEntity(Loc.GetString("smoker-relief"), ent, ent);

            smoker.Stage = 0;
            return;
        }

        var stage = 0;
        foreach (var start in smoker.StageStarts)
        {
            if (now - smoker.LastNicotine >= start)
                stage++;
        }

        if (stage > smoker.Stage)
            _popup.PopupEntity(Loc.GetString($"smoker-withdrawal-{stage}"), ent, ent, PopupType.Medium);

        smoker.Stage = stage;
        if (stage == 0)
            return;

        if (_random.Prob(smoker.CravingChance))
            _popup.PopupEntity(Loc.GetString($"smoker-craving-{_random.Next(1, CravingMessages + 1)}"), ent, ent);

        var index = Math.Min(stage, smoker.JitterAmplitudes.Length) - 1;
        if (smoker.JitterAmplitudes[index] > 0f)
            _jitter.DoJitter(ent, JitterTime, true, smoker.JitterAmplitudes[index]);

        if (_random.Prob(smoker.CoughChances[Math.Min(index, smoker.CoughChances.Length - 1)]))
            _chat.TryEmoteWithChat(ent, Cough);
    }
}
