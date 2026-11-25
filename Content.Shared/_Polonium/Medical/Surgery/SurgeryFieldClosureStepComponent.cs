using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Medical.Surgery;

/// <summary>
/// Surgery step that closes a sawed-open part without sealing its bones: the incision is gone,
/// the bone is left broken for a later mend-bones surgery.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryFieldClosureStepComponent : Component;
