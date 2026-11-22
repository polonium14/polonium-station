namespace Content.Server._Polonium.Paper;

/// <summary>
/// Who has this paper open for writing.
/// </summary>
[RegisterComponent, UnsavedComponent, Access(typeof(PaperWriteGuardSystem))]
public sealed partial class PaperWritersComponent : Component
{
    /// <summary>
    /// Writers, each with whether their tool can write over stamps.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, bool> Writers = new();
}
