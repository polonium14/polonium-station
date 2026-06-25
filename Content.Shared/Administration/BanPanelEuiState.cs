// SPDX-FileCopyrightText: 2023 Chief-Engineer <119664036+Chief-Engineer@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Riggle <27156122+RigglePrime@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2025 Errant <35878406+Errant-4@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 beck-thompson <beck314159@hotmail.com>
// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
// SPDX-FileCopyrightText: 2026 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;
using Content.Shared.Database;
using Content.Shared.Eui;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

[Serializable, NetSerializable]
public sealed class BanPanelEuiState : EuiStateBase
{
    public string PlayerName { get; set; }
    public bool HasBan { get; set; }
    public int RoundId { get; }

    public BanPanelEuiState(string playerName, bool hasBan, int roundId)
    {
        PlayerName = playerName;
        HasBan = hasBan;
        RoundId = roundId;
    }
}

public static class BanPanelEuiStateMsg
{
    [Serializable, NetSerializable]
    public sealed class CreateBanRequest(Ban ban) : EuiMessageBase
    {
        public Ban Ban { get; } = ban;
    }

    [Serializable, NetSerializable]
    public sealed class GetPlayerInfoRequest : EuiMessageBase
    {
        public string PlayerUsername { get; set; }

        public GetPlayerInfoRequest(string username)
        {
            PlayerUsername = username;
        }
    }
}

/// <summary>
///     Contains all the data related to a particular ban action created by the BanPanel window.
/// </summary>
[Serializable, NetSerializable]
public sealed record Ban
{
    public Ban(
        string? target,
        (IPAddress, int)? ipAddressTuple,
        bool useLastIp,
        ImmutableTypedHwid? hwid,
        bool useLastHwid,
        uint banDurationMinutes,
        string reason,
        NoteSeverity severity,
        ProtoId<JobPrototype>[]? bannedJobs,
        ProtoId<AntagPrototype>[]? bannedAntags,
        bool erase,
        int situationRound = 0)
    {
        Target = target;
        IpAddress = ipAddressTuple?.Item1.ToString();
        IpAddressHid = ipAddressTuple?.Item2.ToString() ?? "0";
        UseLastIp = useLastIp;
        Hwid = hwid;
        UseLastHwid = useLastHwid;
        BanDurationMinutes = banDurationMinutes;
        Reason = reason;
        Severity = severity;
        BannedJobs = bannedJobs;
        BannedAntags = bannedAntags;
        Erase = erase;
        SituationRound = situationRound;
    }

    public readonly string? Target;
    public readonly string? IpAddress;
    public readonly string? IpAddressHid;
    public readonly bool UseLastIp;
    public readonly ImmutableTypedHwid? Hwid;
    public readonly bool UseLastHwid;
    public readonly uint BanDurationMinutes;
    public readonly string Reason;
    public readonly NoteSeverity Severity;
    public readonly ProtoId<JobPrototype>[]? BannedJobs;
    public readonly ProtoId<AntagPrototype>[]? BannedAntags;
    public readonly bool Erase;
    public readonly int SituationRound;
}
