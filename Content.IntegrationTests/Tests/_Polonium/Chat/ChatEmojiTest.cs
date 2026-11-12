using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Polonium.Chat.Emoji;
using Content.Shared.CCVar;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Polonium.Chat;

[TestOf(typeof(ChatEmojiSystem))]
public sealed class ChatEmojiTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: chatEmoji
  id: ChatEmojiTestEmoji
  code: test_emoji
  sprite: /Textures/_Polonium/Interface/Chat/Emoji/idz.png
";

    private const string Tag = "[emoji=\"ChatEmojiTestEmoji\"/]";

    [SidedDependency(Side.Server)] private readonly ChatEmojiSystem _sEmoji = null!;

    [Test]
    [TestCase("hi :test_emoji:!", $"hi {Tag}!")]
    [TestCase(":test_emoji::test_emoji:", Tag + Tag)]
    [TestCase("12:00:test_emoji:", $"12:00{Tag}")]
    [TestCase(":unknown: at 12:30", ":unknown: at 12:30")]
    [TestCase("[bold]:test_emoji:", $"\\[bold]{Tag}")]
    [TestCase("\\:test_emoji:", $"\\\\{Tag}")]
    public async Task ReplacesOnlyKnownCodes(string message, string expected)
    {
        await Server.WaitAssertion(() =>
        {
            var result = _sEmoji.EscapeWithEmojis(message);
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(FormattedMessage.ValidMarkup(result));
        });
    }

    [Test]
    public async Task LimitsEmojisPerMessage()
    {
        await OverrideCVar(Side.Server, CCVars.ChatEmojiLimit, 2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_sEmoji.EscapeWithEmojis(":test_emoji::test_emoji::test_emoji:"),
                Is.EqualTo($"{Tag}{Tag}:test_emoji:"));
        });
    }
}
