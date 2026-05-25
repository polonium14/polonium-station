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
}
