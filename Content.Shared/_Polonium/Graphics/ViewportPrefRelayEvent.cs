using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Graphics;

[Serializable, NetSerializable]
public sealed class ViewportPrefRelayEvent : EntityEventArgs
{
    public string Code = string.Empty;

    public ViewportPrefRelayEvent(string code)
    {
        Code = code;
    }
}

public static class ViewportPrefCodes
{
    public const string A = "a";
    public const string B = "b";
    public const string C = "c";
    public const string D = "d";
    public const string E = "e";

    public static readonly HashSet<string> Known =
    [
        A,
        B,
        C,
        D,
        E,
    ];
}
