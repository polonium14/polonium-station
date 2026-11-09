using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial;

/// <summary>
/// How long the tutorial holopad gives the trainee to read a line.
/// </summary>
public static class TutorialMentorReading
{
    public static readonly EntProtoId MentorProto = "TutorialHoloMentor";

    public static readonly TimeSpan MinBubbleTime = TimeSpan.FromSeconds(3);

    private const double BaseSeconds = 0.8;
    private const double CharsPerSecond = 26;

    public static TimeSpan ReadTime(int length)
    {
        return TimeSpan.FromSeconds(BaseSeconds + length / CharsPerSecond);
    }
}
