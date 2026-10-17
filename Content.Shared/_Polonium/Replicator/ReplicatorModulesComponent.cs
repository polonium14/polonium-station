using Content.Shared.DoAfter;
using Content.Shared.StatusIcon;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Replicator;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ReplicatorModulesComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<ReplicatorClassPrototype>? Class;

    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<ReplicatorModulePrototype>, int> Stages = new();

    [DataField]
    public int MaxStages = 5;

    /// <summary>
    /// How much more blunt damage than the form's health it takes to break the shell apart.
    /// </summary>
    [DataField]
    public float HullBreakMultiplier = 1.3f;

    /// <summary>
    /// How much more damage of any kind than the form's health it takes to destroy the shell outright.
    /// The inherited silicon destruction thresholds are moved up to this, so they cannot fire before death.
    /// </summary>
    [DataField]
    public float OverkillMultiplier = 2f;

    /// <summary>
    /// Shown to the hive over a replicator that has no class yet.
    /// </summary>
    [DataField]
    public ProtoId<FactionIconPrototype> Icon = "ReplicatorHiveUnclassed";

    [DataField]
    public ProtoId<FactionIconPrototype> QueenIcon = "ReplicatorHiveQueen";
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ReplicatorGunBoostComponent : Component
{
    [DataField, AutoNetworkedField]
    public float FireRateMultiplier = 1f;

    [DataField]
    public int? BaseCapacity;

    [DataField]
    public float? BaseRechargeCooldown;
}

[RegisterComponent]
public sealed partial class ReplicatorEvolutionChamberComponent : Component
{
    [DataField]
    public TimeSpan InstallTime = TimeSpan.FromSeconds(6);

    [DataField]
    public SoundSpecifier? InstallSound;
}

[Serializable, NetSerializable]
public enum ReplicatorEvolutionUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ReplicatorEvolutionState(int budget, int nestLevel) : BoundUserInterfaceState
{
    public readonly int Budget = budget;
    public readonly int NestLevel = nestLevel;
}

[Serializable, NetSerializable]
public sealed class ReplicatorEvolutionGrowMessage(ProtoId<ReplicatorModulePrototype> module) : BoundUserInterfaceMessage
{
    public readonly ProtoId<ReplicatorModulePrototype> Module = module;
}

[Serializable, NetSerializable]
public sealed class ReplicatorEvolutionSpecializeMessage(ProtoId<ReplicatorClassPrototype> @class) : BoundUserInterfaceMessage
{
    public readonly ProtoId<ReplicatorClassPrototype> Class = @class;
}

[Serializable, NetSerializable]
public sealed partial class ReplicatorModuleInstallDoAfterEvent : DoAfterEvent
{
    [DataField]
    public ProtoId<ReplicatorModulePrototype> Module;

    private ReplicatorModuleInstallDoAfterEvent()
    {
    }

    public ReplicatorModuleInstallDoAfterEvent(ProtoId<ReplicatorModulePrototype> module)
    {
        Module = module;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class ReplicatorSpecializeDoAfterEvent : DoAfterEvent
{
    [DataField]
    public ProtoId<ReplicatorClassPrototype> Class;

    private ReplicatorSpecializeDoAfterEvent()
    {
    }

    public ReplicatorSpecializeDoAfterEvent(ProtoId<ReplicatorClassPrototype> @class)
    {
        Class = @class;
    }

    public override DoAfterEvent Clone() => this;
}
