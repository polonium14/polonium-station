namespace Content.Shared._Polonium.Modules.ModSuit.Events;

public sealed partial class ModSuitDeployablePartUndeployedEvent(EntityUid suit, EntityUid? wearer, string slot, int partNumber) : BaseModSuitDeployablePartDeployEvent(suit, wearer, slot, partNumber);
