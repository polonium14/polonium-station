using Content.Shared.Silicons.Laws;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.StationAi;

/// <summary>
/// Stores a silicon entity's pre-epsilon laws for restoration when lockdown ends.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class EpsilonLawBackupComponent : Component
{
    [DataField(required: true)]
    public SiliconLawset Lawset = new();
}
