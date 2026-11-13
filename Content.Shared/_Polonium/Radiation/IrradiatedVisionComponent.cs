using Content.Shared.Radiation.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Radiation;

/// <summary>
/// Makes radiation speckles appear on the screen of a player standing in strong radiation.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IrradiatedVisionComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [AutoNetworkedField]
    public GeigerDangerLevel Level;
}
