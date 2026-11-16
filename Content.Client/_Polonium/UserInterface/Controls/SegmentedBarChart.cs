using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace Content.Client._Polonium.UserInterface.Controls;


/// <summary>
/// A horizontal bar consisting of several colored segments side-by-side. This is used (at time of writing) for the UI
/// of gas analyzers, mops and cryo pods. Entries are animated by default.
/// Polonium copy of <see cref="Content.Client.UserInterface.Controls.SegmentedBarChart"/> that can also draw its
/// labels whole on top of the bar, see <see cref="LabelsOnTop"/>.
/// </summary>
/// <remarks>
/// To supply data to the chart, first call <see cref="Clear"/> to get rid of old entries and then call
/// <see cref="SetEntry"/> for each item to be added. Note that the order of <see cref="SetEntry"/> calls matters.
/// </remarks>
public sealed class SegmentedBarChart : Control
{
    private sealed class Entry
    {
        public float WidthFraction;  // This entry's width as a fraction of the chart's total width (between 0 and 1)
        public float TargetAmount;
        public string Uid; // This UID is used to track entries between frames, for animation.
        public string? Tooltip;
        public Color Color;
        public Label Label;

        // Polonium: how far, as a fraction of the chart's width, the entry is still drawn from its place while it
        // slides there after a reorder; and whether a SetEntry call has claimed it since the last Clear.
        public float Offset;
        public bool Placed;

        // Polonium: the color drawn right now, fading towards Color when an entry changes color.
        public Color ShownColor;

        public Entry(string uid, Label label)
        {
            Uid = uid;
            Label = label;
        }
    }

    public const string StyleClassClassicSplitBar = "ClassicSplitBar";

    public const string StylePropertyAnimated = "animated";
    public const string StylePropertyShowRuler = "showRuler";
    public const string StylePropertyShowBackground = "showBackground";

    public const string StylePropertyNotchColor = "notchColor";
    public const string StylePropertyBackgroundColor = "backgroundColor";

    public const string StylePropertyGap = "gap";
    public const string StylePropertyMinEntryWidth = "minEntryWidth";

    public const string StylePropertySmallNotchHeight = "smallNotchHeight";
    public const string StylePropertyMediumNotchHeight = "mediumNotchHeight";
    public const string StylePropertyBigNotchHeight = "bigNotchHeight";
    public const string StylePropertyMediumNotchInterval = "mediumNotchInterval";
    public const string StylePropertyBigNotchInterval = "bigNotchInterval";
    public const string StylePropertyMinSmallNotchScreenDistance = "minSmallNotchScreenDistance";

    /// <summary>
    /// When Gap is greater than zero, all segments are separated by empty space. Gap is measured in UI units.
    /// This is incompatible with ShowRuler. If ShowRuler is enabled, Gap is ignored.
    /// </summary>
    public float? Gap { get; set; } = null;

    /// <summary>
    /// The minimum width of a segment in UI units.
    /// </summary>
    public float? MinEntryWidth { get; set; } = null;

    /// <summary>
    /// How many units of "Amount" fit into this chart. For example, when Capacity is 50, an entry with an amount of 5
    /// will take up 10% of the chart. If this is -1, the capacity is flexible and equal to the sum of the entries.
    /// </summary>
    public float Capacity { get; set; } = -1;

    /// <summary>
    /// Polonium: draws each label whole on top of the bar instead of cutting it to its segment. A label runs over the
    /// next segments when its own is narrow and moves right past the label before it; only the chart's edge cuts it.
    /// </summary>
    public bool LabelsOnTop
    {
        get => _labelsOnTop;
        set
        {
            _labelsOnTop = value;
            RectClipContent = value;
            foreach (var entry in _entries)
            {
                entry.Label.ClipText = !value;
            }

            InvalidateArrange();
        }
    }

    private bool _labelsOnTop;

    /// <summary>
    /// Polonium: space between two labels on top when one has to move past the other.
    /// </summary>
    private const float LabelGap = 8f;

    /// <summary>
    /// Polonium: whether the ruler draws a notch for every unit, or only the medium and big ones.
    /// </summary>
    public bool ShowSmallNotches { get; set; } = true;

    /// <summary>
    /// Polonium: shows a label inside its segment only when it fits whole, instead of cutting it off.
    /// </summary>
    public bool HideClippedLabels { get; set; }

    /// <summary>
    /// Polonium: when the SetEntry order changes, moves the existing segments to their new places and slides them
    /// there, instead of letting a segment that moved left shrink away and grow again further right.
    /// </summary>
    public bool SlideOnReorder { get; set; }

    /// <summary>
    /// Whether this chart is animated.
    /// </summary>
    public bool? Animated { get; set; } = null;

    /// <summary>
    /// Whether the ruler is drawn. Check the cryo pod UI to see what the ruler looks like.
    /// The ruler can be further configured through the various "Notch" properties.
    /// </summary>
    public bool? ShowRuler { get; set; } = null;

    /// <summary>
    /// Whether a background will be drawn behind the chart. The background is a simple rectangle and does not have
    /// gaps when Gap is non-zero. The color of the background is determined by the style property "backgroundColor".
    /// </summary>
    public bool? ShowBackground { get; set; } = null;

    // Every `Notch` variable is related to the ruler.
    public int? MediumNotchInterval { get; set; } = null;
    public int? BigNotchInterval { get; set; } = null;

    /// <summary>
    /// For the cryo pod UI, when we have a very large beaker (e.g. a bluespace beaker) we might need to increase the
    /// distance between notches to prevent all the notches from turning into a solid rectangle. When the distance
    /// between notches is less than MinSmallNotchScreenDistance in UI units, the distance is increased by a factor of
    /// ten (repeated as often as necessary).
    /// </summary>
    public int? MinSmallNotchScreenDistance { get; set; } = null;

    public float? SmallNotchHeight { get; set; } = null;
    public float? MediumNotchHeight { get; set; } = null;
    public float? BigNotchHeight { get; set; } = null;

    // Most properties can either be provided by the stylesheet or overriden through a property.
    // These helper computed properties make the bulk of the code a little less ugly.
    private Color _backgroundColor => StylePropertyDefault(StylePropertyBackgroundColor, new Color(0.1f, 0.1f, 0.1f));
    private Color _notchColor => StylePropertyDefault(StylePropertyNotchColor, Color.White.WithAlpha(0.25f));
    private float _gap => Gap ?? StylePropertyDefault<float>(StylePropertyGap, 0);
    private float _minEntryWidth => MinEntryWidth ?? StylePropertyDefault<float>(StylePropertyMinEntryWidth, 0);
    private bool _animated => Animated ?? StylePropertyDefault(StylePropertyAnimated, false);
    private bool _showRuler => ShowRuler ?? StylePropertyDefault(StylePropertyShowRuler, false);
    private bool _showBackground => ShowBackground ?? StylePropertyDefault(StylePropertyShowBackground, false);
    private float _mediumNotchInterval => MediumNotchInterval ?? StylePropertyDefault(StylePropertyMediumNotchInterval, 5);
    private float _bigNotchInterval => BigNotchInterval ?? StylePropertyDefault(StylePropertyBigNotchInterval, 10);
    private float _minSmallNotchScreenDistance => MinSmallNotchScreenDistance ?? StylePropertyDefault(StylePropertyMinSmallNotchScreenDistance, 2);
    private float _smallNotchHeight => SmallNotchHeight ?? StylePropertyDefault(StylePropertySmallNotchHeight, 0.1f);
    private float _mediumNotchHeight => MediumNotchHeight ?? StylePropertyDefault(StylePropertyMediumNotchHeight, 0.25f);
    private float _bigNotchHeight => BigNotchHeight ?? StylePropertyDefault(StylePropertyBigNotchHeight, 1f);

    // We don't animate new entries until this control has had at least one update where its width was non-zero.
    private bool _hasHadNonZeroWidth = false;

    // This is used to keep the segments of the chart in the same order as the SetEntry calls.
    // For example: In update 1 we might get cryox, alox, bic (in that order), and in update 2 we get alox, cryox, bic.
    // To keep the order of the entries the same as the order of the SetEntry calls, we let the old cryox entry
    // disappear and create a new cryox entry behind the alox entry.
    private int _nextUpdateableEntry = 0;

    private readonly List<Entry> _entries = new();


    public SegmentedBarChart()
    {
        MouseFilter = MouseFilterMode.Pass;
        TooltipSupplier = SupplyTooltip;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_showBackground)
            handle.DrawRect(PixelSizeBox, _backgroundColor);

        // Draw the entry backgrounds
        foreach (var (entry, xMinUI, xMaxUI) in EntryRanges(Width))
        {
            var xMin = UIScale * xMinUI;
            var xMax = UIScale * xMaxUI;
            if (xMin != xMax)
                handle.DrawRect(new(xMin, 0, xMax, PixelHeight), entry.ShownColor);
        }

        // Draw the ruler
        if (_showRuler)
        {
            // These computed properties are used in the loop. Compute them only once.
            var bigNotchInterval = _bigNotchInterval;
            var mediumNotchInterval = _mediumNotchInterval;
            var bigNotchHeight = _bigNotchHeight;
            var mediumNotchHeight = _mediumNotchHeight;
            var smallNotchHeight = _smallNotchHeight;
            var notchColor = _notchColor;

            var capacity = GetCapacity();
            var unitWidth = PixelWidth / capacity;

            // This math ensures the distance between notches is not less than `MinSmallNotchScreenDistance`.
            // We make sure that `unitsPerNotch` is always a power of ten (normally 1, 10 or 100).
            var maxNotches = Width / _minSmallNotchScreenDistance;
            var exp = MathF.Floor(MathF.Log10(maxNotches / capacity));
            var unitsPerNotch = 1f / MathF.Min(1, MathF.Pow(10, exp));

            var notchCount = (int)MathF.Floor(capacity / unitsPerNotch);
            var notchDistance = unitWidth * unitsPerNotch;

            for (int i = 0; i <= notchCount; i++)
            {
                // Polonium: optionally only the medium and big notches.
                if (!ShowSmallNotches && i % bigNotchInterval != 0 && i % mediumNotchInterval != 0)
                    continue;

                var x = i * notchDistance;
                var height = (i % bigNotchInterval    == 0 ? bigNotchHeight :
                              i % mediumNotchInterval == 0 ? mediumNotchHeight :
                                                             smallNotchHeight) * PixelHeight;
                var start = new Vector2(x, PixelHeight);
                var end = new Vector2(x, PixelHeight - height);
                handle.DrawLine(start, end, notchColor);
            }
        }
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        // Some features (Gap, MinEntryWidth) depend on the Control's Width. Once the Width is set and before the
        // first draw, make sure that the entries get an opportunity to update their width properly.
        if (!_hasHadNonZeroWidth && finalSize.X > 0)
            UpdateEntries(finalSize.X, 0);

        // Polonium: labels on top get the width of their text, from their segment or right after the label before.
        var nextFree = 0f;
        foreach (var (entry, xMin, xMax) in EntryRanges(finalSize.X))
        {
            if (!_labelsOnTop)
            {
                // Polonium: a label that would be cut is made transparent instead; the tooltip still names the segment.
                // Not hidden: a hidden control measures as zero, so it would "fit" again next frame and blink.
                if (HideClippedLabels)
                {
                    // Setting it always would re-measure the label on every arrange.
                    if (entry.Label.ClipText)
                        entry.Label.ClipText = false;
                    entry.Label.Measure(new Vector2(float.PositiveInfinity, finalSize.Y));
                    var shown = entry.Label.DesiredSize.X <= xMax - xMin ? Color.White : Color.Transparent;
                    if (entry.Label.Modulate != shown)
                        entry.Label.Modulate = shown;
                }

                entry.Label.Arrange(new((int)xMin, 0, (int)xMax, (int)finalSize.Y));
                continue;
            }

            entry.Label.Measure(new Vector2(float.PositiveInfinity, finalSize.Y));
            var start = MathF.Max(xMin, nextFree > 0 ? nextFree + LabelGap : 0);
            var end = start + entry.Label.DesiredSize.X;
            entry.Label.Arrange(new((int)start, 0, (int)MathF.Ceiling(end), (int)finalSize.Y));
            nextFree = end;
        }

        return finalSize;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        UpdateEntries(Width, args.DeltaSeconds);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        HideTooltip();
    }


    /// <summary>
    /// Makes the current entries disappear (usually in an animated way, so it takes a while for them to be gone).
    /// An entry won't disappear if a SetEntry call (after the Clear) gives the entry a non-zero `amount`.
    /// </summary>
    public void Clear()
    {
        foreach (var entry in _entries)
        {
            entry.TargetAmount = 0;
            entry.Placed = false;
        }

        _nextUpdateableEntry = 0;
    }

    /// <summary>
    /// Polonium: finds this uid's entry wherever it is, moves it to the next place in the call order and gives every
    /// entry that moved an offset back to where it was drawn, so they all slide into their new places.
    /// </summary>
    private bool TryMoveEntryHere(string uid, out int index)
    {
        index = _entries.FindIndex(entry => entry.Uid == uid && !entry.Placed);
        if (index < 0)
            return false;

        if (index != _nextUpdateableEntry)
        {
            var before = DrawnStarts();
            var entry = _entries[index];
            _entries.RemoveAt(index);
            _entries.Insert(_nextUpdateableEntry, entry);
            index = _nextUpdateableEntry;

            var after = BaseStarts();
            foreach (var other in _entries)
            {
                other.Offset = before[other] - after[other];
            }
        }

        _entries[index].Placed = true;
        return true;
    }

    /// <summary>
    /// Polonium: where each entry starts in its place, as a fraction of the chart's width.
    /// </summary>
    private Dictionary<Entry, float> BaseStarts()
    {
        var starts = new Dictionary<Entry, float>();
        var gap = _entries.Count > 1 ? GetTotalGapsWidthFraction(Width) / (_entries.Count - 1) : 0;
        var start = 0f;
        foreach (var entry in _entries)
        {
            starts[entry] = start;
            start += entry.WidthFraction + gap;
        }

        return starts;
    }

    /// <summary>
    /// Polonium: where each entry is drawn right now, its place plus whatever slide it has left.
    /// </summary>
    private Dictionary<Entry, float> DrawnStarts()
    {
        var starts = BaseStarts();
        foreach (var entry in _entries)
        {
            starts[entry] += entry.Offset;
        }

        return starts;
    }

    /// <summary>
    /// Either adds a new entry to the chart if the UID doesn't appear yet, or updates the amount of an existing entry.
    /// Entries are drawn in the order of the SetEntry calls, with the first entry on the left and the last on the
    /// right.
    /// </summary>
    public void SetEntry(
        string uid,
        float amount,
        Color color,
        string? text = null,
        Color? textColor = null,
        string? tooltip = null)
    {
        // If we can find an old entry we're allowed to update, update that one.
        if (SlideOnReorder ? TryMoveEntryHere(uid, out var index) : TryFindUpdateableEntry(uid, out index))
        {
            _entries[index].TargetAmount = amount;
            _entries[index].Color = color;
            _entries[index].Tooltip = tooltip;
            _entries[index].Label.Text = text;
            _nextUpdateableEntry = index + 1;
            return;
        }

        // Otherwise create a new entry.
        if (amount <= 0)
            return;

        // If no text color is provided, use either white or black depending on how dark the background is.
        // Polonium: a label on top also runs over other segments and the empty bar, so it is always white there.
        textColor ??= _labelsOnTop || color.R + color.G + color.B < 1.5f ? Color.White : Color.Black;

        var childLabel = new Label
        {
            Text = text,
            ClipText = !_labelsOnTop,
            // Polonium: centered in the bar's height instead of stuck to the top.
            VAlign = Label.VAlignMode.Center,
            FontColorOverride = textColor,
            Margin = new Thickness(4, 0, 0, 0)
        };
        AddChild(childLabel);

        _entries.Insert(
            _nextUpdateableEntry,
            new Entry(uid, childLabel)
            {
                Placed = true,
                ShownColor = color,
                WidthFraction = 0,
                TargetAmount = amount,
                Tooltip = tooltip,
                Color = color,
                Label = childLabel
            }
        );

        _nextUpdateableEntry += 1;
    }


    private Control? SupplyTooltip(Control sender)
    {
        var globalMousePos = UserInterfaceManager.MousePositionScaled.Position;
        var mousePos = globalMousePos - GlobalPosition;

        if (!TryFindEntry(mousePos.X, Width, out var entry) || entry.Tooltip == null)
            return null;

        var msg = new FormattedMessage();
        msg.AddText(entry.Tooltip);

        var tooltip = new Tooltip();
        tooltip.SetMessage(msg);
        return tooltip;
    }

    private bool TryFindUpdateableEntry(string uid, out int index)
    {
        for (int i = _nextUpdateableEntry; i < _entries.Count; i++)
        {
            if (_entries[i].Uid == uid)
            {
                index = i;
                return true;
            }
        }

        index = -1;
        return false;
    }

    private IEnumerable<(Entry, float xMin, float xMax)> EntryRanges(float chartWidth)
    {
        var xStart = 0f;
        var gapWidth = (_entries.Count > 1
            ? GetTotalGapsWidthFraction(chartWidth) * chartWidth / (_entries.Count - 1)
            : 0);

        foreach (var entry in _entries)
        {
            var entryWidth = entry.WidthFraction * chartWidth;
            var xEnd = MathF.Min(xStart + entryWidth, chartWidth);

            // Polonium: a sliding entry is drawn off its place by what is left of its slide.
            var offset = entry.Offset * chartWidth;
            yield return (entry, Math.Clamp(xStart + offset, 0, chartWidth), Math.Clamp(xEnd + offset, 0, chartWidth));

            xStart = MathF.Min(xEnd + gapWidth, chartWidth);
        }
    }

    private bool TryFindEntry(float x, float chartWidth, [NotNullWhen(true)] out Entry? entry)
    {
        foreach (var (currentEntry, xMin, xMax) in EntryRanges(chartWidth))
        {
            if (x < xMin)
                break;
            if (x > xMax)
                continue;

            entry = currentEntry;
            return true;
        }

        entry = null;
        return false;
    }

    private void UpdateEntries(float chartWidth, float deltaSeconds)
    {
        // Tween the amounts to their target amounts.
        const float tweenInverseHalfLife = 8;  // Half life of tween is 1/n
        var hasChanged = false;
        var animated = _animated;

        // This next series of calculations is somewhat complicated. We're trying to calculate the desired width for
        // each entry, but there's a couple of complicating factors: Gap, MinEntryWidth and constant/flexible capacities
        //
        // The calculations are done in width fractions (i.e. a percentage of this Control's width) because it works
        // better for animations, especially when swapping out a 50u beaker for a 100u beaker.
        //
        // In the calculation the width of an entry is split into two parts:
        // The minWidthFraction is based on MinEntryWidth and is the same for all entries,
        // and the remainder of the space is divided into flexibleWidthFractions (or left as empty space).
        // Normally the great majority of space is taken up by flexibleWidthFractions.

        // The amount of entries we want to have after animations are complete (some entries may disappear).
        var targetEntryCount = 0;
        foreach (var entry in _entries)
        {
            if (entry.TargetAmount > 0)
                targetEntryCount += 1;
        }

        var isCapacityFlexible = (Capacity <= 0);
        var totalAmount = GetCapacity();

        // The width available for entries.
        var totalEntriesWidthFraction = 1 - GetTotalGapsWidthFraction(chartWidth);
        // The min width for all entries can't be wider than the available space per entry.
        var spacePerEntry = totalEntriesWidthFraction / MathF.Max(1, targetEntryCount);
        // Minimum width of an entry.
        var minWidthFraction = MathF.Min(spacePerEntry, _minEntryWidth / MathF.Max(1, chartWidth));
        // The amount of units that `minWidthFraction` covers.
        var minWidthAmount = minWidthFraction * totalAmount;

        // The width that can still be divided among flexible width fractions.
        var remainingWidthFraction = totalEntriesWidthFraction - minWidthFraction * targetEntryCount;
        // The amount of units that can be divided among flexible width fractions.
        var remainingAmount =
            (isCapacityFlexible
            ? _entries.Aggregate(0f, (sum, entry) => sum + MathF.Max(0, entry.TargetAmount - minWidthAmount))
            : totalAmount - minWidthAmount);

        foreach (var entry in _entries)
        {
            // Polonium: slide a reordered entry into its place.
            if (entry.Offset != 0)
            {
                hasChanged = true;
                entry.Offset = animated && _hasHadNonZeroWidth
                    ? MathHelper.Lerp(entry.Offset, 0, MathHelper.Clamp01(tweenInverseHalfLife * deltaSeconds))
                    : 0;
                if (MathF.Abs(entry.Offset) < 0.0001f)
                    entry.Offset = 0;
            }

            // Polonium: fade to a new color instead of switching at once.
            if (entry.ShownColor != entry.Color)
            {
                hasChanged = true;
                var t = animated && _hasHadNonZeroWidth ? MathHelper.Clamp01(tweenInverseHalfLife * deltaSeconds) : 1f;
                entry.ShownColor = Color.InterpolateBetween(entry.ShownColor, entry.Color, t);
                if (MathF.Abs(entry.ShownColor.R - entry.Color.R) + MathF.Abs(entry.ShownColor.G - entry.Color.G)
                    + MathF.Abs(entry.ShownColor.B - entry.Color.B) + MathF.Abs(entry.ShownColor.A - entry.Color.A) < 0.004f)
                    entry.ShownColor = entry.Color;
            }

            // Calculate the target width for this entry.
            var targetWidthFraction = 0f;

            if (entry.TargetAmount != 0)
            {
                var flexibleAmount = MathF.Max(0, entry.TargetAmount - minWidthAmount);
                var flexibleWidthFraction =
                    (remainingAmount != 0
                    ? (flexibleAmount / remainingAmount) * remainingWidthFraction
                    : 0);

                targetWidthFraction = minWidthFraction + flexibleWidthFraction;
            }

            if (entry.WidthFraction == targetWidthFraction)
                continue;

            // Move the entry's width towards its target width.
            hasChanged = true;

            if (animated && _hasHadNonZeroWidth)
            {
                // Tween with lerp abuse interpolation
                entry.WidthFraction = MathHelper.Lerp(
                    entry.WidthFraction,
                    targetWidthFraction,
                    MathHelper.Clamp01(tweenInverseHalfLife * deltaSeconds)
                );

                if (MathF.Abs(entry.WidthFraction - targetWidthFraction) < 0.0001f)
                    entry.WidthFraction = targetWidthFraction;
            }
            else
            {
                // Don't animate, just snap straight to the target.
                entry.WidthFraction = targetWidthFraction;
            }
        }

        _hasHadNonZeroWidth |= (chartWidth > 0);

        if (!hasChanged)
            return;

        InvalidateArrange();

        // Remove old entries whose animations have finished.
        foreach (var entry in _entries)
        {
            if (entry.WidthFraction == 0 && entry.TargetAmount == 0)
                RemoveChild(entry.Label);
        }

        _entries.RemoveAll(entry => entry.WidthFraction == 0 && entry.TargetAmount == 0);
    }

    private float GetCapacity()
    {
        // Constant capacity.
        if (Capacity > 0)
            return Capacity;

        // Flexible capacity.
        var amountSum = _entries.Aggregate(0f, (sum, entry) => sum + entry.TargetAmount);
        return MathF.Max(0.001f, amountSum);  // Make sure it's not zero (it's often used as denominator)
    }

    private float GetTotalGapsWidthFraction(float chartWidth)
    {
        if (_showRuler)
            return 0;  // ShowRuler is incompatible with Gap.

        var gapsWidth = (_entries.Count - 1) * _gap;
        var gapsFraction = gapsWidth / MathF.Max(chartWidth, 1f);

        // We limit the gaps to cover max 25% of the chart, to make sure there's always space for entries no matter
        // how many entries you add.
        return MathF.Min(gapsFraction, 0.25f);
    }
}
