using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Polonium.Chat.Emoji;

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
    public async Task ReplacesOnlyKnownCodes()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(_sEmoji.EscapeWithEmojis("hi :test_emoji:!"), Is.EqualTo($"hi {Tag}!"));
                Assert.That(_sEmoji.EscapeWithEmojis(":test_emoji::test_emoji:"), Is.EqualTo(Tag + Tag));
                Assert.That(_sEmoji.EscapeWithEmojis("12:00:test_emoji:"), Is.EqualTo($"12:00{Tag}"));
                Assert.That(_sEmoji.EscapeWithEmojis(":unknown: at 12:30"), Is.EqualTo(":unknown: at 12:30"));
                Assert.That(_sEmoji.EscapeWithEmojis("[bold]:test_emoji:"), Is.EqualTo($"\\[bold]{Tag}"));
            });
        });
    }
}
