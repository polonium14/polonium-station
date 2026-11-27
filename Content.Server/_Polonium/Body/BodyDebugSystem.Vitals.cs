using Content.Server._Shitmed.Medical.Tourniquet;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._Polonium.Body;
using Content.Shared._Polonium.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Temperature.Components;
using Content.Shared.Temperature.Systems;

namespace Content.Server._Polonium.Body;

/// <summary>
/// The body debug window's whole-body readings and the switches for the surgery, lobotomy, cavity and tourniquet
/// states of a body part.
/// </summary>
public sealed partial class BodyDebugSystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private CirculationSystem _circulation = default!;
    [Dependency] private RespiratorSystem _respirator = default!;
    [Dependency] private SharedSurgerySystem _surgery = default!;
    [Dependency] private SharedTemperatureSystem _temperature = default!;
    [Dependency] private SurgicalCavitySystem _cavity = default!;
    [Dependency] private TourniquetSystem _tourniquet = default!;

    public BodyDebugVitals GetVitals(EntityUid body)
    {
        TryComp<BloodstreamComponent>(body, out var bloodstream);
        TryComp<RespiratorComponent>(body, out var respirator);
        var normalBlood = bloodstream?.BloodReferenceSolution.Volume ?? FixedPoint2.Zero;

        return new BodyDebugVitals(
            bloodstream != null ? normalBlood * _bloodstream.GetBloodLevel((body, bloodstream)) : null,
            normalBlood,
            CompOrNull<TemperatureComponent>(body)?.Temperature,
            respirator?.Saturation,
            respirator?.MinSaturation ?? 0f,
            respirator?.MaxSaturation ?? 0f,
            _circulation.IsStopped(body),
            HasComp<CirculationArrestComponent>(body));
    }

    /// <summary>
    /// Sets the body's blood to the given volume.
    /// </summary>
    public bool TrySetBloodVolume(EntityUid body, FixedPoint2 volume)
    {
        if (!TryComp<BloodstreamComponent>(body, out var bloodstream))
            return false;

        var current = bloodstream.BloodReferenceSolution.Volume * _bloodstream.GetBloodLevel((body, bloodstream));
        return _bloodstream.TryModifyBloodLevel((body, bloodstream), volume - current);
    }

    /// <summary>
    /// Sets the body's temperature in kelvin by adding or taking away the heat that takes.
    /// </summary>
    public bool TrySetTemperature(EntityUid body, float kelvin)
    {
        if (!TryComp<TemperatureComponent>(body, out var temperature) || kelvin <= 0f)
            return false;

        _temperature.ChangeHeat((body, temperature), (kelvin - temperature.Temperature) * temperature.HeatCapacity, ignoreHeatResistance: true);
        return true;
    }

    /// <summary>
    /// Sets the body's oxygen saturation, within what its respirator allows.
    /// </summary>
    public bool TrySetSaturation(EntityUid body, float saturation)
    {
        if (!TryComp<RespiratorComponent>(body, out var respirator))
            return false;

        _respirator.UpdateSaturation(body, saturation - respirator.Saturation, respirator);
        return true;
    }

    /// <summary>
    /// Stops the circulation of a body with a working heart, or lets it run again, for testing CPR.
    /// </summary>
    public bool TryToggleCirculation(EntityUid body)
    {
        if (!HasComp<BodyComponent>(body))
            return false;

        if (!RemComp<CirculationArrestComponent>(body))
            AddComp<CirculationArrestComponent>(body);

        _circulation.Refresh(body);
        return true;
    }

    public bool TryToggleLobotomy(EntityUid body, EntityUid limb)
    {
        if (!TryGetOrgan(body, limb, out _))
            return false;

        if (!RemComp<LobotomizedComponent>(limb))
            AddComp<LobotomizedComponent>(limb);

        return true;
    }

    public bool TryEjectCavity(EntityUid body, EntityUid limb)
    {
        return TryGetOrgan(body, limb, out _) && _cavity.DropContents(limb);
    }

    /// <summary>
    /// Clears the marks an interrupted surgery left on a body part. A lobotomy is a result, not a mark, so it stays.
    /// </summary>
    public bool TryResetSurgery(EntityUid body, EntityUid limb)
    {
        if (!TryGetOrgan(body, limb, out _))
            return false;

        var lobotomized = HasComp<LobotomizedComponent>(limb);
        _surgery.ResetSurgery(limb);
        if (lobotomized)
            EnsureComp<LobotomizedComponent>(limb);

        return true;
    }

    public bool TryRemoveTourniquet(EntityUid body, EntityUid limb)
    {
        return TryGetOrgan(body, limb, out _) && _tourniquet.TryTakeOffTourniquet(body, limb);
    }

    private BodyDebugLimb WithStates(BodyDebugLimb row, EntityUid limb)
    {
        return row with
        {
            Lobotomized = HasComp<LobotomizedComponent>(limb),
            CavityItem = _cavity.GetContents(limb) is { } item ? Name(item) : null,
            UnfinishedSurgery = _surgery.HasUnfinishedSurgerySteps(limb),
            Tourniquet = HasComp<TourniquetedComponent>(limb),
        };
    }
}
