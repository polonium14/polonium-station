using Content.Shared._Polonium.Tutorial;
using Content.Shared.Chat;

namespace Content.Client.Chat.UI;

public abstract partial class SpeechBubble
{
    private TimeSpan GetLifetime(ChatMessage message, EntityUid sender)
    {
        var proto = EntityManager.GetComponent<MetaDataComponent>(sender).EntityPrototype;
        if (proto?.ID != TutorialMentorReading.MentorProto.Id)
            return TotalTime;

        var readTime = TutorialMentorReading.ReadTime(message.Message.Length);
        return readTime > TutorialMentorReading.MinBubbleTime ? readTime : TutorialMentorReading.MinBubbleTime;
    }
}
