namespace Content.Shared._RMC14.Xenonids.Hive;

[ByRefEvent]
public readonly record struct HiveChangedEvent(EntityUid? Hive, EntityUid? OldHive);
