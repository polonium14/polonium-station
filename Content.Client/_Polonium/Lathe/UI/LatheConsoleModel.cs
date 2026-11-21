using System.Linq;
using Content.Client.Lathe;
using Content.Client.Materials;
using Content.Shared._Polonium.Lathe;
using Content.Shared.CCVar;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Emag.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Lathe;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Materials;
using Content.Shared.Materials.OreSilo;
using Content.Shared.Power.EntitySystems;
using Content.Shared.ReagentSpeed;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Lathe.UI;

public readonly record struct LatheCatalogEntry(LatheRecipePrototype Recipe, string Name);

public readonly record struct LatheOutput(LatheRecipePrototype Recipe, TimeSpan Time);

/// <summary>
/// What the operator console shows: the lathe's state as the client knows it, read once a frame, plus the operator's own choices.
/// </summary>
public sealed partial class LatheConsoleModel
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public const int MaxAmount = SharedLatheSystem.MaxItemsPerRequest;

    /// <summary>
    /// How many sheets fill a material gauge to the top.
    /// </summary>
    public const float GaugeSheets = 40f;

    private static readonly TimeSpan DenyTime = TimeSpan.FromSeconds(1.6);
    private static readonly TimeSpan AbortWindow = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan FinishTolerance = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan OutputTime = TimeSpan.FromSeconds(1.4);
    private static readonly TimeSpan GlitchHold = TimeSpan.FromSeconds(2.4);
    private const int GlitchSpread = 24;

    private readonly LatheSystem _lathe;
    private readonly MaterialStorageSystem _materialStorage;
    private readonly SharedPowerReceiverSystem _power;
    private readonly EmagSystem _emag;
    private readonly SharedSolutionContainerSystem _solutions;

    private readonly List<LatheRecipePrototype> _recipes = new();
    private TimeSpan? _abortedAt;
    private TimeSpan _nextGlitch;
    private int _glitchStep;

    public readonly EntityUid Entity;
    public readonly List<LatheCatalogEntry> Catalog = new();
    public readonly List<LatheCategoryPrototype?> Categories = new() { null };
    public readonly Dictionary<ProtoId<MaterialPrototype>, int> Reserved = new();
    public readonly List<LatheOutput> Outputs = new();

    public Dictionary<ProtoId<MaterialPrototype>, int> Materials = new();
    public LatheRecipeBatch[] Queue = Array.Empty<LatheRecipeBatch>();
    public LatheCategoryPrototype? Category;
    public string Search = string.Empty;
    public int Amount = 1;
    public LatheRecipePrototype? Hover;

    public LatheRecipePrototype? Printing;
    public TimeSpan PrintStart;
    public TimeSpan PrintLength;

    public LatheRecipePrototype? Denied;
    public string DeniedText = string.Empty;
    public TimeSpan DeniedUntil;

    public float MaterialMultiplier = 1f;
    public float TimeMultiplier = 1f;
    public bool HasServers;
    public bool CanEject;
    public bool Powered;
    public bool Emagged;
    public bool Stopped;
    public bool SiloLinked;
    public bool ReducedMotion;

    public ReagentSpeedComponent? Lube;
    public Solution? LubeSolution;

    public event Action? CatalogChanged;

    /// <summary>
    /// Raised when the item being printed leaves the plotter. False means it was cancelled.
    /// </summary>
    public event Action<bool>? ItemEnded;

    public LatheConsoleModel(EntityUid entity)
    {
        IoCManager.InjectDependencies(this);

        Entity = entity;
        _lathe = _entMan.System<LatheSystem>();
        _materialStorage = _entMan.System<MaterialStorageSystem>();
        _power = _entMan.System<SharedPowerReceiverSystem>();
        _emag = _entMan.System<EmagSystem>();
        _solutions = _entMan.System<SharedSolutionContainerSystem>();
    }

    public TimeSpan Now => _timing.RealTime;

    public bool Denying => Denied != null;

    public float Progress
    {
        get
        {
            if (Printing == null || PrintLength <= TimeSpan.Zero)
                return 0f;

            return (float) Math.Clamp((_timing.CurTime - PrintStart) / PrintLength, 0d, 1d);
        }
    }

    public double SecondsLeft => Printing == null ? 0d : Math.Max(0d, (PrintStart + PrintLength - _timing.CurTime).TotalSeconds);

    public string RecipeName(LatheRecipePrototype recipe)
    {
        return _lathe.GetRecipeName(recipe);
    }

    public string RecipeDescription(LatheRecipePrototype recipe)
    {
        return _lathe.GetRecipeDescription(recipe);
    }

    public int Cost(LatheRecipePrototype recipe, int amount)
    {
        return SharedLatheSystem.AdjustMaterial(amount, recipe.ApplyMaterialDiscount, MaterialMultiplier);
    }

    /// <summary>
    /// How long the lathe would take to print the recipe with what is in its tank right now.
    /// </summary>
    public TimeSpan PrintTime(LatheRecipePrototype recipe)
    {
        var time = recipe.CompleteTime * TimeMultiplier;
        if (Lube is not { } lube || LubeSolution is not { } solution)
            return time;

        var cost = lube.Cost;
        var modifiers = lube.Modifiers;
        if (cost <= FixedPoint2.Zero)
            return time;

        foreach (var (reagent, modifier) in modifiers)
        {
            var used = FixedPoint2.Min(solution.GetTotalPrototypeQuantity(reagent), cost);
            time *= 1f - (1f - modifier) * (used / cost).Float();
        }

        return time;
    }

    public int SheetVolume(MaterialPrototype material)
    {
        return _materialStorage.GetSheetVolume(material);
    }

    public bool CanProduce(LatheRecipePrototype recipe)
    {
        return _lathe.CanProduce(Entity, recipe, Materials, Amount);
    }

    public void SetRecipes(List<ProtoId<LatheRecipePrototype>> recipes)
    {
        _recipes.Clear();
        foreach (var id in recipes)
        {
            if (_proto.Resolve(id, out var recipe))
                _recipes.Add(recipe);
        }

        var categories = new List<LatheCategoryPrototype>();
        foreach (var recipe in _recipes)
        {
            foreach (var id in recipe.Categories)
            {
                if (_proto.Resolve(id, out var category) && !categories.Contains(category))
                    categories.Add(category);
            }
        }

        Categories.Clear();
        Categories.Add(null);
        Categories.AddRange(categories.OrderBy(category => Loc.GetString(category.Name)));
        if (Category != null && !Categories.Contains(Category))
            Category = null;

        RebuildCatalog();
    }

    public void SetCategory(LatheCategoryPrototype? category)
    {
        if (Category == category)
            return;

        Category = category;
        RebuildCatalog();
    }

    public void SetSearch(string search)
    {
        Search = search;
        RebuildCatalog();
    }

    private void RebuildCatalog()
    {
        Catalog.Clear();
        foreach (var recipe in _recipes)
        {
            if (Category == null || recipe.Categories.Contains(Category.ID))
                Catalog.Add(new LatheCatalogEntry(recipe, _lathe.GetRecipeName(recipe)));
        }

        // With every category shown, recipes that share a name get their category appended, as in the stock menu.
        if (Category == null)
        {
            var shared = Catalog.GroupBy(entry => entry.Name).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
            for (var i = 0; i < Catalog.Count; i++)
            {
                var entry = Catalog[i];
                if (!shared.Contains(entry.Name) || entry.Recipe.Categories.Count == 0)
                    continue;

                var name = Loc.GetString("lathe-menu-recipe-name-category",
                    ("name", entry.Name),
                    ("category", Loc.GetString(_proto.Index(entry.Recipe.Categories[0]).Name)));
                Catalog[i] = entry with { Name = name };
            }
        }

        var search = Search.Trim();
        if (search.Length != 0)
            Catalog.RemoveAll(entry => !entry.Name.Contains(search, StringComparison.InvariantCultureIgnoreCase));

        Catalog.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        CatalogChanged?.Invoke();
    }

    public void Deny(LatheRecipePrototype recipe)
    {
        DeniedText = string.Empty;
        foreach (var (id, amount) in recipe.Materials)
        {
            var lack = Cost(recipe, amount) * Amount - Materials.GetValueOrDefault(id);
            if (lack <= 0 || !_proto.Resolve(id, out var material))
                continue;

            DeniedText = Loc.GetString("polonium-lathe-shortage-detail",
                ("material", Loc.GetString(material.Name)),
                ("amount", lack / (float) SheetVolume(material)),
                ("unit", Loc.GetString(material.Unit)));
            break;
        }

        Denied = recipe;
        DeniedUntil = Now + DenyTime;
    }

    /// <summary>
    /// A number below the range that decides where a screen fault of an emagged lathe sits.
    /// It keeps its value for a few seconds, then all faults hop at once.
    /// </summary>
    public int Glitch(int salt, int range)
    {
        var hash = unchecked((uint) Entity.Id * 73856093u ^ (uint) _glitchStep * 19349663u ^ (uint) salt * 83492791u);
        hash ^= hash >> 13;
        hash = unchecked(hash * 0x5bd1e995u);
        hash ^= hash >> 15;
        return (int) (hash % (uint) range);
    }

    public void NoteAbort()
    {
        if (Printing != null)
            _abortedAt = Now;
    }

    /// <summary>
    /// Reads the lathe's components. Called once a frame.
    /// </summary>
    public void Refresh()
    {
        ReducedMotion = _cfg.GetCVar(CCVars.ReducedMotion);
        Powered = _power.IsPowered(Entity);
        Emagged = _entMan.HasComponent<EmagLatheRecipesComponent>(Entity) && _emag.CheckFlag(Entity, EmagType.Interaction);
        Stopped = _entMan.HasComponent<LatheStoppedComponent>(Entity);
        SiloLinked = _entMan.TryGetComponent(Entity, out OreSiloClientComponent? silo) && silo.Silo != null;

        if (Emagged && !ReducedMotion && Now >= _nextGlitch)
        {
            _glitchStep++;
            _nextGlitch = Now + GlitchHold + TimeSpan.FromSeconds(Glitch(0, GlitchSpread) / 10d);
        }

        if (_entMan.TryGetComponent(Entity, out LatheComponent? lathe))
        {
            MaterialMultiplier = lathe.MaterialUseMultiplier;
            TimeMultiplier = lathe.TimeMultiplier;
            HasServers = lathe.DynamicPacks.Count > 0;
        }

        if (_entMan.TryGetComponent(Entity, out MaterialStorageComponent? storage))
        {
            CanEject = storage.CanEjectStoredMaterials;
            Materials = _materialStorage.GetStoredMaterials((Entity, storage));
        }

        Lube = null;
        LubeSolution = null;
        if (_entMan.TryGetComponent(Entity, out ReagentSpeedComponent? speed)
            && _solutions.TryGetSolution(Entity, speed.Solution, out _, out var solution))
        {
            Lube = speed;
            LubeSolution = solution;
        }

        RefreshPrinting();
        RefreshReserved();

        if (Denied != null && Now >= DeniedUntil)
            Denied = null;

        for (var i = Outputs.Count - 1; i >= 0; i--)
        {
            if (Now - Outputs[i].Time > OutputTime)
                Outputs.RemoveAt(i);
        }
    }

    private void RefreshPrinting()
    {
        LatheRecipePrototype? printing = null;
        var start = TimeSpan.Zero;
        var length = TimeSpan.Zero;
        if (_entMan.TryGetComponent(Entity, out LatheProgressComponent? progress) && _proto.Resolve(progress.Recipe, out printing))
        {
            start = progress.StartTime;
            length = progress.Length;
        }

        if (Printing != null && (printing == null || start != PrintStart))
        {
            var aborted = _abortedAt is { } at && Now - at <= AbortWindow;
            _abortedAt = null;
            var produced = !aborted && _timing.CurTime >= PrintStart + PrintLength - FinishTolerance;
            if (produced)
                Outputs.Add(new LatheOutput(Printing, Now));

            ItemEnded?.Invoke(produced);
        }

        Printing = printing;
        PrintStart = start;
        PrintLength = length;
    }

    private void RefreshReserved()
    {
        Reserved.Clear();
        foreach (var batch in Queue)
        {
            if (_proto.Resolve(batch.Recipe, out var recipe))
                Reserve(recipe, batch.ItemsRequested - batch.ItemsPrinted);
        }

        if (Printing != null)
            Reserve(Printing, 1);
    }

    private void Reserve(LatheRecipePrototype recipe, int count)
    {
        foreach (var (id, amount) in recipe.Materials)
        {
            Reserved[id] = Reserved.GetValueOrDefault(id) + Cost(recipe, amount) * count;
        }
    }
}
