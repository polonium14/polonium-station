using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Body;

/// <summary>
/// Kidneys that work worse than healthy ones. Chemistry in the body runs slower: drugs, poisons and drinks work
/// longer for the same total effect.
/// A body with healthy kidneys, or one that never had any, does not have this component.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class KidneyFunctionComponent : Component
{
    /// <summary>
    /// 1 for healthy kidneys, lower for sick, dead or missing ones.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Rate = 1f;
}

/// <summary>
/// Raised on a body before one of its organs metabolizes chemicals. Not raised for breathing.
/// </summary>
[ByRefEvent]
public record struct GetChemicalMetabolismRateEvent()
{
    /// <summary>
    /// 1 is the normal pace, 0.5 takes twice as long per metabolism step.
    /// </summary>
    public float Rate = 1f;
}

public sealed partial class KidneyFunctionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KidneyFunctionComponent, GetChemicalMetabolismRateEvent>(OnGetMetabolismRate);
    }

    private void OnGetMetabolismRate(Entity<KidneyFunctionComponent> ent, ref GetChemicalMetabolismRateEvent args)
    {
        args.Rate *= ent.Comp.Rate;
    }
}
