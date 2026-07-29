using System.Numerics;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Kaucjomat;

/// <summary>
/// A deposit-return machine. Takes empty drink containers off a user and pays out spesos for them.
/// Anything else it is willing to swallow is refused after the same processing delay.
/// </summary>
[RegisterComponent]
public sealed partial class KaucjomatComponent : Component
{
    /// <summary>
    /// What the machine will even attempt to process. Everything else is rejected on interaction.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Payouts by tag, first match wins. Only applies to containers that are actually empty.
    /// </summary>
    [DataField]
    public List<KaucjomatDeposit> Deposits = new();

    /// <summary>
    /// Solution that has to be empty for a container to count as returnable.
    /// </summary>
    [DataField]
    public string SolutionName = "drink";

    /// <summary>
    /// Currency stack paid out.
    /// </summary>
    [DataField]
    public ProtoId<StackPrototype> Currency = "Credit";

    /// <summary>
    /// How long the machine visibly rattles and grinds after taking a deposit.
    /// </summary>
    [DataField]
    public TimeSpan ShakeDuration = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Quiet think time between the rattling stopping and the verdict landing.
    /// </summary>
    [DataField]
    public TimeSpan PauseDuration = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How far, in tiles, a rejected deposit gets flung.
    /// </summary>
    [DataField]
    public Vector2 RejectDistance = new(3f, 10f);

    /// <summary>
    /// Half-width of the arc a rejected deposit is flung into, in degrees, measured off the
    /// machine-to-depositor axis. It throws the thing back over their shoulder.
    /// </summary>
    [DataField]
    public float RejectSpread = 45f;

    /// <summary>
    /// How far in front of itself, in tiles, the machine puts anything it ejects before throwing
    /// it. The wallmount variant sits on the wall tile, so ejecting on the spot would drop cans
    /// and cash inside the wall.
    /// </summary>
    [DataField]
    public float EjectOffset = 0.6f;

    [DataField]
    public float JitterAmplitude = -10f;

    [DataField]
    public float JitterFrequency = 100f;

    /// <summary>
    /// Chance for an otherwise valid deposit to be rejected anyway.
    /// </summary>
    [DataField]
    public float DenyChance = 0.09f;

    /// <summary>
    /// How long the accept/deny face stays up before the machine goes back to idle.
    /// </summary>
    [DataField]
    public TimeSpan ResultDuration = TimeSpan.FromSeconds(1.2);

    [DataField]
    public SoundSpecifier? SoundStartup = new SoundPathSpecifier("/Audio/Machines/reclaimer_startup.ogg");

    [DataField]
    public SoundSpecifier? SoundAccept = new SoundCollectionSpecifier("CargoPing");

    [DataField]
    public SoundSpecifier? SoundDeny = new SoundCollectionSpecifier("CargoError");

    /// <summary>
    /// Container the deposit sits in while the machine is chewing on it.
    /// </summary>
    [DataField]
    public string ContainerId = "kaucjomat_slot";

    /// <summary>
    /// When the rattling phase ends. Null when the machine isn't rattling.
    /// </summary>
    [ViewVariables]
    public TimeSpan? ShakeEnd;

    /// <summary>
    /// When the verdict lands. Null when the machine isn't processing anything.
    /// </summary>
    [ViewVariables]
    public TimeSpan? VerdictAt;

    /// <summary>
    /// Who fed the machine, for the popup. May be gone by the time the verdict lands.
    /// </summary>
    [ViewVariables]
    public EntityUid? Depositor;

    /// <summary>
    /// World-space direction from the machine towards whoever fed it, recorded at insert time so
    /// the payout and any rejected item still come out the front even if they walk off.
    /// </summary>
    [ViewVariables]
    public Vector2 DepositDirection = Vector2.UnitY;

    /// <summary>
    /// When the current accept/deny display expires. Null when idle.
    /// </summary>
    [ViewVariables]
    public TimeSpan? ResultEnd;

    /// <summary>
    /// Which result face is currently up. Only meaningful while <see cref="ResultEnd"/> is set.
    /// </summary>
    [ViewVariables]
    public KaucjomatVisualState ResultState = KaucjomatVisualState.Normal;

    [ViewVariables]
    public bool Broken;
}

[DataDefinition]
public sealed partial class KaucjomatDeposit
{
    [DataField(required: true)]
    public ProtoId<TagPrototype> Tag;

    [DataField(required: true)]
    public int Payout;
}
