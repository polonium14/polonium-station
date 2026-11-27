namespace Content.Shared._Polonium.Chat;

/// <summary>
/// Raised on a speaker before its in-character message goes out. A system that handles it sends the message
/// somewhere else (like the eldritch horror's silent talk with its host), so the chat drops it.
/// </summary>
[ByRefEvent]
public record struct PoloniumSpeechRedirectEvent(string Message, bool Handled = false);
