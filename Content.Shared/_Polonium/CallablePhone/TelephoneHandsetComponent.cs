using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.CallablePhone;

/// <summary>
/// Handset item linked to a callable phone base.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TelephoneHandsetComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity ParentPhone;
}
