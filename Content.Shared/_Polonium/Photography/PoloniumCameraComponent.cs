using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Photography;

/// <summary>Marks an item as a real pixel-capturing camera: unlike upstream <c>PictureTaker</c>'s examine-text photo, it asks the shooting client to render and submit an actual image.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PoloniumCameraComponent : Component
{
    /// <summary>Photograph entity spawned once a valid image comes back; the blob is stored server-side by id, this entity only carries the id.</summary>
    [DataField]
    public EntProtoId Photograph = "PoloniumPhotograph";
}
