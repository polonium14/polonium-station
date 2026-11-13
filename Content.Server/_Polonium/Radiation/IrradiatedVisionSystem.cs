using Content.Server.Radiation.Components;
using Content.Server.Radiation.Events;
using Content.Server.Radiation.Systems;
using Content.Shared._Polonium.Radiation;
using Content.Shared.Radiation.Components;
using Robust.Shared.Player;

namespace Content.Server._Polonium.Radiation;

/// <summary>
/// Tells players how strong the radiation around them is, by the same levels a Geiger counter uses.
/// </summary>
public sealed partial class IrradiatedVisionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadiationSystemUpdatedEvent>(OnRadiationUpdated);
    }

    private void OnRadiationUpdated(RadiationSystemUpdatedEvent ev)
    {
        var query = EntityQueryEnumerator<RadiationReceiverComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out var receiver, out _))
        {
            var level = GeigerSystem.RadsToLevel(receiver.CurrentRadiation);

            if (level < GeigerDangerLevel.Med)
            {
                RemComp<IrradiatedVisionComponent>(uid);
                continue;
            }

            var vision = EnsureComp<IrradiatedVisionComponent>(uid);
            if (vision.Level == level)
                continue;

            vision.Level = level;
            Dirty(uid, vision);
        }
    }
}
