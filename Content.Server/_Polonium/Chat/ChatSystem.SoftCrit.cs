using Content.Shared.Chat;
using Content.Shared.Radio;

// Polonium: a partial of the upstream system, so its namespace follows the upstream one.
namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    /// <summary>
    /// A mob in soft crit can only whisper, and not into its headset: speech becomes a whisper
    /// and a radio channel is dropped, so the message stays a plain whisper.
    /// </summary>
    private void ApplySoftCritSpeech(EntityUid source, bool ignoreActionBlocker, ref InGameICChatType desiredType)
    {
        if (ignoreActionBlocker || !_mobStateSystem.IsSoftCritical(source))
            return;

        if (desiredType == InGameICChatType.Speak)
            desiredType = InGameICChatType.Whisper;
    }

    /// <inheritdoc cref="ApplySoftCritSpeech"/>
    private RadioChannelPrototype? SoftCritRadioChannel(EntityUid source, bool ignoreActionBlocker, RadioChannelPrototype? channel)
    {
        if (channel == null || ignoreActionBlocker || !_mobStateSystem.IsSoftCritical(source))
            return channel;

        return null;
    }
}
