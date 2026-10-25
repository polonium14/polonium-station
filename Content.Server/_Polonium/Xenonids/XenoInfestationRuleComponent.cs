using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Xenonids;

/// <summary>
/// Inwazja ksenomorfów z NSV13: 1-2 larwy wychodzą z jednego wentylatora. Ogłoszenie przychodzi dopiero
/// pod koniec zdarzenia (endAnnouncement), żeby ul zdążył się zagnieździć.
/// </summary>
[RegisterComponent, Access(typeof(XenoInfestationRule))]
public sealed partial class XenoInfestationRuleComponent : Component
{
    [DataField]
    public EntProtoId Larva = "CMXenoLarva";

    [DataField]
    public int MinLarvae = 1;

    [DataField]
    public int MaxLarvae = 2;
}
