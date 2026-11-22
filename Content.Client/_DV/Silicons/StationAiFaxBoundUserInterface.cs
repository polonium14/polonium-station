using System.IO;
using Content.Shared._DV.Silicons;
using Content.Shared.Fax;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Client._DV.Silicons;

[UsedImplicitly]
public sealed partial class StationAiFaxBoundUserInterface : BoundUserInterface
{
    [Dependency] private IFileDialogManager _fileDialog = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private StationAiFaxWindow? _window;
    private StationAiFaxTemplateWindow? _templateWindow;
    private bool _dialogIsOpen;

    public StationAiFaxBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<StationAiFaxWindow>();
        _window.BlankPressed += () => SendMessage(new FaxFileMessage(null, string.Empty, false));
        _window.TemplatePressed += OnTemplatePressed;
        _window.UploadPressed += OnUploadPressed;
        _window.RefreshPressed += () => SendMessage(new FaxRefreshMessage());
        _window.PeerSelected += address => SendMessage(new FaxDestinationMessage(address));
        _window.OpenPressed += document => SendMessage(new StationAiFaxOpenMessage(EntMan.GetNetEntity(document)));
        _window.CopyPressed += document => SendMessage(new StationAiFaxCopyMessage(EntMan.GetNetEntity(document)));
        _window.SendPressed += document => SendMessage(new StationAiFaxSendMessage(EntMan.GetNetEntity(document)));
        _window.ShredPressed += document => SendMessage(new StationAiFaxShredMessage(EntMan.GetNetEntity(document)));

        if (EntMan.TryGetComponent<StationAiFaxComponent>(Owner, out var fax) &&
            EntMan.System<SharedContainerSystem>().TryGetContainer(Owner, StationAiFaxComponent.Container, out var library))
        {
            _window.SetLibrary(library, fax.MaxDocuments);
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is FaxUiState cast)
            _window?.UpdateState(cast);
    }

    private void OnTemplatePressed()
    {
        if (_templateWindow == null)
        {
            if (!EntMan.TryGetComponent<StationAiFaxComponent>(Owner, out var fax))
                return;

            _templateWindow = this.CreateDisposableControl<StationAiFaxTemplateWindow>();
            _templateWindow.SetTemplates(GetTemplates(fax));
            _templateWindow.TemplateSelected += recipe => SendMessage(new StationAiFaxTemplateMessage(recipe));
        }

        if (_templateWindow.IsOpen)
            _templateWindow.MoveToFront();
        else
            _templateWindow.OpenCentered();
    }

    private List<StationAiFaxTemplate> GetTemplates(StationAiFaxComponent fax)
    {
        var templates = new List<StationAiFaxTemplate>();

        foreach (var packId in fax.TemplatePacks)
        {
            if (!_proto.TryIndex(packId, out var pack))
                continue;

            foreach (var recipeId in pack.Recipes)
            {
                if (!_proto.TryIndex(recipeId, out var recipe) || !_proto.TryIndex(recipe.Result, out var result))
                    continue;

                var category = recipe.Categories.Count != 0 && _proto.TryIndex(recipe.Categories[0], out var categoryProto)
                    ? Loc.GetString(categoryProto.Name)
                    : string.Empty;

                templates.Add(new StationAiFaxTemplate(recipeId, result.Name, category));
            }
        }

        templates.Sort((a, b) =>
        {
            var byCategory = string.Compare(a.Category, b.Category, StringComparison.CurrentCulture);
            return byCategory != 0 ? byCategory : string.Compare(a.Name, b.Name, StringComparison.CurrentCulture);
        });

        return templates;
    }

    private async void OnUploadPressed()
    {
        if (_dialogIsOpen)
            return;

        _dialogIsOpen = true;
        var filters = new FileDialogFilters(new FileDialogFilters.Group("txt"));
        await using var file = await _fileDialog.OpenFile(filters, FileAccess.Read);
        _dialogIsOpen = false;

        if (_window == null || _window.Disposed || file == null)
            return;

        using var reader = new StreamReader(file);

        var firstLine = await reader.ReadLineAsync();
        string? label = null;
        var content = await reader.ReadToEndAsync();

        if (firstLine is { })
        {
            if (firstLine.StartsWith('#'))
                label = firstLine[1..].Trim();
            else
                content = firstLine + "\n" + content;
        }

        SendMessage(new FaxFileMessage(
            label?[..Math.Min(label.Length, FaxFileMessageValidation.MaxLabelSize)],
            content[..Math.Min(content.Length, FaxFileMessageValidation.MaxContentSize)],
            false));
    }
}
