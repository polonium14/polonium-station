using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Tutorial;

public sealed partial class TutorialSystem
{
    private static readonly SatiationValue Comfortable = "Okay";

    private void OnTraineeSatiation(Entity<SatiationComponent> ent, ref SatiationUpdateEvent args)
    {
        if (!HasComp<TutorialSessionComponent>(ent))
            return;

        if (args.Type != SatiationSystem.Hunger && args.Type != SatiationSystem.Thirst)
            return;

        FloorTraineeSatiation(ent, args.Type);
    }

    private void KeepTraineeComfortable(EntityUid uid)
    {
        if (!TryComp<SatiationComponent>(uid, out var satiation))
            return;

        var ent = new Entity<SatiationComponent>(uid, satiation);
        _satiation.SetValue(ent, SatiationSystem.Hunger, Comfortable);
        _satiation.SetValue(ent, SatiationSystem.Thirst, Comfortable);
    }

    private void FloorTraineeSatiation(Entity<SatiationComponent> ent, ProtoId<SatiationTypePrototype> type)
    {
        if (_satiation.IsValueInRange(ent, type, below: Comfortable))
            _satiation.SetValue(ent, type, Comfortable);
    }
}
