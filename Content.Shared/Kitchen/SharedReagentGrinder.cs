// SPDX-FileCopyrightText: 2022 0x6273 <0x40@keemail.me>
// SPDX-FileCopyrightText: 2023 Emisse <99158783+Emisse@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 TemporalOroboros <TemporalOroboros@gmail.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Crotalus <Crotalus@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 ScarKy0 <106310278+ScarKy0@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
// SPDX-FileCopyrightText: 2026 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared.Kitchen;

/// <summary>
/// Sent by the client if they want to toggle the grinder's auto mode.
/// </summary>
[Serializable, NetSerializable]
public sealed class ReagentGrinderToggleAutoModeMessage() : BoundUserInterfaceMessage;

/// <summary>
/// Sent by the client if they want to start the grinder.
/// </summary>
[Serializable, NetSerializable]
public sealed class ReagentGrinderStartMessage(GrinderProgram program) : BoundUserInterfaceMessage
{
    public GrinderProgram Program = program;
}

/// <summary>
/// Sent by the client if they want to eject all grindable entities within the grinder.
/// </summary>
[Serializable, NetSerializable]
public sealed class ReagentGrinderEjectChamberAllMessage() : BoundUserInterfaceMessage;

/// <summary>
/// Sent by the client if they want eject a single grindable entity within the grinder.
/// </summary>
[Serializable, NetSerializable]
public sealed class ReagentGrinderEjectChamberContentMessage(NetEntity entityId) : BoundUserInterfaceMessage
{
    public NetEntity EntityId = entityId;
}

/// <summary>
/// Enum to be used for the grinder's appearance data.
/// </summary>
[Serializable, NetSerializable]
public enum ReagentGrinderVisualState : byte
{
    BeakerAttached
}

/// <summary>
/// The mode the grinder will use when activated. Grinding and juicing the same prototype will yield different results.
/// </summary>
[Serializable, NetSerializable]
public enum GrinderProgram : byte
{
    Grind,
    Juice
}

/// <summary>
/// Key for the ReagentGrinderBoundUserInterface.
/// </summary>
[NetSerializable, Serializable]
public enum ReagentGrinderUiKey : byte
{
    Key
}

/// <summary>
/// The setting of the grinder's auto mode.
/// </summary>
[NetSerializable, Serializable]
public enum GrinderAutoMode : byte
{
    Off,
    Grind,
    Juice
}
