using Continuum.Drawing;

namespace Outlaw
{
    public static class ImageLoader
    {
        private static readonly Dictionary<string, Bitmap> _cache = [];
        private static readonly string _defaultLocation = "Images";

        public static Bitmap Get (string filename)
        {
            var key = filename.ToLowerInvariant ();

            if (!_cache.TryGetValue (key, out var bitmap)) {
                bitmap = new Bitmap (Path.Combine (_defaultLocation, filename));
                _cache[key] = bitmap;
            }

            return bitmap;
        }
    }
}
