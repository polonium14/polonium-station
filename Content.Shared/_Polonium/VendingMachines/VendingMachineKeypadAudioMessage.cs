using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.VendingMachines;

[Serializable, NetSerializable]
public enum VendingMachineKeypadSound : byte
{
    Beep,
    Success,
    Error,
    Timeout
}

[Serializable, NetSerializable]
public sealed class VendingMachineKeypadAudioMessage(VendingMachineKeypadSound soundType, float pitch = 1f)
    : BoundUserInterfaceMessage
{
    public readonly VendingMachineKeypadSound SoundType = soundType;
    public readonly float Pitch = pitch;
}

public static class VendingMachineKeypadSounds
{
    public static (string Path, float Volume) Get(VendingMachineKeypadSound sound) => sound switch
    {
        VendingMachineKeypadSound.Beep => ("/Audio/Machines/Nuke/general_beep.ogg", -4f),
        VendingMachineKeypadSound.Success => ("/Audio/Machines/vending_jingle.ogg", -4f),
        VendingMachineKeypadSound.Error => ("/Audio/Machines/buzz-two.ogg", -4f),
        VendingMachineKeypadSound.Timeout => ("/Audio/Machines/button.ogg", -6f),
        _ => (string.Empty, 0f),
    };
}
