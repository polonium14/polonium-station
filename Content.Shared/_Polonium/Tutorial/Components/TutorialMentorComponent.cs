namespace Content.Shared._Polonium.Tutorial.Components;

[RegisterComponent]
public sealed partial class TutorialMentorComponent : Component
{
    [ViewVariables]
    public EntityUid OwnerPlayer;

    [ViewVariables]
    public EntityUid? CurrentPad;

    [ViewVariables]
    public Queue<TutorialSpeechLine> SpeechQueue = new();

    [ViewVariables]
    public TimeSpan NextSpeak;

    /// <summary>End of the reading gap after the last reaction line she said.</summary>
    [ViewVariables]
    public TimeSpan QuipDoneAt;
}

/// <summary>
/// One queued N.A.N.C.I. line. A line can wait for the trainee to actually reach a place before
/// it is said - otherwise she briefs the next room while the player is still in the old one.
/// </summary>
public sealed class TutorialSpeechLine
{
    public string Text = string.Empty;

    /// <summary>Anchor the player has to be near. Null = say it right away.</summary>
    public string? GateAnchor;

    public float GateRange;

    /// <summary>Gate opens by itself here, a wandering player should never lose the briefing.</summary>
    public TimeSpan GateExpiresAt;

    /// <summary>Part of a step briefing rather than a quip. Only these arm freezeWhileSpeaking.</summary>
    public bool FromStep;
}
