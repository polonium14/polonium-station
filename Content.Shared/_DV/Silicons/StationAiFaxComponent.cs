using Content.Shared.Chat.Prototypes;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DV.Silicons;

/// <summary>
/// A station AI's internal fax machine. Everything it prints lands in its document archive.
/// </summary>
[RegisterComponent]
public sealed partial class StationAiFaxComponent : Component
{
    public const string Container = "PaperLibrary";

    /// <summary>
    /// Archive capacity. Documents printed past it are destroyed.
    /// </summary>
    [DataField]
    public int MaxDocuments = 30;

    /// <summary>
    /// Lathe recipe packs the AI can use as document templates.
    /// </summary>
    [DataField]
    public List<ProtoId<LatheRecipePackPrototype>> TemplatePacks = new();

    /// <summary>
    /// Sent to the AI when a fax arrives.
    /// </summary>
    [DataField]
    public ProtoId<ChatNotificationPrototype> ReceivedNotification = "StationAiFaxReceived";

    /// <summary>
    /// Sent to the AI when a document is destroyed because the archive is full.
    /// </summary>
    [DataField]
    public ProtoId<ChatNotificationPrototype> ArchiveFullNotification = "StationAiFaxArchiveFull";
}

[Serializable, NetSerializable]
public abstract class StationAiFaxDocumentMessage(NetEntity document) : BoundUserInterfaceMessage
{
    public readonly NetEntity Document = document;
}

[Serializable, NetSerializable]
public sealed class StationAiFaxOpenMessage(NetEntity document) : StationAiFaxDocumentMessage(document);

[Serializable, NetSerializable]
public sealed class StationAiFaxCopyMessage(NetEntity document) : StationAiFaxDocumentMessage(document);

[Serializable, NetSerializable]
public sealed class StationAiFaxSendMessage(NetEntity document) : StationAiFaxDocumentMessage(document);

[Serializable, NetSerializable]
public sealed class StationAiFaxShredMessage(NetEntity document) : StationAiFaxDocumentMessage(document);

[Serializable, NetSerializable]
public sealed class StationAiFaxTemplateMessage(ProtoId<LatheRecipePrototype> recipe) : BoundUserInterfaceMessage
{
    public readonly ProtoId<LatheRecipePrototype> Recipe = recipe;
}
