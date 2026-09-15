using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Tutorial;

[Serializable, NetSerializable]
public sealed class TutorialRestartRequestedEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class TutorialStartPracticalEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class TutorialGuidebookOpenedEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class TutorialCraftingMenuOpenedEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class TutorialConstructionGhostStateEvent : EntityEventArgs
{
    public string AnchorId { get; }
    public bool OnTile { get; }
    public bool Stray { get; }

    public TutorialConstructionGhostStateEvent(string anchorId, bool onTile, bool stray)
    {
        AnchorId = anchorId;
        OnTile = onTile;
        Stray = stray;
    }
}

[Serializable, NetSerializable]
public sealed class TutorialCameraRotatedEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class TutorialRedialEvent : EntityEventArgs
{
    public string Address { get; }
    public string Message { get; }

    public TutorialRedialEvent(string address, string message)
    {
        Address = address;
        Message = message;
    }
}

[Serializable, NetSerializable]
public sealed class TutorialFinaleChoiceEvent : EntityEventArgs
{
    public bool JoinServer { get; }

    public TutorialFinaleChoiceEvent(bool joinServer)
    {
        JoinServer = joinServer;
    }
}

[Serializable, NetSerializable]
public sealed class TutorialCompletionStatusEvent : EntityEventArgs
{
    public bool Completed { get; }

    public TutorialCompletionStatusEvent(bool completed)
    {
        Completed = completed;
    }
}
