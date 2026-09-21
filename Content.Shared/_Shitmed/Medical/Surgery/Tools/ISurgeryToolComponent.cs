namespace Content.Shared._Shitmed.Medical.Surgery.Tools;

public interface ISurgeryToolComponent
{
    /// <summary>
    ///     Localization key for the tool name. Pass it to the surrounding message so
    ///     its translation can resolve the name with the appropriate grammatical case.
    /// </summary>
    public string ToolName { get; }

    /// <summary>
    ///     Field intended for discardable or non-reusable tools.
    /// </summary>
    public bool? Used { get; set; }

    /// <summary>
    ///     Multiply the step's doafter by this value.
    ///     This is per-type so you can have something that's a good scalpel but a bad retractor.
    /// </summary>
    public float Speed { get; set; }
}
