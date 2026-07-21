using Content.Shared.Humanoid;

namespace Content.Shared._Shitmed.Targeting;
public abstract class SharedTargetingSystem : EntitySystem
{
    /// <summary>
    /// Returns all Valid target body parts as an array.
    /// </summary>
    public static TargetBodyPart[] GetValidParts()
    {
        var parts = new[]
        {
            TargetBodyPart.Head,
            TargetBodyPart.Chest,
            TargetBodyPart.LeftArm,
            TargetBodyPart.LeftLeg,
            TargetBodyPart.RightArm,
            TargetBodyPart.RightLeg,
        };

        return parts;
    }

    public static HumanoidVisualLayers ToVisualLayers(TargetBodyPart targetBodyPart)
    {
        switch (targetBodyPart)
        {
            case TargetBodyPart.Head:
                return HumanoidVisualLayers.Head;
            case TargetBodyPart.Chest:
                return HumanoidVisualLayers.Chest;
            case TargetBodyPart.LeftArm:
                return HumanoidVisualLayers.LArm;
            case TargetBodyPart.RightArm:
                return HumanoidVisualLayers.RArm;
            case TargetBodyPart.LeftLeg:
                return HumanoidVisualLayers.LLeg;
            case TargetBodyPart.RightLeg:
                return HumanoidVisualLayers.RLeg;
            default:
                return HumanoidVisualLayers.Chest;
        }
    }
}
