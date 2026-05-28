// SPDX-FileCopyrightText: 2025 Polonium Station Contributors
//
// SPDX-License-Identifier: MIT

using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

public static class EntitySearchEuiMsg
{
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
        public NewResults((string name, NetEntity entity)[] entities, bool replace, bool hasNext)
        {
            Entities = entities;
            Replace = replace;
            HasNext = hasNext;
        }

        public (string name, NetEntity entity)[] Entities { get; }
        public bool Replace { get; }
        public bool HasNext { get; }
    }
}
