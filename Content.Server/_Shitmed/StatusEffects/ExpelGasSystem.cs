using Content.Shared._Shitmed.StatusEffects;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Robust.Shared.Random;

namespace Content.Server._Shitmed.StatusEffects;

public sealed partial class ExpelGasEffectSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ExpelGasComponent, ComponentInit>(OnInit);
    }

    private void OnInit(EntityUid uid, ExpelGasComponent component, ComponentInit args)
    {
        var mix = _atmos.GetContainingMixture((uid, Transform(uid)), true, true) ?? new();
        var gas = _random.Pick(component.PossibleGases);
        mix.AdjustMoles(gas, 60);
        _chat.TryEmoteWithChat(uid, "Fart");
    }
}
