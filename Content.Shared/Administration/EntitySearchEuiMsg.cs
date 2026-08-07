using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

public static class EntitySearchEuiMsg
{
    public static readonly TimeSpan SearchCooldown = TimeSpan.FromSeconds(3);

    [Serializable, NetSerializable]
    public sealed class Search : EuiMessageBase
    {
        public string Query = string.Empty;
    }

    [Serializable, NetSerializable]
    public sealed class NextResultsRequest : EuiMessageBase;

    [Serializable, NetSerializable]
    public sealed class NewResults : EuiMessageBase
    {
        public NewResults((string name, string? proto, NetEntity entity)[] entities, bool replace, bool hasNext, int total)
        {
            Entities = entities;
            Replace = replace;
            HasNext = hasNext;
            Total = total;
        }

        public (string name, string? proto, NetEntity entity)[] Entities { get; }
        public bool Replace { get; }
        public bool HasNext { get; }
        public int Total { get; }
    }
}
