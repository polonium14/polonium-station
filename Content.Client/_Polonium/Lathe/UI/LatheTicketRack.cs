using System.Numerics;
using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Shared.Lathe;
using Content.Shared.Research.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Lathe.UI;

/// <summary>
/// The build queue as a rack of paper tickets, one per batch. A new ticket feeds in, a finished one is
/// stamped and pulled into the machine, a cancelled one is torn off.
/// </summary>
public sealed partial class LatheTicketRack : Control
{
    [Dependency] private IPrototypeManager _proto = default!;

    public const int ArtWidth = 120;
    public const int ArtHeight = 126;

    private const int TicketHeight = 35;
    private const int Gap = 3;
    private const int PaperWidth = 102;
    private const int KeyLeft = 104;
    private const int KeyWidth = 14;
    private const int KeyHeight = 11;
    private const int KeyPitch = 12;
    private const int BarLeft = 118;
    private const int MinThumb = 6;
    private const int ScrollStep = 19;
    private const int IconCenterX = 16;
    private const int IconBottom = 25;
    private const int TextLeft = 26;
    private const int TextWidth = 74;
    private const int LabelTop = 3;
    private const int NameTop = 12;
    private const int NameHeight = 10;
    private const int CountTop = 22;
    private const int PunchTop = 25;
    private const int PunchSize = 3;
    private const int PunchPitch = 4;
    private const int MaxPunches = 12;
    private const int StampRight = 98;
    private const int StampTop = 5;
    private const int StampHeight = 11;
    private const int SlideDistance = 124;
    private const int TearX = -4;
    private const int TearY = 22;

    private const float EnterTime = 0.3f;
    private const int EnterSteps = 7;
    private const float StampTime = 0.62f;
    private const int StampSteps = 7;
    private const float StampHold = 0.35f;
    private const float StampSlideEnd = 0.8f;
    private const float TearTime = 0.36f;
    private const int TearSteps = 5;
    private const float TearFadeEnd = 0.65f;

    private enum Phase : byte
    {
        Entering,
        Live,
        Held,
        Stamped,
        Torn,
    }

    private sealed class Ticket
    {
        public LatheRecipePrototype Recipe = default!;
        public string Name = string.Empty;
        public string Label = string.Empty;
        public string Count = string.Empty;
        public int Number = -1;
        public int Printed;
        public int Total;
        public Phase Phase;
        public float Time;
        public bool Doomed;
        public bool Matched;

        /// <summary>
        /// When the item this ticket last counted began to print.
        /// </summary>
        public TimeSpan? Counted;
        public PixelKey[] Keys = default!;

        public int Top;
        public int Height;
        public int ShiftX;
        public int ShiftY;
        public float Alpha = 1f;

        public bool InQueue => Phase is Phase.Entering or Phase.Live;
    }

    private readonly LatheConsoleModel _model;
    private readonly LatheIconCache _icons;
    private readonly PixelFont _font;
    private readonly LatheScreenFont _screenFont;
    private readonly Texture _paper;
    private readonly Texture _keyDark;
    private readonly Texture _keyRed;
    private readonly Texture _iconTexture;
    private readonly PixelKeySoundUIController _keySounds;
    private readonly List<Ticket> _tickets = new();
    private readonly List<Ticket> _queued = new();
    private readonly string _empty;
    private readonly string _stamp;
    private readonly int _stampWidth;

    private Ticket? _held;
    private LatheRecipeBatch[]? _syncedQueue;
    private LatheRecipePrototype? _syncedPrinting;
    private TimeSpan _syncedStart;
    private bool _synced;
    private bool _following;
    private int _scroll;
    private int _content;
    private int _arranged;

    public event Action<int, int>? OnMove;
    public event Action<int>? OnDelete;

    /// <summary>
    /// Which item of how many is being printed, or null when nothing is.
    /// </summary>
    public (int Index, int Total)? Batch;

    public LatheTicketRack(LatheConsoleModel model, LatheIconCache icons, PixelFont font, LatheScreenFont screenFont, LatheTextures textures)
    {
        IoCManager.InjectDependencies(this);

        _model = model;
        _icons = icons;
        _font = font;
        _screenFont = screenFont;
        _paper = textures.Ticket;
        _keyDark = textures.KeyDark;
        _keyRed = textures.KeyRed;
        _iconTexture = textures.Icons;
        _keySounds = UserInterfaceManager.GetUIController<PixelKeySoundUIController>();
        _empty = Loc.GetString("polonium-lathe-idle").ToUpperInvariant();
        _stamp = Loc.GetString("polonium-lathe-ticket-stamp").ToUpperInvariant();
        _stampWidth = _font.Measure(_stamp) - 1 + 6;

        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
        _model.ItemEnded += OnItemEnded;
    }

    private int MaxScroll => Math.Max(0, _content - ArtHeight);

    /// <summary>
    /// The whole queue is being cancelled: every ticket gets torn off, none is stamped.
    /// </summary>
    public void DoomAll()
    {
        foreach (var ticket in _queued)
        {
            ticket.Doomed = true;
        }
    }

    private void OnItemEnded(bool produced)
    {
        if (_held is not { } held)
            return;

        _held = null;
        Leave(held, produced);
    }

    private void Leave(Ticket ticket, bool stamped)
    {
        ticket.Phase = stamped ? Phase.Stamped : Phase.Torn;
        ticket.Time = 0f;
        foreach (var key in ticket.Keys)
        {
            key.Disabled = true;
        }
    }

    private Ticket Create(LatheRecipePrototype recipe, int printed, int total, Phase phase)
    {
        var ticket = new Ticket
        {
            Recipe = recipe,
            Name = _model.RecipeName(recipe),
            Printed = printed,
            Total = total,
            Phase = phase,
            Time = _synced && !_model.ReducedMotion ? 0f : EnterTime,
            Matched = true,
        };

        ticket.Keys =
        [
            AddKey(ticket, _keyDark, LatheKeyIcon.Up, LathePalette.KeyTextDark, "lathe-menu-move-up-tooltip", () => OnMove?.Invoke(_queued.IndexOf(ticket), -1)),
            AddKey(ticket, _keyDark, LatheKeyIcon.Down, LathePalette.KeyTextDark, "lathe-menu-move-down-tooltip", () => OnMove?.Invoke(_queued.IndexOf(ticket), 1)),
            AddKey(ticket, _keyRed, LatheKeyIcon.Cross, Color.White, "lathe-menu-delete-item-tooltip", () =>
            {
                ticket.Doomed = true;
                OnDelete?.Invoke(_queued.IndexOf(ticket));
            }),
        ];

        SetCount(ticket);
        return ticket;
    }

    private PixelKey AddKey(Ticket ticket, Texture skin, LatheKeyIcon icon, Color color, string tooltip, Action pressed)
    {
        var key = new PixelKey
        {
            Skin = skin,
            ToolTip = Loc.GetString(tooltip),
        };
        key.AddChild(new PixelKeyIcon { Texture = _iconTexture, Region = LathePalette.Icon(icon), Color = color });
        key.OnPressed += _ =>
        {
            if (ticket.InQueue)
                pressed();
        };
        _keySounds.Attach(key);
        AddChild(key);
        return key;
    }

    private static void SetCount(Ticket ticket)
    {
        ticket.Count = $"{ticket.Printed}/{ticket.Total}";
    }

    /// <summary>
    /// Brings the tickets in line with the lathe's queue. The queue carries no batch ids, so tickets are matched
    /// by recipe, counts and order.
    /// </summary>
    private void Sync()
    {
        var queue = _model.Queue;
        if (ReferenceEquals(queue, _syncedQueue) && _model.Printing == _syncedPrinting && _model.PrintStart == _syncedStart)
            return;

        _syncedQueue = queue;
        _syncedPrinting = _model.Printing;
        _syncedStart = _model.PrintStart;
        foreach (var ticket in _queued)
        {
            ticket.Matched = false;
        }

        var matched = new Ticket?[queue.Length];
        var cursor = 0;
        for (var i = 0; i < queue.Length; i++)
        {
            for (var j = cursor; j < _queued.Count; j++)
            {
                if (!Fits(_queued[j], queue[i], true))
                    continue;

                matched[i] = _queued[j];
                _queued[j].Matched = true;
                cursor = j + 1;
                break;
            }
        }

        // First the batches that only moved, then those whose counts changed in a way printing alone would not explain.
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < queue.Length; i++)
            {
                if (matched[i] != null)
                    continue;

                foreach (var ticket in _queued)
                {
                    if (!Fits(ticket, queue[i], pass == 0))
                        continue;

                    matched[i] = ticket;
                    ticket.Matched = true;
                    break;
                }
            }
        }

        for (var i = 0; i < _queued.Count; i++)
        {
            var ticket = _queued[i];
            if (ticket.Matched)
            {
                ticket.Doomed = false;
                continue;
            }

            // A batch leaves the queue when its last item starts. Its ticket waits in the holder until that item is out.
            // A batch that vanishes while the item it last counted is still printing was deleted instead.
            var last = i == 0 && !ticket.Doomed && _held == null && _model.Printing == ticket.Recipe && ticket.Counted != _model.PrintStart;
            if (!last)
            {
                Leave(ticket, false);
                continue;
            }

            ticket.Phase = Phase.Held;
            ticket.Printed = ticket.Total;
            SetCount(ticket);
            foreach (var key in ticket.Keys)
            {
                key.Disabled = true;
            }

            _held = ticket;
        }

        var grew = false;
        var fresh = new List<Ticket>(queue.Length);
        for (var i = 0; i < queue.Length; i++)
        {
            var batch = queue[i];
            var ticket = matched[i];
            if (ticket == null)
            {
                if (!_proto.Resolve(batch.Recipe, out var recipe))
                    continue;

                ticket = Create(recipe, batch.ItemsPrinted, batch.ItemsRequested, Phase.Entering);
                if (batch.ItemsPrinted > 0)
                    ticket.Counted = _model.PrintStart;

                grew = true;
            }
            else if (ticket.Printed != batch.ItemsPrinted || ticket.Total != batch.ItemsRequested)
            {
                if (batch.ItemsPrinted > ticket.Printed)
                    ticket.Counted = _model.PrintStart;

                ticket.Printed = batch.ItemsPrinted;
                ticket.Total = batch.ItemsRequested;
                SetCount(ticket);
            }

            fresh.Add(ticket);
        }

        // Tickets on their way out keep their place, the rest follow the queue's order.
        var next = 0;
        for (var i = 0; i < _tickets.Count; i++)
        {
            if (_tickets[i].InQueue && next < fresh.Count)
                _tickets[i] = fresh[next++];
        }

        for (; next < fresh.Count; next++)
        {
            _tickets.Add(fresh[next]);
        }

        _queued.Clear();
        _queued.AddRange(fresh);
        SyncHeld();

        if (grew && _synced)
            _following = true;

        _synced = true;
    }

    /// <summary>
    /// Whether the ticket can stand for the batch. A strict fit keeps the size of the order and never counts backwards,
    /// and leaves out a ticket whose delete key was pressed.
    /// </summary>
    private static bool Fits(Ticket ticket, LatheRecipeBatch batch, bool strict)
    {
        if (ticket.Matched || ticket.Recipe.ID != batch.Recipe.Id)
            return false;

        return !strict || !ticket.Doomed && ticket.Total == batch.ItemsRequested && ticket.Printed <= batch.ItemsPrinted;
    }

    /// <summary>
    /// An order for a single item on an idle lathe starts at once and never shows up in the queue. It still gets its ticket.
    /// </summary>
    private void SyncHeld()
    {
        Batch = null;
        if (_model.Printing is not { } printing)
        {
            if (_held is { } idle)
            {
                _held = null;
                Leave(idle, false);
            }

            return;
        }

        // A held ticket is the item in work, even when a moved batch of the same recipe has printed some already.
        if (_held is { } current && current.Recipe == printing)
        {
            Batch = (current.Total, current.Total);
            return;
        }

        foreach (var ticket in _queued)
        {
            if (ticket.Recipe != printing || ticket.Printed <= 0)
                continue;

            Batch = (ticket.Printed, ticket.Total);
            return;
        }

        if (_held == null)
        {
            _held = Create(printing, 1, 1, Phase.Held);
            foreach (var key in _held.Keys)
            {
                key.Disabled = true;
            }

            _tickets.Insert(0, _held);
        }

        Batch = (_held.Total, _held.Total);
    }

    private void Number()
    {
        var offset = _held == null ? 0 : 1;
        foreach (var ticket in _tickets)
        {
            if (ticket.Phase == Phase.Stamped)
                offset++;
        }

        if (_held is { Number: < 0 } held)
            SetNumber(held, 1);

        for (var i = 0; i < _queued.Count; i++)
        {
            SetNumber(_queued[i], i + 1 + offset);
        }
    }

    private static void SetNumber(Ticket ticket, int number)
    {
        if (ticket.Number == number)
            return;

        ticket.Number = number;
        ticket.Label = Loc.GetString("polonium-lathe-ticket-number", ("number", number.ToString("00"))).ToUpperInvariant();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        for (var i = _tickets.Count - 1; i >= 0; i--)
        {
            var ticket = _tickets[i];
            ticket.Time += args.DeltaSeconds;
            var gone = ticket.Phase switch
            {
                Phase.Stamped => ticket.Time >= StampTime || _model.ReducedMotion,
                Phase.Torn => ticket.Time >= TearTime || _model.ReducedMotion,
                _ => false,
            };

            if (!gone)
                continue;

            foreach (var key in ticket.Keys)
            {
                key.Orphan();
            }

            _tickets.RemoveAt(i);
        }

        Sync();
        Number();
        Layout();

        // A new ticket grows as it feeds in, so the view keeps up with it until it is whole or the operator scrolls.
        if (_following)
        {
            _scroll = MaxScroll;
            _following = _tickets.Exists(ticket => ticket.Phase == Phase.Entering);
        }

        _scroll = Math.Clamp(_scroll, 0, MaxScroll);

        // Tickets that swap places keep the same geometry between them, so each one counts by itself.
        var arranged = _scroll;
        foreach (var ticket in _tickets)
        {
            arranged = HashCode.Combine(arranged, ticket, ticket.Top, ticket.Height, ticket.ShiftX, ticket.ShiftY, ticket.Alpha);
        }

        if (arranged == _arranged)
            return;

        _arranged = arranged;
        InvalidateArrange();
    }

    private static float Stepped(float time, float length, int steps)
    {
        return MathF.Min(1f, MathF.Floor(time / length * steps) / steps);
    }

    private void Layout()
    {
        var y = 0;
        foreach (var ticket in _tickets)
        {
            ticket.Height = TicketHeight;
            ticket.ShiftX = 0;
            ticket.ShiftY = 0;
            ticket.Alpha = 1f;

            switch (ticket.Phase)
            {
                case Phase.Entering:
                    var entered = Stepped(ticket.Time, EnterTime, EnterSteps);
                    ticket.Height = (int) MathF.Round(TicketHeight * entered);
                    if (entered >= 1f)
                        ticket.Phase = Phase.Live;
                    break;
                case Phase.Held when ticket.Time < EnterTime:
                    ticket.Height = (int) MathF.Round(TicketHeight * Stepped(ticket.Time, EnterTime, EnterSteps));
                    break;
                case Phase.Stamped:
                    var stamped = Stepped(ticket.Time, StampTime, StampSteps);
                    ticket.ShiftX = (int) MathF.Round(SlideDistance * Math.Clamp((stamped - StampHold) / (StampSlideEnd - StampHold), 0f, 1f));
                    ticket.Height = (int) MathF.Round(TicketHeight * (1f - Math.Clamp((stamped - StampSlideEnd) / (1f - StampSlideEnd), 0f, 1f)));
                    break;
                case Phase.Torn:
                    var torn = Stepped(ticket.Time, TearTime, TearSteps);
                    var fall = Math.Clamp(torn / TearFadeEnd, 0f, 1f);
                    ticket.ShiftX = (int) MathF.Round(TearX * fall);
                    ticket.ShiftY = (int) MathF.Round(TearY * fall);
                    ticket.Alpha = 1f - fall;
                    ticket.Height = (int) MathF.Round(TicketHeight * (1f - Math.Clamp((torn - TearFadeEnd) / (1f - TearFadeEnd), 0f, 1f)));
                    break;
            }

            ticket.Top = y;
            y += ticket.Height + (ticket.Height * Gap + TicketHeight - 1) / TicketHeight;
        }

        _content = Math.Max(0, y - Gap);
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (args.Delta.Y == 0f || MaxScroll == 0)
            return;

        _following = false;
        _scroll = Math.Clamp(_scroll - MathF.Sign(args.Delta.Y) * ScrollStep, 0, MaxScroll);
        args.Handle();
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        foreach (var ticket in _tickets)
        {
            var revealed = ticket.Phase is Phase.Entering or Phase.Held ? ticket.Height : TicketHeight;
            for (var i = 0; i < ticket.Keys.Length; i++)
            {
                var key = ticket.Keys[i];
                var top = i * KeyPitch;
                key.Visible = top + KeyHeight <= revealed && ticket.Alpha > 0f;
                key.Modulate = Color.White.WithAlpha(ticket.Alpha);
                key.Arrange(LatheLayout.Box(UIScale, KeyLeft + ticket.ShiftX, ticket.Top - _scroll + ticket.ShiftY + top, KeyWidth, KeyHeight));
            }
        }

        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var canvas = new PixelCanvas(handle, UIScale);
        if (_tickets.Count == 0)
        {
            canvas.Text(_font, 1, 3, _empty, LathePalette.Faint);
            return;
        }

        foreach (var ticket in _tickets)
        {
            if (ticket.Alpha > 0f && ticket.Top - _scroll < ArtHeight && ticket.Top - _scroll + TicketHeight + TearY > 0)
                DrawTicket(handle, canvas, ticket);
        }

        if (MaxScroll == 0)
            return;

        var thumb = Math.Max(MinThumb, ArtHeight * ArtHeight / _content);
        canvas.Rect(BarLeft, (ArtHeight - thumb) * _scroll / MaxScroll, ArtWidth - BarLeft, thumb, LathePalette.Thumb);
    }

    private void DrawTicket(DrawingHandleScreen handle, PixelCanvas canvas, Ticket ticket)
    {
        var x = ticket.ShiftX;
        var y = ticket.Top - _scroll + ticket.ShiftY;
        var revealed = ticket.Phase is Phase.Entering or Phase.Held ? ticket.Height : TicketHeight;
        var fade = Color.White.WithAlpha(ticket.Alpha);
        canvas.Stretch(_paper, UIBox2.FromDimensions(0f, 0f, PaperWidth, revealed), x, y, PaperWidth, revealed, fade);

        if (revealed >= IconBottom && _icons.Get(ticket.Recipe).Texture is { } texture)
        {
            var size = (Vector2) texture.Size * canvas.Scale / 2f;
            var center = new Vector2((x + IconCenterX) * canvas.Scale, (y + TicketHeight / 2f) * canvas.Scale);
            handle.DrawTextureRect(texture, UIBox2.FromDimensions((center - size / 2f).Floored(), size), fade);
        }

        if (revealed >= LabelTop + 9)
            canvas.Text(_font, x + TextLeft, y + LabelTop, ticket.Label, LathePalette.InkSoft.WithAlpha(ticket.Alpha));

        if (revealed >= NameTop + NameHeight)
        {
            var screen = new LatheScreenCanvas(handle, _screenFont, UIScale);
            screen.Text(x + TextLeft, y + NameTop, NameHeight, TextWidth, ticket.Name, LathePalette.Ink.WithAlpha(ticket.Alpha));
        }

        if (revealed >= CountTop + 9)
        {
            var ink = LathePalette.Ink.WithAlpha(ticket.Alpha);
            var left = x + TextLeft;
            if (ticket.Total <= MaxPunches)
            {
                var line = LathePalette.PaperLine.WithAlpha(ticket.Alpha);
                for (var i = 0; i < ticket.Total; i++)
                {
                    if (i < ticket.Printed)
                    {
                        canvas.Rect(left, y + PunchTop, PunchSize, PunchSize, ink);
                    }
                    else
                    {
                        canvas.Rect(left, y + PunchTop, PunchSize, 1, line);
                        canvas.Rect(left, y + PunchTop + PunchSize - 1, PunchSize, 1, line);
                        canvas.Rect(left, y + PunchTop + 1, 1, 1, line);
                        canvas.Rect(left + PunchSize - 1, y + PunchTop + 1, 1, 1, line);
                    }

                    left += PunchPitch;
                }

                left += 1;
            }

            canvas.Text(_font, left, y + CountTop, ticket.Count, ink);
        }

        if (ticket.Phase != Phase.Stamped)
            return;

        var stampLeft = x + StampRight - _stampWidth;
        canvas.Rect(stampLeft, y + StampTop, _stampWidth, 1, LathePalette.Stamp);
        canvas.Rect(stampLeft, y + StampTop + StampHeight - 1, _stampWidth, 1, LathePalette.Stamp);
        canvas.Rect(stampLeft, y + StampTop + 1, 1, StampHeight - 2, LathePalette.Stamp);
        canvas.Rect(stampLeft + _stampWidth - 1, y + StampTop + 1, 1, StampHeight - 2, LathePalette.Stamp);
        canvas.Text(_font, stampLeft + 3, y + StampTop + 1, _stamp, LathePalette.Stamp);
    }
}
