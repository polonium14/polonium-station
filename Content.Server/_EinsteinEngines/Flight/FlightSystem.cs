using Content.Shared._EinsteinEngines.Flight;
using Content.Shared.Damage.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._EinsteinEngines.Flight;

public sealed partial class FlightSystem : SharedFlightSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<FlightComponent>();
        while (query.MoveNext(out var uid, out var flight))
        {
            if (!flight.On || flight.NextFlap > curTime)
                continue;

            flight.NextFlap = curTime + flight.FlapInterval;
            _audio.PlayPvs(flight.FlapSound, uid);
            _stamina.TakeStaminaDamage(uid, flight.StaminaDrainRate * (float) flight.FlapInterval.TotalSeconds, visual: false);
        }
    }
}
