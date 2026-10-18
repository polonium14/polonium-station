using Content.Shared.Buckle.Components;
using Content.Shared.Construction;
using Content.Shared.Examine;
using JetBrains.Annotations;

namespace Content.Server._Polonium.Polish;

/// <summary>
/// Warunek konstrukcji: nikt nie jest przypięty do obiektu (np. nie da się rozkręcić krzyża z ofiarą).
/// </summary>
[UsedImplicitly]
[DataDefinition]
public sealed partial class StrapEmpty : IGraphCondition
{
    public bool Condition(EntityUid uid, IEntityManager entityManager)
    {
        return !entityManager.TryGetComponent<StrapComponent>(uid, out var strap) || strap.BuckledEntities.Count == 0;
    }

    public bool DoExamine(ExaminedEvent args)
    {
        if (Condition(args.Examined, IoCManager.Resolve<IEntityManager>()))
            return false;

        args.PushMarkup(Loc.GetString("construction-examine-condition-strap-empty", ("entityName", args.Examined)) + "\n");
        return true;
    }

    public IEnumerable<ConstructionGuideEntry> GenerateGuideEntry()
    {
        yield return new ConstructionGuideEntry
        {
            Localization = "construction-guide-condition-strap-empty",
        };
    }
}
