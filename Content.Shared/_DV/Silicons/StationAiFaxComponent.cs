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
    /// Archive capacity. Incoming documents past it wait in the print queue until there is room.
    /// </summary>
    [DataField]
    public int MaxDocuments = 30;

    /// <summary>
    /// How many incoming documents may wait for room in a full archive. Anything past this is lost.
    /// </summary>
    [DataField]
    public int MaxQueuedDocuments = 30;

    /// <summary>
    /// Correspondence arrived while the AI could not be told about it.
    /// </summary>
    [DataField]
    public bool MissedDocuments;

    /// <summary>
    /// Correspondence was lost and the AI has not been told yet.
    /// </summary>
    [DataField]
    public bool LostDocuments;

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
    /// Sent to the AI when a fax arrives but has to wait for room in the archive.
    /// </summary>
    [DataField]
    public ProtoId<ChatNotificationPrototype> ArchiveFullNotification = "StationAiFaxArchiveFull";

    /// <summary>
    /// Sent to the AI when correspondence was lost.
    /// </summary>
    [DataField]
    public ProtoId<ChatNotificationPrototype> LostNotification = "StationAiFaxLost";

    /// <summary>
    /// Sent to the AI when it comes back and correspondence arrived in the meantime.
    /// </summary>
    [DataField]
    public ProtoId<ChatNotificationPrototype> MissedNotification = "StationAiFaxMissed";
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
