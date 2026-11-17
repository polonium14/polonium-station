#nullable enable
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Polonium.DiscordLinking;
using Content.Server.Database;
using Content.Shared._Polonium.DiscordLinking;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Robust.Shared.Localization;
using Robust.Shared.Network;
using ClientDiscordLinkSystem = Content.Client._Polonium.DiscordLinking.DiscordLinkSystem;

namespace Content.IntegrationTests.Tests._Polonium.DiscordLinking;

/// <summary>
/// Checks how the server talks to the Discord bot, with a fake bot in place of the HTTP client.
/// </summary>
public sealed class DiscordLinkTest : GameTest
{
    private const string ApiUrl = "http://bot.test/";
    private const string ApiToken = "test-link-token";
    private const string DiscordId = "123456789012345678";
    private const string LinkPage = "https://discord.com/oauth2/authorize?state=test";

    private FakeBot _bot = default!;
    private DiscordLinkManager _links = default!;

    [SetUp]
    public async Task SetUpBot()
    {
        _bot = new FakeBot();
        _links = Server.ResolveDependency<DiscordLinkManager>();
        await Server.WaitPost(() => _links.HttpOverride = new HttpMessageInvoker(_bot));
    }

    [TearDown]
    public async Task RemoveBot()
    {
        await Server.WaitPost(() => _links.HttpOverride = null);
    }

    [Test]
    public async Task GuestsCannotLink()
    {
        await EnableLinking();
        var client = Client.System<ClientDiscordLinkSystem>();
        var states = new List<DiscordLinkStatus>();
        Action<DiscordLinkStateEvent> onState = state => states.Add(state.Status);

        // Test clients connect as guests, real players log in through the launcher.
        Assert.That(ServerSession!.Channel.AuthType, Is.EqualTo(LoginType.GuestAssigned));

        await Client.WaitPost(() => client.StateChanged += onState);
        try
        {
            await Client.WaitPost(() => client.RequestStart());
            await Pair.RunTicksSync(10);
            await Client.WaitPost(() => client.RequestStatus());
            await Pair.RunTicksSync(10);
        }
        finally
        {
            await Client.WaitPost(() => client.StateChanged -= onState);
        }

        Assert.Multiple(() =>
        {
            Assert.That(states, Is.EqualTo(new[] { DiscordLinkStatus.NotLoggedIn, DiscordLinkStatus.NotLoggedIn }));
            Assert.That(_bot.Requests, Is.Empty, "A guest got through to the bot");
        });
    }

    [Test]
    public async Task StartingGivesTheDiscordPage()
    {
        await EnableLinking();
        var user = NewUser();
        _bot.Respond = _ => Json(HttpStatusCode.OK, new { url = LinkPage, expiresInSeconds = 600 });

        var state = await OnServer(() => _links.HandleRequest(user, "Player", LoginType.LoggedIn, true));

        Assert.Multiple(() =>
        {
            Assert.That(state.Status, Is.EqualTo(DiscordLinkStatus.Started));
            Assert.That(state.Url, Is.EqualTo(LinkPage));
            Assert.That(_bot.Requests, Has.Count.EqualTo(1));
        });

        var request = _bot.Requests.Single();
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Multiple(() =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(request.Path, Is.EqualTo("/link/start"));
            Assert.That(request.Authorization, Is.EqualTo($"Bearer {ApiToken}"));
            Assert.That(body.RootElement.GetProperty("uuid").GetString(), Is.EqualTo(user.UserId.ToString().ToLowerInvariant()));
            Assert.That(body.RootElement.GetProperty("name").GetString(), Is.EqualTo("Player"));
        });
    }

    [Test]
    [TestCase("http://discord.com/oauth2/authorize?state=test")]
    [TestCase("https://discord.com.evil.test/oauth2/authorize")]
    [TestCase("https://evil.test/?https://discord.com/")]
    [TestCase("not a link")]
    public async Task PagesOffDiscordAreIgnored(string page)
    {
        await EnableLinking();
        _bot.Respond = _ => Json(HttpStatusCode.OK, new { url = page, expiresInSeconds = 600 });

        var state = await OnServer(() => _links.HandleRequest(NewUser(), "Player", LoginType.LoggedIn, true));

        Assert.Multiple(() =>
        {
            Assert.That(state.Status, Is.EqualTo(DiscordLinkStatus.Unavailable));
            Assert.That(state.Url, Is.Null);
        });
    }

    [Test]
    public async Task StartingWhenAlreadyLinked()
    {
        await EnableLinking();
        var user = NewUser();
        var changes = new List<DiscordLinkInfo>();
        Action<NetUserId, DiscordLinkInfo> onChange = (changed, info) =>
        {
            if (changed == user)
                changes.Add(info);
        };
        _bot.Respond = _ => Json(HttpStatusCode.Conflict, new { error = "already_linked", discordId = DiscordId, discordName = "@nick" });

        await Server.WaitPost(() => _links.LinkChanged += onChange);
        try
        {
            var state = await OnServer(() => _links.HandleRequest(user, "Player", LoginType.LoggedIn, true));

            Assert.Multiple(() =>
            {
                Assert.That(state.Status, Is.EqualTo(DiscordLinkStatus.Linked));
                Assert.That(state.DiscordName, Is.EqualTo("@nick"));
                Assert.That(state.Url, Is.Null);
            });
        }
        finally
        {
            await Server.WaitPost(() => _links.LinkChanged -= onChange);
        }

        await Server.WaitAssertion(() =>
        {
            Assert.That(_links.IsLinked(user), Is.True);
            Assert.That(_links.TryGetLink(user, out var info), Is.True);
            Assert.That(info!.DiscordId, Is.EqualTo(DiscordId));
            Assert.That(changes, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task CooldownAnswersFromWhatIsKnown()
    {
        await EnableLinking();
        await OverrideCVar(Side.Server, CCVars.DiscordLinkCooldown, 60f);
        var user = NewUser();
        _bot.Respond = request => request.RequestUri!.AbsolutePath == "/link/start"
            ? Json(HttpStatusCode.OK, new { url = LinkPage, expiresInSeconds = 600 })
            : Json(HttpStatusCode.OK, new { linked = false, relatedUuids = Array.Empty<string>() });

        var status = await OnServer(() => _links.HandleRequest(user, "Player", LoginType.LoggedIn, false));
        var statusAgain = await OnServer(() => _links.HandleRequest(user, "Player", LoginType.LoggedIn, false));
        // The window asks about the link when it opens, which must not hold back the link button.
        var start = await OnServer(() => _links.HandleRequest(user, "Player", LoginType.LoggedIn, true));
        var startAgain = await OnServer(() => _links.HandleRequest(user, "Player", LoginType.LoggedIn, true));

        Assert.Multiple(() =>
        {
            Assert.That(status.Status, Is.EqualTo(DiscordLinkStatus.NotLinked));
            Assert.That(statusAgain.Status, Is.EqualTo(DiscordLinkStatus.NotLinked));
            Assert.That(start.Status, Is.EqualTo(DiscordLinkStatus.Started));
            Assert.That(startAgain.Status, Is.EqualTo(DiscordLinkStatus.Cooldown));
            Assert.That(_bot.Requests.Select(request => request.Path), Is.EqualTo(new[] { "/link/status", "/link/start" }),
                "Asked the bot again during the cooldown");
        });

        // Another player has their own cooldown, and a player who left starts over.
        var other = await OnServer(() => _links.HandleRequest(NewUser(), "Other", LoginType.LoggedIn, true));
        await Server.WaitPost(() => _links.ForgetPlayer(user));
        var afterLeaving = await OnServer(() => _links.HandleRequest(user, "Player", LoginType.LoggedIn, true));

        Assert.Multiple(() =>
        {
            Assert.That(other.Status, Is.EqualTo(DiscordLinkStatus.Started));
            Assert.That(afterLeaving.Status, Is.EqualTo(DiscordLinkStatus.Started));
        });
    }

    [Test]
    public async Task BotErrorsMakeLinkingUnavailable()
    {
        await EnableLinking();

        _bot.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var serverError = await OnServer(() => _links.HandleRequest(NewUser(), "Player", LoginType.LoggedIn, true));

        _bot.Respond = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized);
        var unauthorized = await OnServer(() => _links.HandleRequest(NewUser(), "Player", LoginType.LoggedIn, false));

        _bot.Respond = _ => throw new HttpRequestException("The bot is down");
        var down = await OnServer(() => _links.HandleRequest(NewUser(), "Player", LoginType.LoggedIn, true));

        _bot.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{not json", Encoding.UTF8, "application/json"),
        };
        var broken = await OnServer(() => _links.HandleRequest(NewUser(), "Player", LoginType.LoggedIn, false));

        Assert.Multiple(() =>
        {
            Assert.That(serverError.Status, Is.EqualTo(DiscordLinkStatus.Unavailable));
            Assert.That(unauthorized.Status, Is.EqualTo(DiscordLinkStatus.Unavailable));
            Assert.That(down.Status, Is.EqualTo(DiscordLinkStatus.Unavailable));
            Assert.That(broken.Status, Is.EqualTo(DiscordLinkStatus.Unavailable));
        });
    }

    [Test]
    public async Task NothingIsSentWhenSwitchedOffOrNotSetUp()
    {
        var user = NewUser();
        _bot.Respond = _ => Json(HttpStatusCode.OK, new { url = LinkPage, expiresInSeconds = 600 });

        var off = await OnServer(() => _links.HandleRequest(user, "Player", LoginType.LoggedIn, true));
        var joinOff = await OnServer(() => _links.GetJoinState(user, LoginType.LoggedIn));
        var connectOff = await OnServer(() => _links.CheckConnection(user, "Player", LoginType.LoggedIn));

        await OverrideCVar(Side.Server, CCVars.DiscordLinkEnabled, true);
        await OverrideCVar(Side.Server, CCVars.DiscordLinkApiToken, ApiToken);
        var noUrl = await OnServer(() => _links.HandleRequest(user, "Player", LoginType.LoggedIn, true));

        Assert.Multiple(() =>
        {
            Assert.That(off.Status, Is.EqualTo(DiscordLinkStatus.Unavailable));
            Assert.That(joinOff, Is.Null);
            Assert.That(connectOff, Is.Null);
            Assert.That(noUrl.Status, Is.EqualTo(DiscordLinkStatus.Unavailable));
            Assert.That(_bot.Requests, Is.Empty);
        });
    }

    [Test]
    public async Task StatusFillsThePublicApi()
    {
        await EnableLinking();
        var user = NewUser();
        var related = NewUser();
        var changes = 0;
        Action<NetUserId, DiscordLinkInfo> onChange = (changed, _) =>
        {
            if (changed == user)
                changes++;
        };
        _bot.Respond = _ => Json(HttpStatusCode.OK, new
        {
            linked = true,
            discordId = DiscordId,
            discordName = "@nick",
            relatedUuids = new[] { related.UserId.ToString().ToUpperInvariant(), "not-a-uuid" },
        });

        DiscordLinkInfo? info;
        DiscordLinkStateEvent? joinState;
        await Server.WaitPost(() => _links.LinkChanged += onChange);
        try
        {
            info = await OnServer(() => _links.FetchLink(user));
            await OnServer(() => _links.FetchLink(user));
            joinState = await OnServer(() => _links.GetJoinState(user, LoginType.LoggedIn));
        }
        finally
        {
            await Server.WaitPost(() => _links.LinkChanged -= onChange);
        }

        var request = _bot.Requests.First();
        Assert.Multiple(() =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(request.Path, Is.EqualTo("/link/status"));
            Assert.That(request.Query, Is.EqualTo($"?uuid={user.UserId}"));
            Assert.That(request.Authorization, Is.EqualTo($"Bearer {ApiToken}"));

            Assert.That(info, Is.Not.Null);
            Assert.That(info!.Linked, Is.True);
            Assert.That(info.DiscordId, Is.EqualTo(DiscordId));
            Assert.That(info.DiscordName, Is.EqualTo("@nick"));
            Assert.That(info.RelatedUsers, Is.EqualTo(new[] { related }), "Kept an invalid UUID");

            Assert.That(changes, Is.EqualTo(1), "Raised LinkChanged when nothing changed");
            Assert.That(_bot.Requests, Has.Count.EqualTo(2), "The join status did not reuse the fresh answer");
            Assert.That(joinState?.Status, Is.EqualTo(DiscordLinkStatus.Linked));
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(_links.IsLinked(user), Is.True);
            Assert.That(_links.GetRelatedUsers(user), Is.EqualTo(new[] { related }));
            Assert.That(_links.IsLinked(related), Is.False, "Knew about an account nobody asked about");

            _links.ForgetPlayer(user);
            Assert.That(_links.TryGetLink(user, out _), Is.False);
            Assert.That(_links.GetRelatedUsers(user), Is.Empty);
        });
    }

    [Test]
    public async Task BadDiscordDataIsDropped()
    {
        await EnableLinking();
        var user = NewUser();
        _bot.Respond = _ => Json(HttpStatusCode.OK, new
        {
            linked = true,
            discordId = "123; DROP TABLE",
            discordName = new string('x', 65),
            relatedUuids = Array.Empty<string>(),
        });

        var info = await OnServer(() => _links.FetchLink(user));

        Assert.Multiple(() =>
        {
            Assert.That(info?.Linked, Is.True);
            Assert.That(info?.DiscordId, Is.Null);
            Assert.That(info?.DiscordName, Is.Null);
        });
    }

    [Test]
    public async Task ConnectionCheckOnlyWhenRequired()
    {
        await EnableLinking();
        _bot.Respond = _ => Json(HttpStatusCode.OK, new { linked = false, relatedUuids = Array.Empty<string>() });

        var notRequired = await OnServer(() => _links.CheckConnection(NewUser(), "Player", LoginType.LoggedIn));

        await OverrideCVar(Side.Server, CCVars.DiscordLinkRequired, true);
        var guest = await OnServer(() => _links.CheckConnection(NewUser(), "Guest", LoginType.GuestAssigned));

        Assert.Multiple(() =>
        {
            Assert.That(notRequired, Is.Null);
            Assert.That(guest?.Message, Is.EqualTo(GetString("discord-link-deny-not-logged-in")));
            Assert.That(_bot.Requests, Is.Empty);
        });
    }

    [Test]
    public async Task ConnectionCheckRefusesUntilLinked()
    {
        await EnableLinking();
        await OverrideCVar(Side.Server, CCVars.DiscordLinkRequired, true);
        var user = NewUser();
        var linked = false;
        _bot.Respond = request => request.RequestUri!.AbsolutePath == "/link/start"
            ? Json(HttpStatusCode.OK, new { url = LinkPage, expiresInSeconds = 600 })
            : Json(HttpStatusCode.OK, linked
                ? new { linked = true, discordId = DiscordId, discordName = "@nick", relatedUuids = Array.Empty<string>() }
                : new { linked = false, discordId = (string) null!, discordName = (string) null!, relatedUuids = Array.Empty<string>() });

        var refused = await OnServer(() => _links.CheckConnection(user, "Player", LoginType.LoggedIn));
        string? url = null;
        string? urlAgain = null;
        await Server.WaitPost(() =>
        {
            url = _links.TakeDenyUrl(user);
            urlAgain = _links.TakeDenyUrl(user);
        });

        linked = true;
        var allowed = await OnServer(() => _links.CheckConnection(user, "Player", LoginType.LoggedIn));

        Assert.Multiple(() =>
        {
            Assert.That(refused?.Message, Is.EqualTo(GetString("discord-link-deny-not-linked")));
            Assert.That(url, Is.EqualTo(LinkPage));
            Assert.That(urlAgain, Is.Null, "The linking page was given out twice");
            Assert.That(allowed, Is.Null);
            Assert.That(_bot.Requests.Select(request => request.Path),
                Is.EqualTo(new[] { "/link/status", "/link/start", "/link/status" }));
        });
    }

    [Test]
    public async Task ConnectionCheckRefusesAltsOfBannedPlayers()
    {
        await EnableLinking();
        await OverrideCVar(Side.Server, CCVars.DiscordLinkRequired, true);
        var db = Server.ResolveDependency<IServerDbManager>();
        var user = NewUser();
        var banned = NewUser();
        var clean = NewUser();

        await db.AddBanAsync(new BanDef(
            null,
            BanType.Server,
            ImmutableArray.Create(banned),
            ImmutableArray<(IPAddress, int)>.Empty,
            ImmutableArray<ImmutableTypedHwid>.Empty,
            DateTimeOffset.Now,
            null,
            ImmutableArray<int>.Empty,
            TimeSpan.Zero,
            "Discord link test",
            NoteSeverity.High,
            null,
            null));

        _bot.Respond = _ => Json(HttpStatusCode.OK, new
        {
            linked = true,
            discordId = DiscordId,
            relatedUuids = new[] { clean.UserId.ToString(), banned.UserId.ToString() },
        });

        var denial = await OnServer(() => _links.CheckConnection(user, "Alt", LoginType.LoggedIn));

        Assert.Multiple(() =>
        {
            Assert.That(denial?.Message, Is.EqualTo(GetString("discord-link-deny-related-ban")));
            Assert.That(_bot.Requests.Select(request => request.Path), Is.EqualTo(new[] { "/link/status" }));
        });

        // An account that only tried to link the Discord of players who are not banned gets in.
        _bot.Respond = _ => Json(HttpStatusCode.OK, new
        {
            linked = true,
            discordId = DiscordId,
            relatedUuids = new[] { clean.UserId.ToString() },
        });

        Assert.That(await OnServer(() => _links.CheckConnection(NewUser(), "Player", LoginType.LoggedIn)), Is.Null);
    }

    [Test]
    public async Task ConnectionCheckWhenTheBotIsDown()
    {
        await EnableLinking();
        await OverrideCVar(Side.Server, CCVars.DiscordLinkRequired, true);
        _bot.Respond = _ => throw new HttpRequestException("The bot is down");

        var allowed = await OnServer(() => _links.CheckConnection(NewUser(), "Player", LoginType.LoggedIn));

        await OverrideCVar(Side.Server, CCVars.DiscordLinkRequiredAllowOnError, false);
        var refused = await OnServer(() => _links.CheckConnection(NewUser(), "Player", LoginType.LoggedIn));

        Assert.Multiple(() =>
        {
            Assert.That(allowed, Is.Null);
            Assert.That(refused?.Message, Is.EqualTo(GetString("discord-link-deny-unavailable")));
        });
    }

    private async Task EnableLinking()
    {
        await OverrideCVar(Side.Server, CCVars.DiscordLinkEnabled, true);
        await OverrideCVar(Side.Server, CCVars.DiscordLinkApiUrl, ApiUrl);
        await OverrideCVar(Side.Server, CCVars.DiscordLinkApiToken, ApiToken);
    }

    /// <summary>
    /// Runs a call of the manager on the server thread, where its awaits come back, and ticks until it is done.
    /// </summary>
    private async Task<T> OnServer<T>(Func<Task<T>> call)
    {
        Task<T>? task = null;
        await Server.WaitPost(() => task = call());

        for (var i = 0; i < 100 && !task!.IsCompleted; i++)
        {
            await Pair.RunTicksSync(1);
        }

        Assert.That(task!.IsCompleted, Is.True, "The Discord link manager did not finish in time");
        return await task;
    }

    private string GetString(string key)
    {
        return Server.ResolveDependency<ILocalizationManager>().GetString(key);
    }

    private static NetUserId NewUser()
    {
        return new NetUserId(Guid.NewGuid());
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
    }

    private sealed record BotRequest(HttpMethod Method, string Path, string Query, string? Authorization, string? Body);

    private sealed class FakeBot : HttpMessageHandler
    {
        public readonly List<BotRequest> Requests = new();

        public Func<HttpRequestMessage, HttpResponseMessage> Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancel);
            Requests.Add(new BotRequest(
                request.Method,
                request.RequestUri!.AbsolutePath,
                request.RequestUri.Query,
                request.Headers.Authorization?.ToString(),
                body));

            return Respond(request);
        }
    }
}
