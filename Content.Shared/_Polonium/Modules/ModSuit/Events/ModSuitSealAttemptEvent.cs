namespace Content.Shared._Polonium.Modules.ModSuit.Events;

public sealed partial class ModSuitSealAttemptEvent(bool shouldSeal, int sealPartCount) : CancellableEntityEventArgs
{
    public readonly bool ShouldSeal = shouldSeal;
    public readonly int SealPartCount = sealPartCount;
}
