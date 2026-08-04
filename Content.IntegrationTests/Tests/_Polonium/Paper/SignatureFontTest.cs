// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Polonium.Paper;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Paper;

/// <summary>
/// Every font a <see cref="SignatureWriterComponent"/> can select must resolve to a
/// real client-side <see cref="FontPrototype"/>. Guards against a dangling font id
/// after a font is removed (e.g. the copyrighted font dropped on this branch), which
/// would otherwise only blow up at runtime when a player opens the signature UI.
/// </summary>
[TestFixture]
[TestOf(typeof(SignatureWriterComponent))]
public sealed class SignatureFontTest : GameTest
{
    // The special "Default" alias resolves to Noto Sans in the UI, not a FontPrototype.
    private const string DefaultFont = "Default";

    [Test]
    public async Task SignatureFontsResolve()
    {
        var pair = Pair;
        var client = pair.Client;
        var protoMan = client.ResolveDependency<IPrototypeManager>();
        var componentFactory = client.ResolveDependency<IComponentFactory>();

        await client.WaitAssertion(() =>
        {
            var protos = protoMan.EnumeratePrototypes<EntityPrototype>()
                .Where(p => !p.Abstract)
                .Where(p => !pair.IsTestPrototype(p))
                .Where(p => p.TryComp<SignatureWriterComponent>(out _, componentFactory))
                .OrderBy(p => p.ID)
                .ToList();

            Assert.That(protos, Is.Not.Empty, "No SignatureWriter prototypes found - test would be a no-op.");

            Assert.Multiple(() =>
            {
                foreach (var proto in protos)
                {
                    proto.TryComp<SignatureWriterComponent>(out var comp, componentFactory);

                    foreach (var (label, fontId) in comp!.FontList)
                    {
                        if (fontId == DefaultFont)
                            continue;

                        Assert.That(protoMan.HasIndex<FontPrototype>(fontId), Is.True,
                            $"{proto.ID}: fontList entry '{label}' points at missing font prototype '{fontId}'.");
                    }

                    if (comp.Font is { } forced && forced != DefaultFont)
                    {
                        Assert.That(protoMan.HasIndex<FontPrototype>(forced), Is.True,
                            $"{proto.ID}: forced font '{forced}' has no font prototype.");
                    }
                }
            });
        });
    }
}
