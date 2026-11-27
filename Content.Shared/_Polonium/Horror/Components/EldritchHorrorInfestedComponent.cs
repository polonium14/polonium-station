using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Horror.Components;

/// <summary>
/// A body with an eldritch horror in its head.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EldritchHorrorInfestedComponent : Component
{
    public const string HorrorContainerId = "polonium_horror";
    public const string CaptiveMindContainerId = "polonium_horror_captive_mind";

    /// <summary>
    /// The horror inside.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Horror;

    [ViewVariables]
    public ContainerSlot HorrorContainer = default!;

    /// <summary>
    /// Holds the trapped mind of the host while the horror controls the body.
    /// </summary>
    [ViewVariables]
    public ContainerSlot CaptiveMindContainer = default!;

    /// <summary>
    /// Mind of the host, set while it is trapped.
    /// </summary>
    [DataField]
    public EntityUid? HostMind;

    /// <summary>
    /// The host took damage this tick; checked once the damage is applied.
    /// </summary>
    [ViewVariables]
    public bool HitPending;

    /// <summary>
    /// The host thinks at the horror: their speech goes to it instead of out loud.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool TalkToHorror;

    /// <summary>
    /// Last things the host said, for the horror reading their memories.
    /// </summary>
    [ViewVariables]
    public List<string> RecentSpeech = new();

    /// <summary>
    /// How many of the host's last lines are remembered.
    /// </summary>
    [DataField]
    public int RecentSpeechLimit = 10;

    /// <summary>
    /// The host's action to think at the horror.
    /// </summary>
    [DataField]
    public EntityUid? TalkToHorrorAction;
}

/// <summary>
/// A mind whose soul an eldritch horror devoured. It can't be devoured again.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HorrorSoullessComponent : Component;

/// <summary>
/// The tentacle a horror grows out of its host's arm.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HorrorTentacleComponent : Component
{
    /// <summary>
    /// How long prying an unbolted airlock open takes.
    /// </summary>
    [DataField]
    public TimeSpan PryTime = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long a grabbed or thrown victim stays down.
    /// </summary>
    [DataField]
    public TimeSpan GrabKnockdown = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Speed of a victim thrown with the tentacle.
    /// </summary>
    [DataField]
    public float ThrowSpeed = 8f;

    /// <summary>
    /// How far the tentacle reaches for grabs and airlocks, the same as its melee range.
    /// </summary>
    [DataField]
    public float Reach = 2f;
}

/// <summary>
/// Sprite view of the soul locator alert, the client turns its arrow towards the hunted soul.
/// </summary>
[RegisterComponent]
public sealed partial class HorrorSoulLocatorAlertComponent : Component;

/// <summary>
/// The trapped mind of a host, sitting in a dummy entity inside the body while a horror controls it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HorrorCaptiveMindComponent : Component
{
    /// <summary>
    /// The body the mind belongs to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Host;

    /// <summary>
    /// When the running resist attempt hands the body back, null if the mind isn't resisting.
    /// </summary>
    [DataField]
    public TimeSpan? ResistEnd;

    /// <summary>
    /// Random time a resist attempt takes.
    /// </summary>
    [DataField]
    public TimeSpan ResistMin = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan ResistMax = TimeSpan.FromSeconds(30);
}
