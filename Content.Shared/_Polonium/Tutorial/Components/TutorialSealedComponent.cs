using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Tutorial.Components;

/// <summary>
/// Point of no return. Panel, pry and weld stop here. Emag is a tag on the airlock itself.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TutorialSealedComponent : Component;
