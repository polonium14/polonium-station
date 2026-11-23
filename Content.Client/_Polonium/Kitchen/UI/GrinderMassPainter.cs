using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Polonium.Kitchen.UI;

public enum GrinderMassKind : byte
{
    Paste,
    Juice,
    Powder,
}

/// <summary>
/// Paints the mass inside the jug pixel by pixel: a thick paste, a thin juice with foam or a heap of dry crumbs.
/// </summary>
public sealed class GrinderMassPainter
{
    public const int Width = GrinderArt.JugWidth;
    public const int Height = GrinderArt.JugHeight;

    private const int Floor = GrinderArt.JugFloor;
    private const int Center = GrinderArt.JugCenter;
    private const float CrumbLightening = 0.14f;

    private static readonly int[] PasteRipple = [0, -1, -2, -1, 0, 1, 0, -1];
    private static readonly Vector3 White = new(255f);

    private readonly float[][] _keep = new float[2][];
    private readonly float[][] _add = new float[2][];
    private readonly List<GrinderGrain> _grains = new();
    private float _grainTotal;
    private Vector3 _color;

    public readonly Rgba32[] Pixels = new Rgba32[Width * Height];

    /// <summary>
    /// The top row of the mass in every column of the jug. Negative where the column holds none.
    /// </summary>
    public readonly int[] Tops = new int[Width];

    public GrinderMassPainter()
    {
        for (var kind = 0; kind < 2; kind++)
        {
            _keep[kind] = new float[Width * Height];
            _add[kind] = new float[Width * Height];
            Array.Fill(_keep[kind], 1f);
        }

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var a = (x * 7 + y * 13) % 11;
                var b = (x * 5 + y * 3) % 7;
                var c = (x * 3 + y * 7) % 9;
                if (a == 0)
                {
                    Mark(GrinderMassKind.Paste, x, y, 0f, 0.2f);
                    Mark(GrinderMassKind.Paste, x + 1, y, 0f, 0.2f);
                }
                else if (a == 5 && b < 2)
                {
                    Mark(GrinderMassKind.Paste, x, y, 255f, 0.22f);
                }

                if (a != 3 || c != 1)
                    continue;

                for (var i = 0; i < 3; i++)
                {
                    Mark(GrinderMassKind.Juice, x, y + i, 255f, 0.2f);
                }
            }
        }
    }

    private void Mark(GrinderMassKind kind, int x, int y, float value, float alpha)
    {
        if (x >= Width || y >= Height)
            return;

        var index = y * Width + x;
        _add[(int) kind][index] = value * alpha + _add[(int) kind][index] * (1f - alpha);
        _keep[(int) kind][index] *= 1f - alpha;
    }

    /// <summary>
    /// Sets what the mass is made of. Every crumb of dust takes the colour of one reagent, picked by their shares.
    /// </summary>
    public void SetMaterial(Color color, IReadOnlyList<GrinderGrain> grains)
    {
        _color = GrinderArt.Rgb(color);
        _grains.Clear();
        _grainTotal = 0f;
        foreach (var grain in grains)
        {
            if (grain.Amount <= 0f)
                continue;

            _grains.Add(grain);
            _grainTotal += grain.Amount;
        }
    }

    /// <summary>
    /// The colour of one crumb of dust.
    /// </summary>
    /// <param name="pick">A number from 0 to 1 that chooses the reagent.</param>
    public Vector3 CrumbColor(double pick)
    {
        if (_grains.Count == 0)
            return Vector3.Lerp(_color, White, CrumbLightening);

        var left = (float) pick * _grainTotal;
        var index = 0;
        while (index < _grains.Count - 1 && left > _grains[index].Amount)
        {
            left -= _grains[index].Amount;
            index++;
        }

        return Vector3.Lerp(GrinderArt.Rgb(_grains[index].Color), White, CrumbLightening);
    }

    /// <summary>
    /// A steady pseudo-random number from 0 to 1 for a pixel, so that crumbs stay where they are between frames.
    /// </summary>
    public static double Hash(int x, int y, int seed)
    {
        unchecked
        {
            var n = x * 374761393 + y * 668265263 + seed * 362437;
            n = (int) ((uint) n ^ ((uint) n >> 13)) * 1274126177;
            return ((uint) n ^ ((uint) n >> 16)) / 4294967296d;
        }
    }

    /// <summary>
    /// Paints the mass at the given height.
    /// </summary>
    /// <param name="height">Height of the mass over the floor of the jug in art pixels.</param>
    /// <param name="spinning">Whether the blades stir the mass.</param>
    /// <param name="clock">Seconds since the window opened.</param>
    /// <param name="shift">How far the texture of the mass has travelled sideways, in art pixels.</param>
    public void Paint(GrinderMassKind kind, int height, bool spinning, float clock, float shift)
    {
        Array.Clear(Pixels);
        if (height <= 0)
        {
            Array.Fill(Tops, -1);
            return;
        }

        if (kind == GrinderMassKind.Powder)
            PaintPowder(height, spinning, clock);
        else
            PaintLiquid(kind, height, spinning, clock, shift);
    }

    private void PaintLiquid(GrinderMassKind kind, int height, bool spinning, float clock, float shift)
    {
        var step = spinning ? (int) MathF.Floor(clock * 8f) : 0;
        var offset = (int) MathF.Floor(shift) % Width;
        var keep = _keep[(int) kind];
        var add = _add[(int) kind];
        var juice = kind == GrinderMassKind.Juice;
        var crest = Pixel(Vector3.Lerp(_color, White, juice ? 0.45f : 0.25f));
        var under = Pixel(Vector3.Lerp(_color, White, 0.18f));
        for (var x = 0; x < Width; x++)
        {
            var top = Math.Max(0, Surface(kind, x, height, spinning, step));
            var bottom = GrinderArt.ColumnFloor(x);
            if (top >= bottom)
            {
                Tops[x] = -1;
                continue;
            }

            Tops[x] = top;
            var grain = (x + offset) % Width;
            for (var y = top; y < bottom; y++)
            {
                var index = y * Width + grain;
                Pixels[y * Width + x] = Pixel(_color * keep[index] + new Vector3(add[index]));
            }

            Pixels[top * Width + x] = crest;
            for (var y = top + 1; juice && y <= top + 2 && y < bottom; y++)
            {
                Pixels[y * Width + x] = under;
            }
        }
    }

    private static int Surface(GrinderMassKind kind, int x, int height, bool spinning, int step)
    {
        var u = (x - Center) / (float) Center;
        var spin = spinning ? 1f : 0f;
        float y = Floor - height;
        if (kind == GrinderMassKind.Juice)
            y += spin * MathF.Min(7f, height * 0.3f) * (1f - u * u) + ((x + step * 3) % 23 < 4 ? -spin : 0f);
        else
            y += spin * MathF.Min(5f, height * 0.25f) * (1f - u * u) + PasteRipple[(x + step * 5) / 6 % 8];

        return Math.Min(Floor, GrinderArt.Round(y));
    }

    /// <summary>
    /// A lumpy heap of crumbs two pixels across with dark gaps. The blades drag the lower layers along in steps.
    /// </summary>
    private void PaintPowder(int height, bool spinning, float clock)
    {
        var step = spinning ? (int) MathF.Floor(clock * 10f) : 0;
        var slow = step >> 2;
        for (var x = 0; x < Width; x++)
        {
            var u = (x - Center) / (float) Center;
            var lump = x / 7;
            var along = x % 7 / 7f;
            var near = (float) Hash(lump, 7, slow);
            var far = (float) Hash(lump + 1, 7, slow);
            var level = Floor - height + (spinning
                ? MathF.Min(11f, height * 0.45f) * (1f - MathF.Abs(u))
                : MathF.Min(7f, height * 0.35f) * (MathF.Abs(u) - 0.45f));
            level += (near + (far - near) * along - 0.5f) * 7f + ((float) Hash(x, 3, step >> 1) - 0.5f) * 2.4f;

            var top = Math.Clamp(GrinderArt.Round(level), 0, Floor);
            var bottom = GrinderArt.ColumnFloor(x);
            Tops[x] = top < bottom ? top : -1;
            for (var y = top; y < bottom; y++)
            {
                var depth = y - top;
                var drag = spinning ? step * (depth > 8 ? 2 : 1) : 0;
                var crumbX = (x + drag + ((y >> 1) & 1)) >> 1;
                var crumbY = y >> 1;
                var color = CrumbColor(Hash(crumbX, crumbY, 11));
                var tone = Hash(crumbX, crumbY, 23);
                var shade = tone < 0.16 ? 0.42f : tone < 0.5 ? 0.8f : tone < 0.86 ? 1f : 1.25f;
                var fleck = Hash(x + drag, y, 31);
                if (depth == 0)
                    shade = MathF.Max(shade, 0.95f) * 1.2f;
                else if (fleck > 0.94)
                    shade *= 1.35f;
                else if (fleck < 0.05)
                    shade *= 0.55f;

                Pixels[y * Width + x] = Pixel(color * shade);
            }
        }

        // Loose crumbs lying on the crust.
        for (var x = 3; x < Width - 3; x++)
        {
            if (Tops[x] > 2 && Hash(x, 5, step >> 1) > 0.84)
                Pixels[(Tops[x] - 1) * Width + x] = Pixel(CrumbColor(Hash(x, 9, 0)));
        }
    }

    /// <summary>
    /// Paints a small square into the mass. Pixels outside the glass are left out.
    /// </summary>
    public void Square(int x, int y, int width, int height, Vector3 color)
    {
        var pixel = Pixel(color);
        for (var row = Math.Max(y, 0); row < y + height && row < Floor; row++)
        {
            var inset = GrinderArt.Inset(row);
            for (var column = Math.Max(x, inset); column < x + width && column < Width - inset; column++)
            {
                Pixels[row * Width + column] = pixel;
            }
        }
    }

    private static Rgba32 Pixel(Vector3 color)
    {
        return new Rgba32(GrinderArt.Channel(color.X), GrinderArt.Channel(color.Y), GrinderArt.Channel(color.Z), 255);
    }
}
