using Content.Shared._Polonium.Abductors;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Abductors;

public sealed partial class AbductorShopSystem : EntitySystem
{
    private const int WheatBundles = 3;

    private static readonly EntProtoId Cow = "MobCow";
    private static readonly EntProtoId Wheat = "WheatBushel";
    private static readonly EntProtoId Sparks = "EffectSparks";
    private static readonly SoundSpecifier ArrivalSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");

    [Dependency] private AbductorShipSystem _ship = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AbductorComponent, AbductorCowDeliveryEvent>(OnCowDelivery);
    }

    private void OnCowDelivery(Entity<AbductorComponent> ent, ref AbductorCowDeliveryEvent args)
    {
        var coordinates = _ship.TryGetShip(ent, out var ship) && _ship.TryGetTelepad(ship, out var telepad)
            ? Transform(telepad).Coordinates
            : Transform(ent).Coordinates;

        SpawnAtPosition(Cow, coordinates);
        for (var i = 0; i < WheatBundles; i++)
        {
            SpawnAtPosition(Wheat, coordinates);
        }

        SpawnAtPosition(Sparks, coordinates);
        _audio.PlayPvs(ArrivalSound, coordinates);
    }
}
