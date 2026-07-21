namespace Content.Shared._Shitmed.Medical.Surgery;

public sealed class SurgerySanitizationEvent : HandledEntityEventArgs;

public sealed class SurgeryPainEvent : CancellableEntityEventArgs;

public sealed class SurgeryIgnorePreviousStepsEvent : HandledEntityEventArgs;
