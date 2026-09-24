using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Body;

/// <summary>Holds a detached part's descendant organs until it is reattached.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DismemberedPartComponent : Component
{
    public const string ContainerId = "dismembered-part-organs";
}
