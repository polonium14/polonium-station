using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Polonium.Medical.Cpr;

/// <summary>
/// A body whose heart someone is pushing blood with. While it lasts the body keeps its oxygen and does not rot.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(CprSystem))]
public sealed partial class ChestCompressionsComponent : Component
{
    /// <summary>
    /// When the last cycle stops holding the body, a little after the next cycle is due.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan Until;
}
