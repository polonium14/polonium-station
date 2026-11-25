using System.Text;
using Content.Shared._Polonium.Body;
using Content.Shared.Chat;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Polonium.Body;

/// <summary>
/// Bad hearing from damaged or missing ears. Hard of hearing listeners lose letters of what is said around them;
/// deaf ones only make out a little from right next to the speaker, and a radio in the ear half-way.
/// </summary>
[RegisterComponent]
[Access(typeof(SensesSystem), typeof(HearingSystem))]
public sealed partial class HearingLossComponent : Component
{
    /// <summary>
    /// No working ears at all.
    /// </summary>
    [DataField]
    public bool Deaf;

    /// <summary>
    /// Chance that each letter of nearby speech is lost, for ears that still work.
    /// </summary>
    [DataField]
    public float Garble;
}

/// <inheritdoc cref="HearingLossComponent"/>
public sealed partial class HearingSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;

    /// <summary>
    /// Share of letters a deaf listener still makes out right next to the speaker.
    /// </summary>
    private const float DeafCloseHeard = 0.5f;

    /// <summary>
    /// Distance at which a deaf listener stops making out anything.
    /// </summary>
    private const float DeafRange = 4f;

    /// <summary>
    /// Share of radio letters a deaf listener loses. The earpiece sits right in the ear.
    /// </summary>
    private const float DeafRadioGarble = 0.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HearingLossComponent, ListenerHearsMessageEvent>(OnHears);
    }

    /// <summary>
    /// Lets the listener's hearing change a radio message it is about to get. Returns the message as heard.
    /// </summary>
    public static MsgChatMessage Hear(IEntityManager entMan, EntityUid listener, MsgChatMessage message)
    {
        var heard = new ListenerHearsMessageEvent(ChatChannel.Radio, 0f, message.Message.Message, message.Message.WrappedMessage);
        entMan.EventBus.RaiseLocalEvent(listener, ref heard);

        if (heard.WrappedMessage == message.Message.WrappedMessage)
            return message;

        var chat = message.Message;
        return new MsgChatMessage
        {
            Message = new ChatMessage(chat.Channel,
                heard.Message,
                heard.WrappedMessage,
                chat.SenderEntity,
                chat.SenderKey,
                chat.HideChat,
                chat.MessageColorOverride,
                chat.AudioPath,
                chat.AudioVolume,
                chat.AllowHyperlinks),
        };
    }

    private void OnHears(Entity<HearingLossComponent> ent, ref ListenerHearsMessageEvent args)
    {
        var chance = args.Channel switch
        {
            ChatChannel.Radio => ent.Comp.Deaf ? DeafRadioGarble : ent.Comp.Garble / 2f,
            ChatChannel.Local or ChatChannel.Whisper => ent.Comp.Deaf ? DeafGarble(args.Distance) : ent.Comp.Garble,
            _ => 0f,
        };

        if (chance <= 0f)
            return;

        var garbled = Garble(args.Message, chance);
        if (garbled == args.Message)
            return;

        // The wrapped text holds the message escaped for markup, swap that part only.
        // Only its last occurrence: a short message can also appear inside the speaker's name or the markup.
        var escaped = FormattedMessage.EscapeText(args.Message);
        var (find, replacement) = args.WrappedMessage.Contains(escaped)
            ? (escaped, FormattedMessage.EscapeText(garbled))
            : (args.Message, garbled);
        var at = args.WrappedMessage.LastIndexOf(find, StringComparison.Ordinal);
        if (at >= 0)
            args.WrappedMessage = args.WrappedMessage[..at] + replacement + args.WrappedMessage[(at + find.Length)..];

        args.Message = garbled;
    }

    /// <summary>
    /// Right next to the speaker a deaf listener loses half the letters, from <see cref="DeafRange"/> on all of them.
    /// </summary>
    private static float DeafGarble(float distance)
    {
        var heard = DeafCloseHeard * Math.Clamp((DeafRange - distance) / (DeafRange - 1f), 0f, 1f);
        return 1f - heard;
    }

    /// <summary>
    /// Replaces each letter with "~" at the given chance, like a whisper from afar.
    /// </summary>
    public string Garble(string message, float chance)
    {
        var builder = new StringBuilder(message);
        for (var i = 0; i < builder.Length; i++)
        {
            if (!char.IsWhiteSpace(builder[i]) && _random.Prob(chance))
                builder[i] = '~';
        }

        return builder.ToString();
    }
}
