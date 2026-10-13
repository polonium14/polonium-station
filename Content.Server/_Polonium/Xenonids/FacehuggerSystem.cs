using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Polonium.Xenonids;
using Content.Shared.Database;
using Content.Shared.Popups;
using Robust.Shared.Containers;

namespace Content.Server._Polonium.Xenonids;

public sealed partial class FacehuggerSystem : SharedFacehuggerSystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private XenoEmbryoSystem _embryo = default!;

    private static readonly TimeSpan CheckDelay = TimeSpan.FromSeconds(0.5);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = Timing.CurTime;
        var query = EntityQueryEnumerator<FacehuggerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var hugger, out var xform))
        {
            if (time < hugger.NextCheck)
                continue;

            hugger.NextCheck = time + CheckDelay;
            var ent = new Entity<FacehuggerComponent>(uid, hugger);

            if (hugger.ImpregnateAt is { } impregnateAt && time >= impregnateAt)
            {
                hugger.ImpregnateAt = null;
                Impregnate(ent);
                continue;
            }

            if (hugger.State is FacehuggerState.Dead or FacehuggerState.Impregnated)
                continue;

            // Na twarzy albo w kieszeni nie poluje i nie marznie ani nie płonie.
            if (_container.IsEntityInContainer(uid))
                continue;

            if (_atmosphere.GetContainingMixture((uid, xform)) is { } air && air.Temperature > hugger.MaxTemperature)
            {
                Die(ent);
                continue;
            }

            if (hugger.State == FacehuggerState.Idle)
            {
                hugger.WakeAt ??= time + RandomTime(hugger.MinIdleTime, hugger.MaxIdleTime);
                if (time < hugger.WakeAt)
                    continue;

                hugger.WakeAt = null;
                SetState(ent, FacehuggerState.Active);
            }

            TryLeapAtNearby(ent, xform);
        }
    }

    private void Impregnate(Entity<FacehuggerComponent> hugger)
    {
        if (hugger.Comp.Victim is not { } victim || TerminatingOrDeleted(victim))
            return;

        // Zerwany na czas albo ofiara już nie żyje.
        if (!Inventory.TryGetSlotEntity(victim, MaskSlot, out var mask) || mask != hugger.Owner)
            return;

        if (!CanHug(victim, checkHead: false, hugger: hugger))
            return;

        if (hugger.Comp.Sterile)
        {
            PopupBoth(victim,
                Loc.GetString("xeno-facehugger-sterile-self", ("hugger", hugger.Owner)),
                Loc.GetString("xeno-facehugger-sterile-others", ("hugger", hugger.Owner), ("target", victim)),
                PopupType.Medium);
            return;
        }

        PopupBoth(victim,
            Loc.GetString("xeno-facehugger-impregnate-self", ("hugger", hugger.Owner)),
            Loc.GetString("xeno-facehugger-impregnate-others", ("hugger", hugger.Owner), ("target", victim)),
            PopupType.MediumCaution);

        Die(hugger, impregnated: true);

        if (_embryo.TryInfect(victim))
            _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(victim):player} was impregnated by facehugger {ToPrettyString(hugger):entity}");
    }
}
