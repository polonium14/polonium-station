using Content.Shared.Dataset;
using Content.Shared._RMC14.Xenonids.Name;
using Content.Shared._RMC14.Xenonids.Rank;
using Content.Shared.GameTicking;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Random.Helpers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._RMC14.Xenonids.Name;

public sealed class XenoNameSystem : SharedXenoNameSystem
{
    [Dependency] private readonly NameModifierSystem _nameModifier = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly ProtoId<LocalizedDatasetPrototype> PrefixDataset = "NamesXenoPrefix";

    private readonly List<int> _available = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _available.Clear();
        for (var i = 1; i < 1000; i++)
            _available.Add(i);
    }
    public override void SetupName(EntityUid xeno)
    {
        base.SetupName(xeno);

        if (!TryComp(xeno, out ActorComponent? actor))
            return;

        if (_available.Count == 0)
        {
            for (var i = 1; i < 1000; i++)
                _available.Add(i);
        }

        try
        {
            var name = EnsureComp<XenoNameComponent>(xeno);
            EnsureComp<XenoRankComponent>(xeno);

            if (_proto.TryIndex(PrefixDataset, out var prefixDataset))
                name.Prefix = _random.Pick(prefixDataset);

            name.Number = _available.Count == 0 ? _random.Next(1, 1000) : _random.PickAndTake(_available);
            _nameModifier.RefreshNameModifiers(xeno);
            RemCompDeferred<AssignXenoNameComponent>(xeno);
        }
        catch (Exception e)
        {
            Log.Error($"Error setting up xeno name for {ToPrettyString(xeno)}:\n{e}");
        }
    }
}
