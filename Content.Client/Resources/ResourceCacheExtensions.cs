using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Resources
{
    [PublicAPI]
    public static class ResourceCacheExtensions
    {
        public static Texture GetTexture(this IResourceCache cache, ResPath path)
        {
            return cache.GetResource<TextureResource>(path);
        }

        public static Texture GetTexture(this IResourceCache cache, string path)
        {
            return GetTexture(cache, new ResPath(path));
        }

        public static Font GetFont(this IResourceCache cache, ResPath path, int size)
        {
            return new VectorFont(cache.GetResource<FontResource>(path), size);
        }

        public static Font GetFont(this IResourceCache cache, string path, int size)
        {
            return cache.GetFont(new ResPath(path), size);
        }

        public static Font GetFont(this IResourceCache cache, ResPath[] path, int size)
        {
            var fs = new Font[path.Length];
            for (var i = 0; i < path.Length; i++)
                fs[i] = new VectorFont(cache.GetResource<FontResource>(path[i]), size);

            return new StackedFont(fs);
        }

        public static Font GetFont(this IResourceCache cache, string[] path, int size)
        {
            var rp = new ResPath[path.Length];
            for (var i = 0; i < path.Length; i++)
                rp[i] = new ResPath(path[i]);

            return cache.GetFont(rp, size);
        }

        // POLONIUM: Get a font from a font prototype
        public static Font GetFont(this IResourceCache cache, IPrototypeManager prototypes, ProtoId<FontPrototype> font, int size)
        {
            return cache.GetFont(prototypes.Index(font).Path, size);
        }
    }
}
