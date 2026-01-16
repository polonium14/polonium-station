namespace Content.Server.Administration.Components;

[RegisterComponent]
public sealed partial class AdminAlertOnUseComponent : Component
{
    [DataField]
    public string Message = string.Empty;
}
