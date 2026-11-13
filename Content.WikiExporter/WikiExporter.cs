using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.WikiExporter;

/// <summary>
/// One data set for the wiki. Every non-abstract subclass is found by <see cref="Program"/> and written
/// to <c>&lt;Name&gt;.json</c>; dependencies are injected before <see cref="Export"/> runs on the server thread.
/// </summary>
public abstract partial class WikiExporter
{
    [Dependency] protected IComponentFactory Factory = default!;
    [Dependency] protected IEntitySystemManager EntitySystems = default!;
    [Dependency] protected IPrototypeManager Proto = default!;

    /// <summary>
    /// Name used on the command line and as the output file name.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Builds the data set. The result is serialized with <see cref="Program.JsonOptions"/>.
    /// </summary>
    public abstract object Export();
}
