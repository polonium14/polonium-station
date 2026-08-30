using Content.Client.TextScreen;
using NUnit.Framework;

namespace Content.Tests.Client
{
    [TestFixture]
    public sealed class TextScreenStateTest
    {
        // Every state here must exist in Resources/Textures/Effects/text.rsi.
        [Test]
        [TestCase('Ć', "cacute")]
        [TestCase('ć', "cacute")]
        [TestCase('Ż', "zdot")]
        [TestCase('Ł', "lstroke")]
        [TestCase('A', "a")]
        [TestCase('7', "7")]
        [TestCase(' ', "blank")]
        [TestCase('~', null)]
        public void GetStateFromChar(char chr, string expected)
        {
            Assert.That(TextScreenSystem.GetStateFromChar(chr), Is.EqualTo(expected));
        }
    }
}
