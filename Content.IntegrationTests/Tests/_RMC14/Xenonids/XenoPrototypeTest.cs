#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._RMC14.Xenonids;

[TestOf(typeof(XenoComponent))]
public sealed class XenoPrototypeTest : GameTest
{
    private static readonly string[] Castes =
    [
        "CMXenoLarva",
        "CMXenoDrone",
        "CMXenoRunner",
        "CMXenoSentinel",
        "CMXenoDefender",
        "CMXenoHivelord",
        "CMXenoLurker",
        "CMXenoSpitter",
        "CMXenoWarrior",
        "CMXenoPraetorian",
        "CMXenoRavager",
        "CMXenoCrusher",
        "CMXenoQueen",
    ];

    [Test]
    public async Task AllXenoCastesHaveXenoComponent()
    {
        await Pair.Server.WaitPost(() =>
        {
            foreach (var caste in Castes)
            {
                var ent = SSpawn(caste);
                Assert.That(
                    SEntMan.HasComponent<XenoComponent>(ent),
                    Is.True,
                    $"{caste} should spawn with XenoComponent");
            }
        });
    }
}
