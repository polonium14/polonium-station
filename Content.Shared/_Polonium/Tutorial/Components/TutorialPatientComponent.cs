using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Tutorial.Components;

// networked so the client predicts the same medicine refusals the server makes
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialPatientComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool SpawnedDead;
}
