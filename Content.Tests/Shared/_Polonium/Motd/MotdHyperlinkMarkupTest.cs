using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Client._Polonium.RichText;
using Content.Client.Administration.UI.Bwoink;
using Content.Client.RichText;
using Content.Shared._Polonium.Motd;
using Content.Shared.Chat;
using NUnit.Framework;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Utility;

namespace Content.Tests.Shared._Polonium.Motd;

[Parallelizable]
[TestFixture]
[TestOf(typeof(MotdHyperlinkMarkup))]
public sealed class MotdHyperlinkMarkupTest
{
    [Test]
    public void LeavesPlainTextAlone()
    {
        Assert.That(MotdHyperlinkMarkup.Apply("hello crew"), Is.EqualTo("hello crew"));
    }

    [Test]
    public void DoesNotLinkifyBareHttps()
    {
        const string input = "join https://example.com/discord please";
        Assert.That(MotdHyperlinkMarkup.Apply(input), Is.EqualTo(input));
        Assert.That(FormattedMessage.FromMarkupPermissive(input).Any(n => n.Name == MotdHyperlinkMarkup.TagName), Is.False);
    }

    [Test]
    public void LeavesBareUrlPunctuationAlone()
    {
        const string input = "see https://example.com.";
        Assert.That(MotdHyperlinkMarkup.Apply(input), Is.EqualTo(input));
    }

    [Test]
    public void ConvertsMarkdownBesideBareUrl()
    {
        var result = MotdHyperlinkMarkup.Apply("plain https://example.com/wiki and [Discord](https://example.com/discord)");
        Assert.That(result, Is.EqualTo("plain https://example.com/wiki and [url=\"Discord\" href=\"https://example.com/discord\"/]"));
        Assert.That(FormattedMessage.ValidMarkup(result));
    }

    [Test]
    public void ConvertsMarkdownLinks()
    {
        var result = MotdHyperlinkMarkup.Apply("read [the wiki](https://wiki.example.com/foo)");
        Assert.That(result, Is.EqualTo("read [url=\"the wiki\" href=\"https://wiki.example.com/foo\"/]"));
        Assert.That(FormattedMessage.ValidMarkup(result));
    }

    [Test]
    public void KeepsExistingUrlTags()
    {
        const string input = "go [url=\"Discord\" href=\"https://discord.gg/x\"/] now";
        Assert.That(MotdHyperlinkMarkup.Apply(input), Is.EqualTo(input));
    }

    [Test]
    public void DoesNotLinkifyBareUrlInsideColorTags()
    {
        const string input = "[color=red]https://a.com[/color]";
        Assert.That(MotdHyperlinkMarkup.Apply(input), Is.EqualTo(input));
    }

    [Test]
    public void LeavesBareUrlWithParentheses()
    {
        const string input = "https://en.wikipedia.org/wiki/Foo_(bar)";
        Assert.That(MotdHyperlinkMarkup.Apply(input), Is.EqualTo(input));
        Assert.That(FormattedMessage.FromMarkupPermissive(input).Any(n => n.Name == MotdHyperlinkMarkup.TagName), Is.False);
    }

    [Test]
    public void IgnoresJavascriptUrls()
    {
        Assert.That(MotdHyperlinkMarkup.Apply("javascript:alert(1)"), Is.EqualTo("javascript:alert(1)"));
        Assert.That(MotdHyperlinkMarkup.Apply("[x](javascript:alert(1))"), Is.EqualTo("[x](javascript:alert(1))"));
        Assert.That(MotdHyperlinkMarkup.IsSafeHttpUrl("javascript:alert(1)"), Is.False);
        Assert.That(MotdHyperlinkMarkup.IsSafeHttpUrl("https://example.com"), Is.True);
        Assert.That(MotdHyperlinkMarkup.IsSafeHttpUrl("https://"), Is.False);
    }

    [Test]
    public void ApplyForChatFallsBackWhenMarkupIsBroken()
    {
        var result = MotdHyperlinkMarkup.ApplyForChat("oops [unclosed");
        Assert.That(result, Is.EqualTo(FormattedMessage.EscapeText("oops [unclosed")));
    }

    [TestCase("[url=\"click me\" href=\"https://evil.example/\"/]")]
    [TestCase("[url=https://evil.example/]click me[/url]")]
    [TestCase("https://evil.example/wiki")]
    public void EscapedPlayerTextDoesNotParseAsUrlTag(string payload)
    {
        // chat + announcements EscapeText this before markup
        var escaped = FormattedMessage.EscapeText(payload);
        var parsed = FormattedMessage.FromMarkupPermissive(escaped);

        Assert.That(parsed.Any(n => n.Name == MotdHyperlinkMarkup.TagName), Is.False);
        Assert.That(parsed.ToString(), Does.Contain(payload.Contains('[') ? "[url" : "https://"));
    }
}

[NonParallelizable]
[TestFixture]
[TestOf(typeof(UrlTag))]
public sealed class UrlTagRestrictedContextTest
{
    [Test]
    public void ChatMessagesDoNotAllowHyperlinksByDefault()
    {
        var msg = new ChatMessage(ChatChannel.OOC, "hi", "hi", default, null);
        Assert.That(msg.AllowHyperlinks, Is.False);

        var radio = new ChatMessage(ChatChannel.Radio, "announcement", "announcement", default, null);
        Assert.That(radio.AllowHyperlinks, Is.False);

        Assert.That(new ChatMessage(msg).AllowHyperlinks, Is.False);
    }

    [Test]
    public void RestrictedWhitelistsDoNotIncludeUrlTag()
    {
        Assert.Multiple(() =>
        {
            AssertNoUrl("chat", UserFormattableTags.WithoutUrl);
            AssertNoUrl("paper/news", UserFormattableTags.BaseAllowedTags);
            AssertNoUrl("silicon laws", UserFormattableTags.SiliconAllowedTags);
            AssertNoUrl("ahelp", BwoinkPanel.AllowedTags);
        });
    }

    [Test]
    public void EngineDefaultRichTextTagsDoNotIncludeUrlTag()
    {
        var type = typeof(MarkupTagManager).Assembly.GetType("Robust.Client.UserInterface.RichTextEntry");
        Assert.That(type, Is.Not.Null);

        var field = type!.GetField("DefaultTags", BindingFlags.Public | BindingFlags.Static);
        Assert.That(field, Is.Not.Null);

        var tags = (Type[])field!.GetValue(null)!;
        Assert.That(tags, Does.Not.Contain(typeof(UrlTag)));
        Assert.That(WouldGetUrlHandler(tags), Is.False);
    }

    [Test]
    public void UrlTagDoesNotCreateControlWithoutAllow()
    {
        var tag = new UrlTag();
        Assert.That(tag.TryCreateControl(SafeUrlNode(), out var control), Is.False);
        Assert.That(control, Is.Null);
    }

    [Test]
    public void UrlTagDoesNotCreateControlFromBareUrlValueWithoutHref()
    {
        var tag = new UrlTag();
        var node = new MarkupNode(
            MotdHyperlinkMarkup.TagName,
            new MarkupParameter("https://example.com"),
            null);

        using (UrlTag.Allow())
        {
            Assert.That(tag.TryCreateControl(node, out var control), Is.False);
            Assert.That(control, Is.Null);
            Assert.That(tag.TextBefore(node), Is.EqualTo("https://example.com"));
        }
    }

    [Test]
    public void UrlTagDoesNotCreateControlForJavascriptEvenWithAllow()
    {
        var tag = new UrlTag();
        var node = UrlNode("x", "javascript:alert(1)");

        using (UrlTag.Allow())
        {
            Assert.That(tag.TryCreateControl(node, out var control), Is.False);
            Assert.That(control, Is.Null);
        }
    }

    [Test]
    public void AllowScopeDoesNotLeakAfterDispose()
    {
        using (UrlTag.Allow())
        {
        }

        var tag = new UrlTag();
        Assert.That(tag.TryCreateControl(SafeUrlNode(), out _), Is.False);
    }

    [Test]
    public void NullWhitelistStillNeedsAllowToCreateALink()
    {
        Assert.That(WouldGetUrlHandler(null), Is.True);

        var tag = new UrlTag();
        Assert.That(tag.TryCreateControl(SafeUrlNode(), out _), Is.False);
    }

    private static void AssertNoUrl(string context, Type[] tags)
    {
        Assert.That(tags, Does.Not.Contain(typeof(UrlTag)), $"{context} must not whitelist [url]");
        Assert.That(WouldGetUrlHandler(tags), Is.False, $"{context} would still resolve a url handler");
    }

    private static bool WouldGetUrlHandler(Type[] tagsAllowed)
    {
        return tagsAllowed == null || Array.IndexOf(tagsAllowed, typeof(UrlTag)) != -1;
    }

    private static MarkupNode SafeUrlNode() => UrlNode("https://example.com", "https://example.com");

    private static MarkupNode UrlNode(string text, string href)
    {
        return new MarkupNode(
            MotdHyperlinkMarkup.TagName,
            new MarkupParameter(text),
            new Dictionary<string, MarkupParameter>
            {
                ["href"] = new MarkupParameter(href),
            });
    }
}
