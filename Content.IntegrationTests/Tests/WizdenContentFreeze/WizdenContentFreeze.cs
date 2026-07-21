using Content.IntegrationTests.Fixtures;
using Content.Shared.Kitchen;

namespace Content.IntegrationTests.Tests.WizdenContentFreeze;

/// <summary>
/// These tests are limited to adding a specific type of content, essentially freezing it. If you are a fork developer, you may want to disable these tests.
/// </summary>
public sealed class WizdenContentFreeze : GameTest
{
    /// <summary>
    /// This freeze prohibits the addition of new microwave recipes.
    /// The maintainers decided that the mechanics of cooking food in the microwave should be removed,
    /// and all recipes should be ported to other cooking methods.
    /// All added recipes essentially increase the technical debt of future cooking refactoring.
    ///
    /// https://github.com/space-wizards/space-station-14/issues/8524
    /// </summary>
    [Test]
    public async Task MicrowaveRecipesFreezeTest()
    {
        var pair = Pair;
        var server = pair.Server;

        var protoMan = server.ProtoMan;

        var recipesCount = protoMan.Count<FoodRecipePrototype>();
        var recipesLimit = 227; // Polonium +3, Funky +6

        if (recipesCount > recipesLimit)
        {
            Assert.Fail($"PROSIMY O ZAPRZESTANIE DODAWANIA NOWYCH PRZEPISÓW NA POTRAWY Z MIKROFALI. ONE SĄ NIEAKTUALNE I NALEŻY JE ZASTĄPIĆ PRZEPISAMI OPARTYMI NA WŁAŚCIWYCH ZASADACH GOTOWANIA! Zobacz https://github.com/space-wizards/space-station-14/issues/8524. Nie przekraczaj limitu {recipesLimit}. Aktualna liczba: {recipesCount}"); // please forgive me
        }

        if (recipesCount < recipesLimit)
        {
            Assert.Fail($"Oh, you deleted the microwave recipes? YOU ARE SO COOL! Please lower the number of recipes in MicrowaveRecipesFreezeTest from {recipesLimit} to {recipesCount} so that future contributors cannot add new recipes back.");
        }
    }
}
