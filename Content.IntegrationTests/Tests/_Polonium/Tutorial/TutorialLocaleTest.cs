using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Tutorial;

/// <summary>
/// A tutorial that cannot read its own lines out is worse than no tutorial: the trainee is left in
/// front of a bubble showing the key instead of the sentence. Ids written in YAML are checked when
/// the prototype loads, the ones the code asks for by name are not - those are the list below.
/// </summary>
public sealed class TutorialLocaleTest : GameTest
{
    /// <summary>
    /// Every locale id the tutorial code asks for by literal, lobby tour included. Not every
    /// "tutorial-*" literal is one: overlay and shader ids such as "tutorial-ingame" share the prefix
    /// and are left out on purpose.
    /// </summary>
    private static readonly string[] CodeLines =
    {
        "cmd-startintro-disabled",
        "cmd-startintro-help",
        "cmd-startintro-not-in-lobby",
        "cmd-tutorial-lobby-join-blocked",
        "comp-climbable-verb-climb",
        "examine-verb-name",
        "execution-verb-name",
        "gas-tank-window-internals-toggle-button",
        "guide-entry-radio",
        "humanoid-profile-editor-jobs-tab",
        "humanoid-profile-editor-markings-tab",
        "humanoid-profile-editor-save-button",
        "humanoid-profile-editor-traits-tab",
        "intro-bubble-size-done",
        "intro-bubble-size-sample",
        "intro-bubble-size-title",
        "intro-bubble-size-warning",
        "intro-character-creation-click-to-continue-button",
        "intro-character-creation-final-message",
        "intro-character-creation-message-1",
        "intro-character-creation-reopen-message",
        "intro-character-creation-save-reminder-message",
        "intro-click-any-to-continue-label",
        "intro-click-to-continue-label",
        "intro-info-complete",
        "intro-lobby-overview-character-section-message-1",
        "intro-lobby-overview-character-section-message-2",
        "intro-lobby-overview-message-1",
        "intro-lobby-skip-button",
        "intro-lobby-skip-later",
        "intro-proceed-practical-button",
        "intro-proceed-prompt-agree-later",
        "intro-proceed-prompt-fallback-message",
        "intro-proceed-prompt-message-1",
        "intro-solitary-server-hopping-message",
        "intro-training-hop-question",
        "intro-training-offer-agree",
        "intro-training-offer-disagree",
        "intro-training-offer-message-1",
        "intro-training-offer-message-2",
        "intro-welcome-begin-agree-button",
        "intro-welcome-begin-disagree-button",
        "intro-welcome-begin-question-message",
        "intro-welcome-message-1",
        "intro-welcome-message-2",
        "intro-welcome-reminder-message",
        "tutorial-anchor-labeler-add-text",
        "tutorial-anchor-labeler-applied",
        "tutorial-anchor-labeler-examine",
        "tutorial-anchor-labeler-examine-blank",
        "tutorial-anchor-labeler-hover-line",
        "tutorial-anchor-labeler-hover-no-anchor",
        "tutorial-anchor-labeler-hover-none",
        "tutorial-anchor-labeler-remove-text",
        "tutorial-anchor-labeler-removed",
        "tutorial-bubble-acknowledge",
        "tutorial-bubble-exit",
        "tutorial-bubble-finale-join",
        "tutorial-bubble-finale-quit",
        "tutorial-bubble-guidebook",
        "tutorial-cannot-break-structure",
        "tutorial-cannot-eat",
        "tutorial-cannot-ghost",
        "tutorial-cannot-slice",
        "tutorial-craft-guide-goal",
        "tutorial-craft-guide-need-rotate",
        "tutorial-craft-guide-status-build",
        "tutorial-craft-guide-status-pick",
        "tutorial-craft-guide-status-place",
        "tutorial-craft-guide-status-search",
        "tutorial-craft-guide-step-build",
        "tutorial-craft-guide-step-build-body",
        "tutorial-craft-guide-step-find",
        "tutorial-craft-guide-step-find-body",
        "tutorial-craft-guide-step-place",
        "tutorial-craft-guide-step-place-body",
        "tutorial-craft-guide-title",
        "tutorial-guide-done",
        "tutorial-hint-less",
        "tutorial-hint-more",
        "tutorial-holopad-countdown",
        "tutorial-holopad-meteor-impact",
        "tutorial-holopad-quip-death",
        "tutorial-holopad-r13-eat",
        "tutorial-holopad-r13-eat-2",
        "tutorial-holopad-r13-eat-cannot",
        "tutorial-holopad-r13-eat-cannot-2",
        "tutorial-holopad-r13-eat-maybe",
        "tutorial-holopad-r13-eat-maybe-2",
        "tutorial-holopad-r16-gloves-off",
        "tutorial-holopad-r16-shocked",
        "tutorial-holopad-stuck-hint",
        "tutorial-holopad-stuck-skip",
        "tutorial-lathe-guide-goal",
        "tutorial-lathe-guide-item-done",
        "tutorial-lathe-guide-item-missing",
        "tutorial-lathe-guide-need-materials",
        "tutorial-lathe-guide-status-done",
        "tutorial-lathe-guide-status-load",
        "tutorial-lathe-guide-status-order",
        "tutorial-lathe-guide-step-load",
        "tutorial-lathe-guide-step-load-body",
        "tutorial-lathe-guide-step-order",
        "tutorial-lathe-guide-step-order-body",
        "tutorial-lathe-guide-step-take",
        "tutorial-lathe-guide-step-take-body",
        "tutorial-lathe-guide-title",
        "tutorial-med-container",
        "tutorial-med-corpse",
        "tutorial-med-dead",
        "tutorial-med-empty",
        "tutorial-med-self",
        "tutorial-range-next-target",
        "tutorial-redial-message",
        "tutorial-wires-guide-dark-for",
        "tutorial-wires-guide-for-good",
        "tutorial-wires-guide-goal",
        "tutorial-wires-guide-legend-contact",
        "tutorial-wires-guide-legend-wire",
        "tutorial-wires-guide-need-crowbar",
        "tutorial-wires-guide-need-multitool",
        "tutorial-wires-guide-status-dead",
        "tutorial-wires-guide-status-find",
        "tutorial-wires-guide-status-found",
        "tutorial-wires-guide-status-one-cut",
        "tutorial-wires-guide-status-window",
        "tutorial-wires-guide-step-find",
        "tutorial-wires-guide-step-find-body",
        "tutorial-wires-guide-step-pry",
        "tutorial-wires-guide-step-pry-body",
        "tutorial-wires-guide-title",
        "verb-categories-eject",
        "wire-name-power",
    };

    [Test]
    public async Task EveryLineTheCodeAsksForExists()
    {
        var loc = Server.ResolveDependency<ILocalizationManager>();

        await Server.WaitAssertion(() =>
        {
            var missing = CodeLines.Where(id => !loc.HasString(id)).ToList();

            Assert.That(missing, Is.Empty,
                $"the tutorial code asks for lines the locale does not have: {string.Join(", ", missing)}");
        });
    }

    /// <summary>
    /// The same for everything the flow itself names. The prototype loader already refuses an unknown
    /// id, but it does so against whichever culture happened to be loaded - this checks the one the
    /// trainee will actually be served.
    /// </summary>
    [Test]
    public async Task EveryLineTheFlowSaysExists()
    {
        var proto = Server.ResolveDependency<IPrototypeManager>();
        var loc = Server.ResolveDependency<ILocalizationManager>();

        await Server.WaitAssertion(() =>
        {
            var missing = new List<string>();

            foreach (var flow in proto.EnumeratePrototypes<TutorialFlowPrototype>())
            {
                foreach (var step in TutorialContent.Steps(proto, flow))
                {
                    foreach (var node in TutorialContent.Nodes(proto, step))
                    {
                        foreach (var id in TutorialContent.LocIds(node))
                        {
                            if (!loc.HasString(id))
                                missing.Add($"{step.ID}: {id}");
                        }
                    }
                }
            }

            Assert.That(missing, Is.Empty, $"steps say lines that are not written: {string.Join(", ", missing)}");
        });
    }

    /// <summary>Every step puts something in front of the trainee, even if it is only a line to read.</summary>
    [Test]
    public async Task EveryStepSaysSomething()
    {
        var proto = Server.ResolveDependency<IPrototypeManager>();

        await Server.WaitAssertion(() =>
        {
            var mute = proto.EnumeratePrototypes<TutorialStepPrototype>()
                .Where(step => string.IsNullOrWhiteSpace(step.Instruction.Id))
                .Select(step => step.ID)
                .ToList();

            Assert.That(mute, Is.Empty, $"steps with no instruction at all: {string.Join(", ", mute)}");
        });
    }
}
