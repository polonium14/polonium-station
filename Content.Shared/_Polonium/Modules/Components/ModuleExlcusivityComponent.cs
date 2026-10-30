using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Modules.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModuleExclusivityComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityWhitelist? Whitelist;

    [DataField, AutoNetworkedField]
    public EntityWhitelist? Blacklist;

    [DataField, AutoNetworkedField]
    public int Maximum = int.MaxValue;

    [DataField, AutoNetworkedField]
    public int Minimum = int.MinValue;

    [DataField, AutoNetworkedField]
    public LocId? Popup = "modsuit-exclusivity-generic";
}
