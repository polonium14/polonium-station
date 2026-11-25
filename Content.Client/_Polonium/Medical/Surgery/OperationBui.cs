using System.Linq;
using Content.Client._Polonium.UserInterface.Controls;
using Content.Client._Shitmed.Choice.UI;
using Content.Client.Stylesheets;
using Content.Client._Shitmed.Medical.Surgery;
using Content.Shared._Polonium.Medical.Surgery;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Polonium.Medical.Surgery;

/// <summary>
/// Drives <see cref="OperationWindow"/> from the surgeries the server offers for each body part.
/// </summary>
[UsedImplicitly]
public sealed partial class OperationBui : BoundUserInterface
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private static readonly Color Done = Color.FromHex("#5fd35f");
    private static readonly Color Blocked = Color.FromHex("#8a8a8a");
    private static readonly LocId OtherGroup = "surgery-group-other";

    private readonly SurgerySystem _system;

    [ViewVariables]
    private OperationWindow? _window;

    private NetEntity? _part;
    private EntProtoId? _surgery;
    private readonly Dictionary<EntProtoId, (ChoiceControl Button, string Name)> _surgeryButtons = new();

    public OperationBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _system = _entities.System<SurgerySystem>();
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<OperationWindow>();
        _window.Title = Loc.GetString("surgery-ui-window-title");
        _window.OnPartPressed += OnPartPressed;
        Rebuild();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        Rebuild();
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (message is not SurgeryBuiRefreshMessage)
            return;

        BuildSteps();
        RefreshStatus();
    }

    private void OnPartPressed(TargetBodyPart target)
    {
        if (!Parts().TryGetValue(target, out var part) || part == _part)
            return;

        _part = part;
        _surgery = null;
        // Another part has another list, so it starts from the top.
        _window?.SurgeriesScroll.SetScrollValue(default);
        Rebuild();
    }

    private void OnSurgeryPressed(EntProtoId surgery)
    {
        _surgery = surgery;
        BuildSteps();
        RefreshStatus();
    }

    /// <summary>
    /// The patient's parts that the server offers surgeries for, by their place on the doll.
    /// </summary>
    private Dictionary<TargetBodyPart, NetEntity> Parts()
    {
        var parts = new Dictionary<TargetBodyPart, NetEntity>();
        if (State is not SurgeryBuiState state)
            return parts;

        foreach (var part in state.Choices.Keys)
        {
            if (_entities.TryGetComponent(_entities.GetEntity(part), out OrganComponent? organ)
                && organ.Category is { } category
                && LimbTargetMap.TryGetTarget(category, out var target))
                parts[target] = part;
        }

        return parts;
    }

    private void Rebuild()
    {
        if (_window == null || State is not SurgeryBuiState state)
            return;

        var parts = Parts();
        if (_part == null || !state.Choices.ContainsKey(_part.Value))
        {
            _surgery = null;
            _part = parts.TryGetValue(TargetBodyPart.Chest, out var chest) ? chest : parts.Values.FirstOrNull();
        }

        var pickedPart = _entities.GetEntity(_part);
        _window.ShowParts(parts.Keys,
            parts.FirstOrNull(pair => pair.Value == _part)?.Key,
            pickedPart is { } picked ? LimbDoll.Capitalize(Name(picked)) : string.Empty,
            _entities.HasComponent<IncisionOpenComponent>(pickedPart));

        _window.Surgeries.DisposeAllChildren();
        _surgeryButtons.Clear();

        var offered = _part is { } current ? state.Choices[current] : new List<EntProtoId>();
        var left = offered.Where(id => _system.GetSingleton(id) != null).ToHashSet();
        if (_surgery is { } chosen && !left.Contains(chosen))
            _surgery = null;

        foreach (var group in _prototypes.EnumeratePrototypes<SurgeryGroupPrototype>().OrderBy(group => group.Order))
        {
            var listed = group.Surgeries.Where(left.Contains).ToList();
            left.ExceptWith(listed);
            AddGroup(group.Name, listed);
        }

        AddGroup(OtherGroup, left.OrderBy(id => Name(_system.GetSingleton(id)!.Value), StringComparer.Ordinal).ToList());

        BuildSteps();
        RefreshStatus();
    }

    private void AddGroup(LocId name, List<EntProtoId> surgeries)
    {
        if (_window == null || surgeries.Count == 0)
            return;

        _window.Surgeries.AddChild(new Label
        {
            Text = Loc.GetString(name),
            StyleClasses = { "LabelSubText" },
            Margin = new Thickness(2, 6, 0, 2),
        });

        // A surgery that is another way to do one of this group shares that surgery's row.
        var rows = new Dictionary<EntProtoId, BoxContainer>();
        foreach (var id in surgeries)
        {
            var entity = _system.GetSingleton(id)!.Value;
            var button = new ChoiceControl { HorizontalExpand = true };
            button.Button.ToggleMode = true;
            button.Button.OnPressed += _ => OnSurgeryPressed(id);

            if (_entities.TryGetComponent(entity, out SurgeryAlternativeComponent? alternative)
                && alternative.Of.FirstOrNull(rows.ContainsKey) is { } main)
            {
                _surgeryButtons[id] = (button, Loc.GetString(alternative.Label));
                button.Set(_surgeryButtons[id].Name, null);
                rows[main].AddChild(button);
                continue;
            }

            _surgeryButtons[id] = (button, Name(entity));
            button.Set(_surgeryButtons[id].Name, null);
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true };
            row.AddChild(button);
            rows[id] = row;
            _window.Surgeries.AddChild(row);
        }
    }

    private void BuildSteps()
    {
        if (_window == null)
            return;

        _window.Steps.DisposeAllChildren();
        if (_part is not { } part
            || _surgery is not { } surgeryId
            || _system.GetSingleton(surgeryId) is not { } surgery
            || !_entities.TryGetComponent(surgery, out SurgeryComponent? surgeryComp))
        {
            _window.SurgeryName.SetMessage(Loc.GetString("surgery-window-pick-surgery"));
            return;
        }

        _window.SurgeryName.SetMessage(FormattedMessage.FromMarkupOrThrow(
            $"[color={StyleNano.NanoGold.ToHex()}][bold]{FormattedMessage.EscapeText(Name(surgery))}[/bold][/color]"));
        var isBody = _entities.HasComponent<BodyComponent>(_entities.GetEntity(part));

        // A surgery that cannot start yet says so above its steps, and the line leads to what has to be done first.
        if (_player.LocalEntity is { } user
            && _system.GetNextStep(Owner, _entities.GetEntity(part), surgery, user) is { } next
            && next.Surgery.Owner != surgery
            && _entities.GetComponent<MetaDataComponent>(next.Surgery.Owner).EntityPrototype is { } first)
        {
            var needed = new ChoiceControl();
            needed.Set(FormattedMessage.FromMarkupOrThrow(
                $"[bold]{FormattedMessage.EscapeText(Loc.GetString("surgery-window-first", ("surgery", Name(next.Surgery.Owner))))}[/bold]"), null);
            needed.Button.OnPressed += _ => OnSurgeryPressed(first.ID);
            _window.Steps.AddChild(needed);
        }

        for (var i = 0; i < surgeryComp.Steps.Count; i++)
        {
            var stepId = surgeryComp.Steps[i];
            if (_system.GetSingleton(stepId) is not { } step)
                continue;

            var button = new SurgeryStepButton { Step = step, SurgeryId = surgeryId, StepId = stepId };
            button.Set($"{i + 1}. {Name(step)}", _entities.GetComponentOrNull<SpriteComponent>(step)?.Icon?.Default);
            button.Button.OnPressed += _ => SendMessage(new SurgeryStepChosenBuiMsg(part, surgeryId, stepId, isBody));
            _window.Steps.AddChild(button);
        }
    }

    /// <summary>
    /// Colours the surgeries and steps by how far the operation on the picked part has got.
    /// </summary>
    private void RefreshStatus()
    {
        if (_window == null
            || _part is not { } netPart
            || _player.LocalEntity is not { } user
            || !_entities.TryGetComponent(Owner, out SurgeryTargetComponent? target)
            || !target.CanOperate)
            return;

        var part = _entities.GetEntity(netPart);
        // Only a surgery that others build on (an opened incision, an opened chest) is a stage worth marking
        // as reached. Any other surgery with no step left simply has nothing to do: a closed chest, a healthy organ.
        var stages = new HashSet<EntProtoId>();
        foreach (var id in _surgeryButtons.Keys)
        {
            if (_entities.GetComponent<SurgeryComponent>(_system.GetSingleton(id)!.Value).Requirement is { } requirement)
                stages.Add(requirement);
        }

        foreach (var (id, (button, name)) in _surgeryButtons)
        {
            var surgery = _system.GetSingleton(id)!.Value;
            var next = _system.GetNextStep(Owner, part, surgery, user);
            var text = new FormattedMessage();
            button.Button.Pressed = id == _surgery;

            if (next == null)
                text.PushColor(stages.Contains(id) ? Done : Blocked);
            else if (next.Value.Surgery.Owner != surgery)
                text.PushColor(Blocked);

            text.AddText(name);
            // A blocked surgery stays on the list and says what has to be done first.
            if (next != null && next.Value.Surgery.Owner != surgery)
                text.AddText("\n" + Loc.GetString("surgery-window-requires", ("surgery", Name(next.Value.Surgery.Owner))));

            button.Set(text, null);
        }

        if (_surgery is not { } chosen || _system.GetSingleton(chosen) is not { } chosenEnt)
            return;

        var nextStep = _system.GetNextStep(Owner, part, chosenEnt, user);
        var index = 0;
        foreach (var child in _window.Steps.Children)
        {
            if (child is not SurgeryStepButton step)
                continue;

            // A negative step comes from a surgeon who may skip ahead: everything up to it can be done.
            var done = nextStep == null
                || nextStep.Value.Surgery.Owner == chosenEnt
                && (nextStep.Value.Step < 0 ? index > -nextStep.Value.Step - 1 : index < nextStep.Value.Step);
            var isNext = nextStep != null
                && nextStep.Value.Surgery.Owner == chosenEnt
                && (nextStep.Value.Step < 0 ? index <= -nextStep.Value.Step - 1 : index == nextStep.Value.Step);

            step.Button.Disabled = !isNext;
            step.Button.Modulate = done ? Done : Color.White;
            step.ToolTip = isNext && !_system.CanPerformStepWithHeld(user, Owner, part, step.Step, false, out var reason, chosenEnt)
                ? reason
                : null;
            index++;
        }
    }

    private string Name(EntityUid entity)
    {
        return _entities.GetComponent<MetaDataComponent>(entity).EntityName;
    }
}
