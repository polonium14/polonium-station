using Content.Shared._Polonium.Tutorial.Actions;
using Content.Shared._Polonium.Tutorial.Conditions;
using Content.Shared._Polonium.Tutorial.Watchers;
using Content.Shared.Guidebook;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Prototypes;

/// <summary>Parts of the hud the interface briefing can point at.</summary>
public enum TutorialHudTarget : byte
{
    None,
    Hands,
    Alerts,
    Inventory,
    Chat,
    Actions,
    TopBar,

    /// <summary>Menu buttons and the action slots below them as one region.</summary>
    ActionsAndMenu,

    /// <summary>The body part doll in the bottom right corner.</summary>
    Targeting,

    /// <summary>Just the guidebook button in the menu bar, not the whole bar.</summary>
    Guidebook,
}

[Prototype]
public sealed partial class TutorialStepPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Instruction = string.Empty;

    [DataField]
    public string? NavigationAnchor;

    [DataField]
    public TutorialCondition? Completion;

    [DataField]
    public List<TutorialAction> OnEnter = new();

    [DataField]
    public List<TutorialAction> OnComplete = new();

    [DataField]
    public ProtoId<GuideEntryPrototype>? Guidebook;

    [DataField]
    public bool Blocking;

    /// <summary>Comic lobby bubble with quit / main server, not the blue card.</summary>
    [DataField]
    public bool Finale;

    /// <summary>
    /// Long explanation for the bubble. Instruction stays short because it also has to fit
    /// the hud objective bar. Falls back to Instruction when unset.
    /// </summary>
    [DataField]
    public LocId? BubbleText;

    /// <summary>Hud widget to spotlight while this step is up. Client resolves the actual control.</summary>
    [DataField]
    public TutorialHudTarget HighlightHud = TutorialHudTarget.None;

    [DataField]
    public List<LocId> Speak = new();

    /// <summary>
    /// Hold the step lines until the trainee is actually near this anchor. Without it N.A.N.C.I.
    /// briefs the next room while the player is still walking out of the previous one.
    /// </summary>
    [DataField]
    public string? SpeakAtAnchor;

    [DataField]
    public float SpeakAtRange = 1.5f;

    // safety net, the gate opens by itself so a wandering player never loses the briefing
    [DataField]
    public float SpeakHoldSeconds = 40f;

    /// <summary>Pin the trainee in place from the first line of this step until the holopad goes quiet.</summary>
    [DataField]
    public bool FreezeWhileSpeaking;

    /// <summary>Hold the trainee in place for the whole step, until its completion lets go.</summary>
    [DataField]
    public bool Freeze;

    /// <summary>Hold the trainee from the moment the completion is met, so they stay on the spot they just reached.</summary>
    [DataField]
    public bool FreezeOnceDone;

    [DataField]
    public List<TutorialTileHighlight> HighlightTiles = new();

    [DataField]
    public List<string> HighlightAnchors = new();

    [DataField]
    public LocId? KeybindHint;

    [DataField]
    public List<TutorialWatcher> Watchers = new();

    /// <summary>
    /// While the wires window of this anchor is open, walk the trainee through cutting its power:
    /// a guide card next to the window and a glow on the contacts, wires and lights that matter.
    /// </summary>
    [DataField]
    public string? WiresGuideAnchor;

    /// <summary>
    /// Teach the crafting menu on this step: glow the way to this recipe - hud button, search field or
    /// recipe, build button - with a guide card beside the menu. Also reports the open menu to the server.
    /// </summary>
    [DataField]
    public ProtoId<Content.Shared.Construction.Prototypes.ConstructionPrototype>? CraftingGuideRecipe;

    /// <summary>Parts to print for a machine: glowing recipes and a checklist beside the lathe window.</summary>
    [DataField]
    public TutorialLatheGuide? LatheGuide;

    // null = pick a default from the completion type. 0 = never skip (finale).
    [DataField]
    public float? StuckSkipSeconds;

    /// <summary>
    /// Already done on entry - run OnComplete and move on without a word.
    /// </summary>
    [DataField]
    public bool SkipIfSatisfied;

    /// <summary>
    /// The trainee may eat or drink during this step. Everywhere else ingredients and props stay
    /// out of their mouth.
    /// </summary>
    [DataField]
    public bool AllowIngestion;
}
