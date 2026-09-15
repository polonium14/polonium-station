using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Tutorial;

/// <summary>
/// Sent client → server when the player clicks the acknowledge button on a tutorial bubble.
/// Server only advances if the current step actually has a ManualAcknowledgeCondition, so
/// spamming this from a modded client does nothing useful.
/// </summary>
[Serializable, NetSerializable]
public sealed class TutorialAcknowledgeStepEvent : EntityEventArgs
{
    /// <summary>Step id that was on screen when the player clicked — guards against stale clicks.</summary>
    public string StepId { get; }

    public TutorialAcknowledgeStepEvent(string stepId)
    {
        StepId = stepId;
    }
}
