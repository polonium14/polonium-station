using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Disposal;

/// <summary>
/// Sent by the client when the player clicks the mouth of a disposal or mailing unit.
/// The server looks at the player's hand: a held item goes in, an empty hand takes out what went in last.
/// </summary>
[Serializable, NetSerializable]
public sealed class DisposalDeviceMouthMessage : BoundUserInterfaceMessage;
