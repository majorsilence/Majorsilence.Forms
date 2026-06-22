using System.Collections.Concurrent;
using System.Drawing;

namespace Majorsilence.Drawing
{
    /// <summary>
    /// Brushes for each named color. Cross-platform replacement for System.Drawing.Brushes; each property returns a shared, cached <see cref="SolidBrush"/> (WinForms semantics).
    /// </summary>
    public static class Brushes
    {
        private static readonly ConcurrentDictionary<string, Brush> cache = new ();

        private static Brush Get (Color color) =>
            cache.GetOrAdd (color.Name, _ => new SolidBrush (color));

        /// <summary>Gets a brush for the color Transparent.</summary>
        public static Brush Transparent => Get (Color.Transparent);
        /// <summary>Gets a brush for the color AliceBlue.</summary>
        public static Brush AliceBlue => Get (Color.AliceBlue);
        /// <summary>Gets a brush for the color AntiqueWhite.</summary>
        public static Brush AntiqueWhite => Get (Color.AntiqueWhite);
        /// <summary>Gets a brush for the color Aqua.</summary>
        public static Brush Aqua => Get (Color.Aqua);
        /// <summary>Gets a brush for the color Aquamarine.</summary>
        public static Brush Aquamarine => Get (Color.Aquamarine);
        /// <summary>Gets a brush for the color Azure.</summary>
        public static Brush Azure => Get (Color.Azure);
        /// <summary>Gets a brush for the color Beige.</summary>
        public static Brush Beige => Get (Color.Beige);
        /// <summary>Gets a brush for the color Bisque.</summary>
        public static Brush Bisque => Get (Color.Bisque);
        /// <summary>Gets a brush for the color Black.</summary>
        public static Brush Black => Get (Color.Black);
        /// <summary>Gets a brush for the color BlanchedAlmond.</summary>
        public static Brush BlanchedAlmond => Get (Color.BlanchedAlmond);
        /// <summary>Gets a brush for the color Blue.</summary>
        public static Brush Blue => Get (Color.Blue);
        /// <summary>Gets a brush for the color BlueViolet.</summary>
        public static Brush BlueViolet => Get (Color.BlueViolet);
        /// <summary>Gets a brush for the color Brown.</summary>
        public static Brush Brown => Get (Color.Brown);
        /// <summary>Gets a brush for the color BurlyWood.</summary>
        public static Brush BurlyWood => Get (Color.BurlyWood);
        /// <summary>Gets a brush for the color CadetBlue.</summary>
        public static Brush CadetBlue => Get (Color.CadetBlue);
        /// <summary>Gets a brush for the color Chartreuse.</summary>
        public static Brush Chartreuse => Get (Color.Chartreuse);
        /// <summary>Gets a brush for the color Chocolate.</summary>
        public static Brush Chocolate => Get (Color.Chocolate);
        /// <summary>Gets a brush for the color Coral.</summary>
        public static Brush Coral => Get (Color.Coral);
        /// <summary>Gets a brush for the color CornflowerBlue.</summary>
        public static Brush CornflowerBlue => Get (Color.CornflowerBlue);
        /// <summary>Gets a brush for the color Cornsilk.</summary>
        public static Brush Cornsilk => Get (Color.Cornsilk);
        /// <summary>Gets a brush for the color Crimson.</summary>
        public static Brush Crimson => Get (Color.Crimson);
        /// <summary>Gets a brush for the color Cyan.</summary>
        public static Brush Cyan => Get (Color.Cyan);
        /// <summary>Gets a brush for the color DarkBlue.</summary>
        public static Brush DarkBlue => Get (Color.DarkBlue);
        /// <summary>Gets a brush for the color DarkCyan.</summary>
        public static Brush DarkCyan => Get (Color.DarkCyan);
        /// <summary>Gets a brush for the color DarkGoldenrod.</summary>
        public static Brush DarkGoldenrod => Get (Color.DarkGoldenrod);
        /// <summary>Gets a brush for the color DarkGray.</summary>
        public static Brush DarkGray => Get (Color.DarkGray);
        /// <summary>Gets a brush for the color DarkGreen.</summary>
        public static Brush DarkGreen => Get (Color.DarkGreen);
        /// <summary>Gets a brush for the color DarkKhaki.</summary>
        public static Brush DarkKhaki => Get (Color.DarkKhaki);
        /// <summary>Gets a brush for the color DarkMagenta.</summary>
        public static Brush DarkMagenta => Get (Color.DarkMagenta);
        /// <summary>Gets a brush for the color DarkOliveGreen.</summary>
        public static Brush DarkOliveGreen => Get (Color.DarkOliveGreen);
        /// <summary>Gets a brush for the color DarkOrange.</summary>
        public static Brush DarkOrange => Get (Color.DarkOrange);
        /// <summary>Gets a brush for the color DarkOrchid.</summary>
        public static Brush DarkOrchid => Get (Color.DarkOrchid);
        /// <summary>Gets a brush for the color DarkRed.</summary>
        public static Brush DarkRed => Get (Color.DarkRed);
        /// <summary>Gets a brush for the color DarkSalmon.</summary>
        public static Brush DarkSalmon => Get (Color.DarkSalmon);
        /// <summary>Gets a brush for the color DarkSeaGreen.</summary>
        public static Brush DarkSeaGreen => Get (Color.DarkSeaGreen);
        /// <summary>Gets a brush for the color DarkSlateBlue.</summary>
        public static Brush DarkSlateBlue => Get (Color.DarkSlateBlue);
        /// <summary>Gets a brush for the color DarkSlateGray.</summary>
        public static Brush DarkSlateGray => Get (Color.DarkSlateGray);
        /// <summary>Gets a brush for the color DarkTurquoise.</summary>
        public static Brush DarkTurquoise => Get (Color.DarkTurquoise);
        /// <summary>Gets a brush for the color DarkViolet.</summary>
        public static Brush DarkViolet => Get (Color.DarkViolet);
        /// <summary>Gets a brush for the color DeepPink.</summary>
        public static Brush DeepPink => Get (Color.DeepPink);
        /// <summary>Gets a brush for the color DeepSkyBlue.</summary>
        public static Brush DeepSkyBlue => Get (Color.DeepSkyBlue);
        /// <summary>Gets a brush for the color DimGray.</summary>
        public static Brush DimGray => Get (Color.DimGray);
        /// <summary>Gets a brush for the color DodgerBlue.</summary>
        public static Brush DodgerBlue => Get (Color.DodgerBlue);
        /// <summary>Gets a brush for the color Firebrick.</summary>
        public static Brush Firebrick => Get (Color.Firebrick);
        /// <summary>Gets a brush for the color FloralWhite.</summary>
        public static Brush FloralWhite => Get (Color.FloralWhite);
        /// <summary>Gets a brush for the color ForestGreen.</summary>
        public static Brush ForestGreen => Get (Color.ForestGreen);
        /// <summary>Gets a brush for the color Fuchsia.</summary>
        public static Brush Fuchsia => Get (Color.Fuchsia);
        /// <summary>Gets a brush for the color Gainsboro.</summary>
        public static Brush Gainsboro => Get (Color.Gainsboro);
        /// <summary>Gets a brush for the color GhostWhite.</summary>
        public static Brush GhostWhite => Get (Color.GhostWhite);
        /// <summary>Gets a brush for the color Gold.</summary>
        public static Brush Gold => Get (Color.Gold);
        /// <summary>Gets a brush for the color Goldenrod.</summary>
        public static Brush Goldenrod => Get (Color.Goldenrod);
        /// <summary>Gets a brush for the color Gray.</summary>
        public static Brush Gray => Get (Color.Gray);
        /// <summary>Gets a brush for the color Green.</summary>
        public static Brush Green => Get (Color.Green);
        /// <summary>Gets a brush for the color GreenYellow.</summary>
        public static Brush GreenYellow => Get (Color.GreenYellow);
        /// <summary>Gets a brush for the color Honeydew.</summary>
        public static Brush Honeydew => Get (Color.Honeydew);
        /// <summary>Gets a brush for the color HotPink.</summary>
        public static Brush HotPink => Get (Color.HotPink);
        /// <summary>Gets a brush for the color IndianRed.</summary>
        public static Brush IndianRed => Get (Color.IndianRed);
        /// <summary>Gets a brush for the color Indigo.</summary>
        public static Brush Indigo => Get (Color.Indigo);
        /// <summary>Gets a brush for the color Ivory.</summary>
        public static Brush Ivory => Get (Color.Ivory);
        /// <summary>Gets a brush for the color Khaki.</summary>
        public static Brush Khaki => Get (Color.Khaki);
        /// <summary>Gets a brush for the color Lavender.</summary>
        public static Brush Lavender => Get (Color.Lavender);
        /// <summary>Gets a brush for the color LavenderBlush.</summary>
        public static Brush LavenderBlush => Get (Color.LavenderBlush);
        /// <summary>Gets a brush for the color LawnGreen.</summary>
        public static Brush LawnGreen => Get (Color.LawnGreen);
        /// <summary>Gets a brush for the color LemonChiffon.</summary>
        public static Brush LemonChiffon => Get (Color.LemonChiffon);
        /// <summary>Gets a brush for the color LightBlue.</summary>
        public static Brush LightBlue => Get (Color.LightBlue);
        /// <summary>Gets a brush for the color LightCoral.</summary>
        public static Brush LightCoral => Get (Color.LightCoral);
        /// <summary>Gets a brush for the color LightCyan.</summary>
        public static Brush LightCyan => Get (Color.LightCyan);
        /// <summary>Gets a brush for the color LightGoldenrodYellow.</summary>
        public static Brush LightGoldenrodYellow => Get (Color.LightGoldenrodYellow);
        /// <summary>Gets a brush for the color LightGreen.</summary>
        public static Brush LightGreen => Get (Color.LightGreen);
        /// <summary>Gets a brush for the color LightGray.</summary>
        public static Brush LightGray => Get (Color.LightGray);
        /// <summary>Gets a brush for the color LightPink.</summary>
        public static Brush LightPink => Get (Color.LightPink);
        /// <summary>Gets a brush for the color LightSalmon.</summary>
        public static Brush LightSalmon => Get (Color.LightSalmon);
        /// <summary>Gets a brush for the color LightSeaGreen.</summary>
        public static Brush LightSeaGreen => Get (Color.LightSeaGreen);
        /// <summary>Gets a brush for the color LightSkyBlue.</summary>
        public static Brush LightSkyBlue => Get (Color.LightSkyBlue);
        /// <summary>Gets a brush for the color LightSlateGray.</summary>
        public static Brush LightSlateGray => Get (Color.LightSlateGray);
        /// <summary>Gets a brush for the color LightSteelBlue.</summary>
        public static Brush LightSteelBlue => Get (Color.LightSteelBlue);
        /// <summary>Gets a brush for the color LightYellow.</summary>
        public static Brush LightYellow => Get (Color.LightYellow);
        /// <summary>Gets a brush for the color Lime.</summary>
        public static Brush Lime => Get (Color.Lime);
        /// <summary>Gets a brush for the color LimeGreen.</summary>
        public static Brush LimeGreen => Get (Color.LimeGreen);
        /// <summary>Gets a brush for the color Linen.</summary>
        public static Brush Linen => Get (Color.Linen);
        /// <summary>Gets a brush for the color Magenta.</summary>
        public static Brush Magenta => Get (Color.Magenta);
        /// <summary>Gets a brush for the color Maroon.</summary>
        public static Brush Maroon => Get (Color.Maroon);
        /// <summary>Gets a brush for the color MediumAquamarine.</summary>
        public static Brush MediumAquamarine => Get (Color.MediumAquamarine);
        /// <summary>Gets a brush for the color MediumBlue.</summary>
        public static Brush MediumBlue => Get (Color.MediumBlue);
        /// <summary>Gets a brush for the color MediumOrchid.</summary>
        public static Brush MediumOrchid => Get (Color.MediumOrchid);
        /// <summary>Gets a brush for the color MediumPurple.</summary>
        public static Brush MediumPurple => Get (Color.MediumPurple);
        /// <summary>Gets a brush for the color MediumSeaGreen.</summary>
        public static Brush MediumSeaGreen => Get (Color.MediumSeaGreen);
        /// <summary>Gets a brush for the color MediumSlateBlue.</summary>
        public static Brush MediumSlateBlue => Get (Color.MediumSlateBlue);
        /// <summary>Gets a brush for the color MediumSpringGreen.</summary>
        public static Brush MediumSpringGreen => Get (Color.MediumSpringGreen);
        /// <summary>Gets a brush for the color MediumTurquoise.</summary>
        public static Brush MediumTurquoise => Get (Color.MediumTurquoise);
        /// <summary>Gets a brush for the color MediumVioletRed.</summary>
        public static Brush MediumVioletRed => Get (Color.MediumVioletRed);
        /// <summary>Gets a brush for the color MidnightBlue.</summary>
        public static Brush MidnightBlue => Get (Color.MidnightBlue);
        /// <summary>Gets a brush for the color MintCream.</summary>
        public static Brush MintCream => Get (Color.MintCream);
        /// <summary>Gets a brush for the color MistyRose.</summary>
        public static Brush MistyRose => Get (Color.MistyRose);
        /// <summary>Gets a brush for the color Moccasin.</summary>
        public static Brush Moccasin => Get (Color.Moccasin);
        /// <summary>Gets a brush for the color NavajoWhite.</summary>
        public static Brush NavajoWhite => Get (Color.NavajoWhite);
        /// <summary>Gets a brush for the color Navy.</summary>
        public static Brush Navy => Get (Color.Navy);
        /// <summary>Gets a brush for the color OldLace.</summary>
        public static Brush OldLace => Get (Color.OldLace);
        /// <summary>Gets a brush for the color Olive.</summary>
        public static Brush Olive => Get (Color.Olive);
        /// <summary>Gets a brush for the color OliveDrab.</summary>
        public static Brush OliveDrab => Get (Color.OliveDrab);
        /// <summary>Gets a brush for the color Orange.</summary>
        public static Brush Orange => Get (Color.Orange);
        /// <summary>Gets a brush for the color OrangeRed.</summary>
        public static Brush OrangeRed => Get (Color.OrangeRed);
        /// <summary>Gets a brush for the color Orchid.</summary>
        public static Brush Orchid => Get (Color.Orchid);
        /// <summary>Gets a brush for the color PaleGoldenrod.</summary>
        public static Brush PaleGoldenrod => Get (Color.PaleGoldenrod);
        /// <summary>Gets a brush for the color PaleGreen.</summary>
        public static Brush PaleGreen => Get (Color.PaleGreen);
        /// <summary>Gets a brush for the color PaleTurquoise.</summary>
        public static Brush PaleTurquoise => Get (Color.PaleTurquoise);
        /// <summary>Gets a brush for the color PaleVioletRed.</summary>
        public static Brush PaleVioletRed => Get (Color.PaleVioletRed);
        /// <summary>Gets a brush for the color PapayaWhip.</summary>
        public static Brush PapayaWhip => Get (Color.PapayaWhip);
        /// <summary>Gets a brush for the color PeachPuff.</summary>
        public static Brush PeachPuff => Get (Color.PeachPuff);
        /// <summary>Gets a brush for the color Peru.</summary>
        public static Brush Peru => Get (Color.Peru);
        /// <summary>Gets a brush for the color Pink.</summary>
        public static Brush Pink => Get (Color.Pink);
        /// <summary>Gets a brush for the color Plum.</summary>
        public static Brush Plum => Get (Color.Plum);
        /// <summary>Gets a brush for the color PowderBlue.</summary>
        public static Brush PowderBlue => Get (Color.PowderBlue);
        /// <summary>Gets a brush for the color Purple.</summary>
        public static Brush Purple => Get (Color.Purple);
        /// <summary>Gets a brush for the color RebeccaPurple.</summary>
        public static Brush RebeccaPurple => Get (Color.RebeccaPurple);
        /// <summary>Gets a brush for the color Red.</summary>
        public static Brush Red => Get (Color.Red);
        /// <summary>Gets a brush for the color RosyBrown.</summary>
        public static Brush RosyBrown => Get (Color.RosyBrown);
        /// <summary>Gets a brush for the color RoyalBlue.</summary>
        public static Brush RoyalBlue => Get (Color.RoyalBlue);
        /// <summary>Gets a brush for the color SaddleBrown.</summary>
        public static Brush SaddleBrown => Get (Color.SaddleBrown);
        /// <summary>Gets a brush for the color Salmon.</summary>
        public static Brush Salmon => Get (Color.Salmon);
        /// <summary>Gets a brush for the color SandyBrown.</summary>
        public static Brush SandyBrown => Get (Color.SandyBrown);
        /// <summary>Gets a brush for the color SeaGreen.</summary>
        public static Brush SeaGreen => Get (Color.SeaGreen);
        /// <summary>Gets a brush for the color SeaShell.</summary>
        public static Brush SeaShell => Get (Color.SeaShell);
        /// <summary>Gets a brush for the color Sienna.</summary>
        public static Brush Sienna => Get (Color.Sienna);
        /// <summary>Gets a brush for the color Silver.</summary>
        public static Brush Silver => Get (Color.Silver);
        /// <summary>Gets a brush for the color SkyBlue.</summary>
        public static Brush SkyBlue => Get (Color.SkyBlue);
        /// <summary>Gets a brush for the color SlateBlue.</summary>
        public static Brush SlateBlue => Get (Color.SlateBlue);
        /// <summary>Gets a brush for the color SlateGray.</summary>
        public static Brush SlateGray => Get (Color.SlateGray);
        /// <summary>Gets a brush for the color Snow.</summary>
        public static Brush Snow => Get (Color.Snow);
        /// <summary>Gets a brush for the color SpringGreen.</summary>
        public static Brush SpringGreen => Get (Color.SpringGreen);
        /// <summary>Gets a brush for the color SteelBlue.</summary>
        public static Brush SteelBlue => Get (Color.SteelBlue);
        /// <summary>Gets a brush for the color Tan.</summary>
        public static Brush Tan => Get (Color.Tan);
        /// <summary>Gets a brush for the color Teal.</summary>
        public static Brush Teal => Get (Color.Teal);
        /// <summary>Gets a brush for the color Thistle.</summary>
        public static Brush Thistle => Get (Color.Thistle);
        /// <summary>Gets a brush for the color Tomato.</summary>
        public static Brush Tomato => Get (Color.Tomato);
        /// <summary>Gets a brush for the color Turquoise.</summary>
        public static Brush Turquoise => Get (Color.Turquoise);
        /// <summary>Gets a brush for the color Violet.</summary>
        public static Brush Violet => Get (Color.Violet);
        /// <summary>Gets a brush for the color Wheat.</summary>
        public static Brush Wheat => Get (Color.Wheat);
        /// <summary>Gets a brush for the color White.</summary>
        public static Brush White => Get (Color.White);
        /// <summary>Gets a brush for the color WhiteSmoke.</summary>
        public static Brush WhiteSmoke => Get (Color.WhiteSmoke);
        /// <summary>Gets a brush for the color Yellow.</summary>
        public static Brush Yellow => Get (Color.Yellow);
        /// <summary>Gets a brush for the color YellowGreen.</summary>
        public static Brush YellowGreen => Get (Color.YellowGreen);
    }
}
