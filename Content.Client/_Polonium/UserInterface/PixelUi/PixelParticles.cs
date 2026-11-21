using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.UserInterface.PixelUi;

/// <summary>
/// A layer of square particles in art pixels, for smoke, sparks and flames. Fading particles lose opacity in three steps.
/// </summary>
public sealed class PixelParticles : Control
{
    private const float FadeStart = 0.5f;
    private const float FadeEnd = 0.8f;
    private const float FreshAlpha = 0.9f;
    private const float FadingAlpha = 0.6f;
    private const float FadedAlpha = 0.3f;

    private readonly List<Particle> _particles = new();

    public PixelParticles()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    /// <summary>
    /// Adds one particle. Position and velocity are in art pixels and art pixels per second.
    /// </summary>
    /// <param name="gravity">Downward acceleration in art pixels per second squared. Negative values lift the particle.</param>
    /// <param name="drag">Share of the velocity lost per second.</param>
    public void Add(Vector2 position, Vector2 velocity, float life, int size, Color color, float gravity = 0f, float drag = 0f, bool fade = false)
    {
        _particles.Add(new Particle(position, velocity, life, size, color, gravity, drag, fade));
    }

    /// <summary>
    /// Removes every particle at once.
    /// </summary>
    public void Clear()
    {
        _particles.Clear();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var delta = args.DeltaSeconds;
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var particle = _particles[i];
            particle.Age += delta;
            if (particle.Age >= particle.Life)
            {
                _particles.RemoveAt(i);
                continue;
            }

            particle.Velocity.Y += particle.Gravity * delta;
            particle.Velocity *= MathF.Max(0f, 1f - particle.Drag * delta);
            particle.Position += particle.Velocity * delta;
            _particles[i] = particle;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var canvas = new PixelCanvas(handle, UIScale);
        foreach (var particle in _particles)
        {
            var color = particle.Color;
            if (particle.Fade)
            {
                var spent = particle.Age / particle.Life;
                color = color.WithAlpha(spent < FadeStart ? FreshAlpha : spent < FadeEnd ? FadingAlpha : FadedAlpha);
            }

            var x = (int) MathF.Round(particle.Position.X);
            var y = (int) MathF.Round(particle.Position.Y);
            canvas.Rect(x, y, particle.Size, particle.Size, color);
        }
    }

    private struct Particle(Vector2 position, Vector2 velocity, float life, int size, Color color, float gravity, float drag, bool fade)
    {
        public Vector2 Position = position;
        public Vector2 Velocity = velocity;
        public float Age;
        public readonly float Life = life;
        public readonly int Size = size;
        public readonly Color Color = color;
        public readonly float Gravity = gravity;
        public readonly float Drag = drag;
        public readonly bool Fade = fade;
    }
}
