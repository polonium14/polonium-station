using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Server.Codewords;

/// <summary>
/// This is a prototype for specifying codeword generation
/// </summary>
[Prototype]
public sealed partial class CodewordGeneratorPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// List of datasets to use for word generation. All values will be concatenated into one list and then randomly chosen from
    /// </summary>
    // Polonium - zmiana localized na zwykły dla kompatybilności oraz nazwy prototypów całe lowercase. Być może do zmiany jak na forky przejdziemy
    [DataField]
    public List<ProtoId<DatasetPrototype>> Words { get; private set; } =
    [
        "adjectives",
        "verbs",
    ];


    /// <summary>
    /// How many codewords should be generated?
    /// </summary>
    [DataField]
    public int Amount = 3;
}
