using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.Modules.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ModuleContainerPowerComponent : Component
{
    [DataField, AutoNetworkedField]
    public float BaseRate;

    [DataField, AutoNetworkedField]
    public TimeSpan UiUpdateInterval = TimeSpan.FromSeconds(1.5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextUiUpdate;
}
