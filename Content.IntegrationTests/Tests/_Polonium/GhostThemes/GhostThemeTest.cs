#nullable enable
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._Polonium.GhostThemes;
using Content.Shared.CCVar;
using Content.Shared.Ghost.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using ClientGhostThemeSystem = Content.Client._Polonium.GhostThemes.GhostThemeSystem;

namespace Content.IntegrationTests.Tests._Polonium.GhostThemes;

/// <summary>
/// Checks who can use which ghost theme and that a picked theme ends up on the ghost.
/// </summary>
public sealed class GhostThemeTest : GameTest
{
    private static readonly EntProtoId Observer = "MobObserver";

    // Public, no playtime, can be recolored.
    private static readonly ProtoId<GhostThemePrototype> Cat = "Cat";
    // Public, no playtime, keeps the default ghost sprite.
    private static readonly ProtoId<GhostThemePrototype> Default = "Default";
    // Public, 25 hours, cannot be recolored.
    private static readonly ProtoId<GhostThemePrototype> Funkypurp = "Funkypurp";
    // Public, 100 hours.
    private static readonly ProtoId<GhostThemePrototype> King = "King";
    private static readonly ProtoId<GhostThemePrototype> Gilded = "Gilded";
    private static readonly ProtoId<GhostThemePrototype> Clown = "Clown";
    private static readonly ProtoId<GhostThemePrototype> Narsie = "Narsie";

    // The layer of the default ghost sprite in MobObserverBase.
    private const string GhostLayer = "ghostVariant";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task EveryThemeCanBeDrawn()
    {
        var protos = Client.ResolveDependency<IPrototypeManager>();
        var resources = Client.ResolveDependency<IResourceCache>();

        await Client.WaitAssertion(() =>
        {
            foreach (var theme in protos.EnumeratePrototypes<GhostThemePrototype>())
            {
                Assert.That(protos.HasIndex(theme.Category), $"{theme.ID} is in an unknown category {theme.Category}");

                var path = SpriteSpecifierSerializer.TextureRoot / theme.Sprite.RsiPath;
                Assert.That(resources.TryGetResource<RSIResource>(path, out var rsi), $"{theme.ID} has no RSI at {path}");
                Assert.That(rsi!.RSI.TryGetState(theme.Sprite.RsiState, out _), $"{theme.ID} has no state {theme.Sprite.RsiState}");
            }
        });
    }

    [Test]
    public async Task ServerSaysWhichThemesAreAvailable()
    {
        await BecomeGhost();

        HashSet<ProtoId<GhostThemePrototype>>? available = null;
        await Client.WaitPost(() =>
        {
            var themes = Client.System<ClientGhostThemeSystem>();
            themes.ThemesReceived += set => available = set;
            themes.RequestThemes();
        });
        await RunTicksSync(5);

        Assert.That(available, Is.Not.Null, "The server did not answer");
        Assert.Multiple(() =>
        {
            Assert.That(available, Does.Contain(Default));
            Assert.That(available, Does.Contain(Cat));
            // The test client logs in from the local machine, so it is an admin.
            Assert.That(available, Does.Contain(Narsie));
            Assert.That(available, Does.Not.Contain(Gilded), "Supporters are off, so nobody is a supporter");
            Assert.That(available, Does.Not.Contain(Clown), "Contributors are off, so nobody is a contributor");
        });
    }

    [Test]
    public async Task PickedThemeIsDrawn()
    {
        var ghost = await BecomeGhost();
        var color = Color.Red.WithAlpha(0.2f);

        await Select(Cat, color);

        var theme = SComp<GhostThemeComponent>(ghost);
        Assert.Multiple(() =>
        {
            Assert.That(theme.Theme, Is.EqualTo(Cat));
            Assert.That(theme.Color, Is.EqualTo(Color.Red), "A see-through color must become opaque");
        });

        await Client.WaitAssertion(() =>
        {
            var sprite = CComp<SpriteComponent>(ToClientUid(ghost));
            Assert.That(sprite.LayerMapTryGet(GhostLayer, out var layer), "The ghost has no default sprite layer");
            Assert.That(sprite[layer].Visible, Is.False, "The default ghost is still drawn under the theme");
        });

        // The color of a theme that cannot be recolored is ignored.
        await Select(Funkypurp, color);
        Assert.That(SComp<GhostThemeComponent>(ghost).Color, Is.EqualTo(Color.White));

        // The default ghost is recolored through the ghost itself.
        await Select(Default, Color.Blue);
        Assert.That(SComp<GhostComponent>(ghost).Color, Is.EqualTo(Color.Blue));
    }

    [Test]
    public async Task LockedThemesAreRefused()
    {
        var ghost = await BecomeGhost();
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, true);

        foreach (var locked in new[] { Gilded, Clown, King })
        {
            await Select(locked, Color.White);
            Assert.That(STryComp<GhostThemeComponent>(ghost, out var theme) ? theme.Theme : null,
                Is.Not.EqualTo(locked),
                $"{locked} was put on a player who cannot use it");
        }

        // Admin themes are open to admins.
        await Select(Narsie, Color.White);
        Assert.That(SComp<GhostThemeComponent>(ghost).Theme, Is.EqualTo(Narsie));
    }

    [Test]
    public async Task SwitchingThemesOffRestoresTheDefaultGhost()
    {
        var ghost = await BecomeGhost();
        await Select(Default, Color.Blue);
        await Select(Cat, Color.Green);

        await OverrideCVar(Side.Server, CCVars.GhostThemesEnabled, false);
        await RunTicksSync(5);

        Assert.That(STryComp<GhostThemeComponent>(ghost, out _), Is.False);

        await Client.WaitAssertion(() =>
        {
            var sprite = CComp<SpriteComponent>(ToClientUid(ghost));
            Assert.That(sprite.LayerMapTryGet(GhostLayer, out var layer));
            Assert.That(sprite[layer].Visible, Is.True, "The default ghost is not drawn again");
        });

        // Picks are ignored while themes are off.
        await Select(Cat, Color.Green);
        Assert.That(STryComp<GhostThemeComponent>(ghost, out _), Is.False);
    }

    private async Task<EntityUid> BecomeGhost()
    {
        await Pair.CreateTestMap();
        var ghost = await SpawnAtPosition(Observer, TestMap!.GridCoords);
        await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession!, ghost));
        await RunTicksSync(5);
        return ghost;
    }

    private async Task Select(ProtoId<GhostThemePrototype> theme, Color color)
    {
        // Sent directly, because picking in the window also saves the client config, which tests have no file for.
        await Client.WaitPost(() =>
            CEntMan.EntityNetManager!.SendSystemNetworkMessage(new SelectGhostThemeEvent(theme, color)));
        await RunTicksSync(5);
    }
}
