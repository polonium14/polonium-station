namespace Content.Shared._RMC14.Xenonids.Evolution;

[ByRefEvent]
public readonly record struct NewXenoEvolvedEvent(EntityUid OldXeno);

[ByRefEvent]
public readonly record struct XenoDevolvedEvent(EntityUid OldXeno);

[ByRefEvent]
public readonly record struct AfterNewXenoEvolvedEvent;
