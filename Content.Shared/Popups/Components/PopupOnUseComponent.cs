using Robust.Shared.GameStates;

namespace Content.Shared.Popups.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class PopupOnUseComponent : Component
{
    public const float UseDelaySeconds = 1f;

    [DataField(required: true)] public string Message = string.Empty;

    [DataField] public bool ShowToOthers = false;

    [DataField] public string PopupSize = "Medium";

    [DataField] public float Range = 5f;

    [ViewVariables(VVAccess.ReadOnly)] public TimeSpan LastUsed = TimeSpan.MinValue;
}
