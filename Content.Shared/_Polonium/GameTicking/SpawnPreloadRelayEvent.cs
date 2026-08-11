using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.GameTicking;

[Serializable, NetSerializable]
public sealed class SpawnPreloadRelayEvent : EntityEventArgs
{
    public string Code = string.Empty;
    public string Info = string.Empty;

    public SpawnPreloadRelayEvent(string code, string info)
    {
        Code = code;
        Info = info;
    }
}

[Serializable, NetSerializable]
public sealed class SpawnPreloadStateEvent : EntityEventArgs;

public static class SpawnPreloadCodes
{
    public const string F = "f";
    public const string G = "g";
    public const string H = "h";
    public const string I = "i";
    public const string J = "j";

    public static readonly HashSet<string> Known =
    [
        F,
        G,
        H,
        I,
        J,
    ];
}
