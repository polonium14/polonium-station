using Content.Shared.Chat;

namespace Content.Shared._Polonium.Body;

/// <summary>
/// Raised on a listener right before a spoken, whispered or radio message reaches them.
/// Handlers may change what the listener gets, e.g. garble it for bad hearing.
/// </summary>
/// <param name="Channel">Local, Whisper, Radio or another chat channel.</param>
/// <param name="Distance">Distance to the speaker, 0 for radio.</param>
/// <param name="Message">The bare text.</param>
/// <param name="WrappedMessage">The text as shown in chat, with the speaker's name and markup.</param>
[ByRefEvent]
public record struct ListenerHearsMessageEvent(ChatChannel Channel, float Distance, string Message, string WrappedMessage);
