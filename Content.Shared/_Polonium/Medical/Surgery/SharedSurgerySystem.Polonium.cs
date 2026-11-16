using Content.Shared._Polonium.Medical.Surgery;

// The Shitmed system's namespace, so this stays one partial class with it.
namespace Content.Shared._Shitmed.Medical.Surgery;

/// <summary>
/// Polonium step types for <see cref="IsStepComplete"/>, which only knows the step types listed in it.
/// </summary>
public abstract partial class SharedSurgerySystem
{
    [Dependency] private SurgicalCavitySystem _cavity = default!;

    private bool PoloniumStepComplete(EntityUid step, EntityUid part)
    {
        return _cavity.IsStepComplete(step, part);
    }
}
