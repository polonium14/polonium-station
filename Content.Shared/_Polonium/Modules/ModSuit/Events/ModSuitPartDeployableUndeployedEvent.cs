namespace Content.Shared._Polonium.Modules.ModSuit.Events;

public sealed partial class ModSuitPartDeployableUndeployedEvent(EntityUid part, EntityUid? wearer, string slot, int partNumber) : BaseModSuitPartDeployableDeployEvent(part, wearer, slot, partNumber);
