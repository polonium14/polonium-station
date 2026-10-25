using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Modules.Components.Modules;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PowerDrainModuleComponent : Component
{
    [DataField, AutoNetworkedField]
    public float? EnabledDraw;

    [DataField, AutoNetworkedField]
    public float? DisabledDraw;

    [DataField, AutoNetworkedField]
    public float? OnUseDraw;
}
