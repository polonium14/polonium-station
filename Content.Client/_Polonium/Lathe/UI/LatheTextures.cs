using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;

namespace Content.Client._Polonium.Lathe.UI;

public sealed class LatheTextures
{
    public readonly Texture Body;
    public readonly Texture Plate;
    public readonly Texture Ticket;
    public readonly Texture Gauge;
    public readonly Texture KeyLight;
    public readonly Texture KeyDark;
    public readonly Texture KeyRed;
    public readonly Texture Leds;
    public readonly Texture Stop;
    public readonly Texture Icons;
    public readonly Texture TankBack;
    public readonly Texture TankFront;

    public LatheTextures(IResourceCache cache)
    {
        Body = Load(cache, "body");
        Plate = Load(cache, "plate");
        Ticket = Load(cache, "ticket");
        Gauge = Load(cache, "gauge");
        KeyLight = Load(cache, "key_light");
        KeyDark = Load(cache, "key_dark");
        KeyRed = Load(cache, "key_red");
        Leds = Load(cache, "leds");
        Stop = Load(cache, "stop");
        Icons = Load(cache, "icons");
        TankBack = Load(cache, "tank_back");
        TankFront = Load(cache, "tank_front");
    }

    private static Texture Load(IResourceCache cache, string name)
    {
        return cache.GetTexture($"{LathePalette.TexturePath}{name}.png");
    }
}
