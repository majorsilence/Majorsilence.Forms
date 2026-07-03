using System.Collections.Concurrent;
using System.Drawing;

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// Pens of width 1 for each named color. Cross-platform replacement for System.Drawing.Pens; each property returns a shared, cached <see cref="Pen"/> (WinForms semantics).
    /// </summary>
    public static class Pens
    {
        private static readonly ConcurrentDictionary<string, Pen> cache = new ();

        private static Pen Get (Color color) =>
            cache.GetOrAdd (color.Name, _ => new Pen (color));

        /// <summary>Gets a pen for the color Transparent.</summary>
        public static Pen Transparent => Get (Color.Transparent);
        /// <summary>Gets a pen for the color AliceBlue.</summary>
        public static Pen AliceBlue => Get (Color.AliceBlue);
        /// <summary>Gets a pen for the color AntiqueWhite.</summary>
        public static Pen AntiqueWhite => Get (Color.AntiqueWhite);
        /// <summary>Gets a pen for the color Aqua.</summary>
        public static Pen Aqua => Get (Color.Aqua);
        /// <summary>Gets a pen for the color Aquamarine.</summary>
        public static Pen Aquamarine => Get (Color.Aquamarine);
        /// <summary>Gets a pen for the color Azure.</summary>
        public static Pen Azure => Get (Color.Azure);
        /// <summary>Gets a pen for the color Beige.</summary>
        public static Pen Beige => Get (Color.Beige);
        /// <summary>Gets a pen for the color Bisque.</summary>
        public static Pen Bisque => Get (Color.Bisque);
        /// <summary>Gets a pen for the color Black.</summary>
        public static Pen Black => Get (Color.Black);
        /// <summary>Gets a pen for the color BlanchedAlmond.</summary>
        public static Pen BlanchedAlmond => Get (Color.BlanchedAlmond);
        /// <summary>Gets a pen for the color Blue.</summary>
        public static Pen Blue => Get (Color.Blue);
        /// <summary>Gets a pen for the color BlueViolet.</summary>
        public static Pen BlueViolet => Get (Color.BlueViolet);
        /// <summary>Gets a pen for the color Brown.</summary>
        public static Pen Brown => Get (Color.Brown);
        /// <summary>Gets a pen for the color BurlyWood.</summary>
        public static Pen BurlyWood => Get (Color.BurlyWood);
        /// <summary>Gets a pen for the color CadetBlue.</summary>
        public static Pen CadetBlue => Get (Color.CadetBlue);
        /// <summary>Gets a pen for the color Chartreuse.</summary>
        public static Pen Chartreuse => Get (Color.Chartreuse);
        /// <summary>Gets a pen for the color Chocolate.</summary>
        public static Pen Chocolate => Get (Color.Chocolate);
        /// <summary>Gets a pen for the color Coral.</summary>
        public static Pen Coral => Get (Color.Coral);
        /// <summary>Gets a pen for the color CornflowerBlue.</summary>
        public static Pen CornflowerBlue => Get (Color.CornflowerBlue);
        /// <summary>Gets a pen for the color Cornsilk.</summary>
        public static Pen Cornsilk => Get (Color.Cornsilk);
        /// <summary>Gets a pen for the color Crimson.</summary>
        public static Pen Crimson => Get (Color.Crimson);
        /// <summary>Gets a pen for the color Cyan.</summary>
        public static Pen Cyan => Get (Color.Cyan);
        /// <summary>Gets a pen for the color DarkBlue.</summary>
        public static Pen DarkBlue => Get (Color.DarkBlue);
        /// <summary>Gets a pen for the color DarkCyan.</summary>
        public static Pen DarkCyan => Get (Color.DarkCyan);
        /// <summary>Gets a pen for the color DarkGoldenrod.</summary>
        public static Pen DarkGoldenrod => Get (Color.DarkGoldenrod);
        /// <summary>Gets a pen for the color DarkGray.</summary>
        public static Pen DarkGray => Get (Color.DarkGray);
        /// <summary>Gets a pen for the color DarkGreen.</summary>
        public static Pen DarkGreen => Get (Color.DarkGreen);
        /// <summary>Gets a pen for the color DarkKhaki.</summary>
        public static Pen DarkKhaki => Get (Color.DarkKhaki);
        /// <summary>Gets a pen for the color DarkMagenta.</summary>
        public static Pen DarkMagenta => Get (Color.DarkMagenta);
        /// <summary>Gets a pen for the color DarkOliveGreen.</summary>
        public static Pen DarkOliveGreen => Get (Color.DarkOliveGreen);
        /// <summary>Gets a pen for the color DarkOrange.</summary>
        public static Pen DarkOrange => Get (Color.DarkOrange);
        /// <summary>Gets a pen for the color DarkOrchid.</summary>
        public static Pen DarkOrchid => Get (Color.DarkOrchid);
        /// <summary>Gets a pen for the color DarkRed.</summary>
        public static Pen DarkRed => Get (Color.DarkRed);
        /// <summary>Gets a pen for the color DarkSalmon.</summary>
        public static Pen DarkSalmon => Get (Color.DarkSalmon);
        /// <summary>Gets a pen for the color DarkSeaGreen.</summary>
        public static Pen DarkSeaGreen => Get (Color.DarkSeaGreen);
        /// <summary>Gets a pen for the color DarkSlateBlue.</summary>
        public static Pen DarkSlateBlue => Get (Color.DarkSlateBlue);
        /// <summary>Gets a pen for the color DarkSlateGray.</summary>
        public static Pen DarkSlateGray => Get (Color.DarkSlateGray);
        /// <summary>Gets a pen for the color DarkTurquoise.</summary>
        public static Pen DarkTurquoise => Get (Color.DarkTurquoise);
        /// <summary>Gets a pen for the color DarkViolet.</summary>
        public static Pen DarkViolet => Get (Color.DarkViolet);
        /// <summary>Gets a pen for the color DeepPink.</summary>
        public static Pen DeepPink => Get (Color.DeepPink);
        /// <summary>Gets a pen for the color DeepSkyBlue.</summary>
        public static Pen DeepSkyBlue => Get (Color.DeepSkyBlue);
        /// <summary>Gets a pen for the color DimGray.</summary>
        public static Pen DimGray => Get (Color.DimGray);
        /// <summary>Gets a pen for the color DodgerBlue.</summary>
        public static Pen DodgerBlue => Get (Color.DodgerBlue);
        /// <summary>Gets a pen for the color Firebrick.</summary>
        public static Pen Firebrick => Get (Color.Firebrick);
        /// <summary>Gets a pen for the color FloralWhite.</summary>
        public static Pen FloralWhite => Get (Color.FloralWhite);
        /// <summary>Gets a pen for the color ForestGreen.</summary>
        public static Pen ForestGreen => Get (Color.ForestGreen);
        /// <summary>Gets a pen for the color Fuchsia.</summary>
        public static Pen Fuchsia => Get (Color.Fuchsia);
        /// <summary>Gets a pen for the color Gainsboro.</summary>
        public static Pen Gainsboro => Get (Color.Gainsboro);
        /// <summary>Gets a pen for the color GhostWhite.</summary>
        public static Pen GhostWhite => Get (Color.GhostWhite);
        /// <summary>Gets a pen for the color Gold.</summary>
        public static Pen Gold => Get (Color.Gold);
        /// <summary>Gets a pen for the color Goldenrod.</summary>
        public static Pen Goldenrod => Get (Color.Goldenrod);
        /// <summary>Gets a pen for the color Gray.</summary>
        public static Pen Gray => Get (Color.Gray);
        /// <summary>Gets a pen for the color Green.</summary>
        public static Pen Green => Get (Color.Green);
        /// <summary>Gets a pen for the color GreenYellow.</summary>
        public static Pen GreenYellow => Get (Color.GreenYellow);
        /// <summary>Gets a pen for the color Honeydew.</summary>
        public static Pen Honeydew => Get (Color.Honeydew);
        /// <summary>Gets a pen for the color HotPink.</summary>
        public static Pen HotPink => Get (Color.HotPink);
        /// <summary>Gets a pen for the color IndianRed.</summary>
        public static Pen IndianRed => Get (Color.IndianRed);
        /// <summary>Gets a pen for the color Indigo.</summary>
        public static Pen Indigo => Get (Color.Indigo);
        /// <summary>Gets a pen for the color Ivory.</summary>
        public static Pen Ivory => Get (Color.Ivory);
        /// <summary>Gets a pen for the color Khaki.</summary>
        public static Pen Khaki => Get (Color.Khaki);
        /// <summary>Gets a pen for the color Lavender.</summary>
        public static Pen Lavender => Get (Color.Lavender);
        /// <summary>Gets a pen for the color LavenderBlush.</summary>
        public static Pen LavenderBlush => Get (Color.LavenderBlush);
        /// <summary>Gets a pen for the color LawnGreen.</summary>
        public static Pen LawnGreen => Get (Color.LawnGreen);
        /// <summary>Gets a pen for the color LemonChiffon.</summary>
        public static Pen LemonChiffon => Get (Color.LemonChiffon);
        /// <summary>Gets a pen for the color LightBlue.</summary>
        public static Pen LightBlue => Get (Color.LightBlue);
        /// <summary>Gets a pen for the color LightCoral.</summary>
        public static Pen LightCoral => Get (Color.LightCoral);
        /// <summary>Gets a pen for the color LightCyan.</summary>
        public static Pen LightCyan => Get (Color.LightCyan);
        /// <summary>Gets a pen for the color LightGoldenrodYellow.</summary>
        public static Pen LightGoldenrodYellow => Get (Color.LightGoldenrodYellow);
        /// <summary>Gets a pen for the color LightGreen.</summary>
        public static Pen LightGreen => Get (Color.LightGreen);
        /// <summary>Gets a pen for the color LightGray.</summary>
        public static Pen LightGray => Get (Color.LightGray);
        /// <summary>Gets a pen for the color LightPink.</summary>
        public static Pen LightPink => Get (Color.LightPink);
        /// <summary>Gets a pen for the color LightSalmon.</summary>
        public static Pen LightSalmon => Get (Color.LightSalmon);
        /// <summary>Gets a pen for the color LightSeaGreen.</summary>
        public static Pen LightSeaGreen => Get (Color.LightSeaGreen);
        /// <summary>Gets a pen for the color LightSkyBlue.</summary>
        public static Pen LightSkyBlue => Get (Color.LightSkyBlue);
        /// <summary>Gets a pen for the color LightSlateGray.</summary>
        public static Pen LightSlateGray => Get (Color.LightSlateGray);
        /// <summary>Gets a pen for the color LightSteelBlue.</summary>
        public static Pen LightSteelBlue => Get (Color.LightSteelBlue);
        /// <summary>Gets a pen for the color LightYellow.</summary>
        public static Pen LightYellow => Get (Color.LightYellow);
        /// <summary>Gets a pen for the color Lime.</summary>
        public static Pen Lime => Get (Color.Lime);
        /// <summary>Gets a pen for the color LimeGreen.</summary>
        public static Pen LimeGreen => Get (Color.LimeGreen);
        /// <summary>Gets a pen for the color Linen.</summary>
        public static Pen Linen => Get (Color.Linen);
        /// <summary>Gets a pen for the color Magenta.</summary>
        public static Pen Magenta => Get (Color.Magenta);
        /// <summary>Gets a pen for the color Maroon.</summary>
        public static Pen Maroon => Get (Color.Maroon);
        /// <summary>Gets a pen for the color MediumAquamarine.</summary>
        public static Pen MediumAquamarine => Get (Color.MediumAquamarine);
        /// <summary>Gets a pen for the color MediumBlue.</summary>
        public static Pen MediumBlue => Get (Color.MediumBlue);
        /// <summary>Gets a pen for the color MediumOrchid.</summary>
        public static Pen MediumOrchid => Get (Color.MediumOrchid);
        /// <summary>Gets a pen for the color MediumPurple.</summary>
        public static Pen MediumPurple => Get (Color.MediumPurple);
        /// <summary>Gets a pen for the color MediumSeaGreen.</summary>
        public static Pen MediumSeaGreen => Get (Color.MediumSeaGreen);
        /// <summary>Gets a pen for the color MediumSlateBlue.</summary>
        public static Pen MediumSlateBlue => Get (Color.MediumSlateBlue);
        /// <summary>Gets a pen for the color MediumSpringGreen.</summary>
        public static Pen MediumSpringGreen => Get (Color.MediumSpringGreen);
        /// <summary>Gets a pen for the color MediumTurquoise.</summary>
        public static Pen MediumTurquoise => Get (Color.MediumTurquoise);
        /// <summary>Gets a pen for the color MediumVioletRed.</summary>
        public static Pen MediumVioletRed => Get (Color.MediumVioletRed);
        /// <summary>Gets a pen for the color MidnightBlue.</summary>
        public static Pen MidnightBlue => Get (Color.MidnightBlue);
        /// <summary>Gets a pen for the color MintCream.</summary>
        public static Pen MintCream => Get (Color.MintCream);
        /// <summary>Gets a pen for the color MistyRose.</summary>
        public static Pen MistyRose => Get (Color.MistyRose);
        /// <summary>Gets a pen for the color Moccasin.</summary>
        public static Pen Moccasin => Get (Color.Moccasin);
        /// <summary>Gets a pen for the color NavajoWhite.</summary>
        public static Pen NavajoWhite => Get (Color.NavajoWhite);
        /// <summary>Gets a pen for the color Navy.</summary>
        public static Pen Navy => Get (Color.Navy);
        /// <summary>Gets a pen for the color OldLace.</summary>
        public static Pen OldLace => Get (Color.OldLace);
        /// <summary>Gets a pen for the color Olive.</summary>
        public static Pen Olive => Get (Color.Olive);
        /// <summary>Gets a pen for the color OliveDrab.</summary>
        public static Pen OliveDrab => Get (Color.OliveDrab);
        /// <summary>Gets a pen for the color Orange.</summary>
        public static Pen Orange => Get (Color.Orange);
        /// <summary>Gets a pen for the color OrangeRed.</summary>
        public static Pen OrangeRed => Get (Color.OrangeRed);
        /// <summary>Gets a pen for the color Orchid.</summary>
        public static Pen Orchid => Get (Color.Orchid);
        /// <summary>Gets a pen for the color PaleGoldenrod.</summary>
        public static Pen PaleGoldenrod => Get (Color.PaleGoldenrod);
        /// <summary>Gets a pen for the color PaleGreen.</summary>
        public static Pen PaleGreen => Get (Color.PaleGreen);
        /// <summary>Gets a pen for the color PaleTurquoise.</summary>
        public static Pen PaleTurquoise => Get (Color.PaleTurquoise);
        /// <summary>Gets a pen for the color PaleVioletRed.</summary>
        public static Pen PaleVioletRed => Get (Color.PaleVioletRed);
        /// <summary>Gets a pen for the color PapayaWhip.</summary>
        public static Pen PapayaWhip => Get (Color.PapayaWhip);
        /// <summary>Gets a pen for the color PeachPuff.</summary>
        public static Pen PeachPuff => Get (Color.PeachPuff);
        /// <summary>Gets a pen for the color Peru.</summary>
        public static Pen Peru => Get (Color.Peru);
        /// <summary>Gets a pen for the color Pink.</summary>
        public static Pen Pink => Get (Color.Pink);
        /// <summary>Gets a pen for the color Plum.</summary>
        public static Pen Plum => Get (Color.Plum);
        /// <summary>Gets a pen for the color PowderBlue.</summary>
        public static Pen PowderBlue => Get (Color.PowderBlue);
        /// <summary>Gets a pen for the color Purple.</summary>
        public static Pen Purple => Get (Color.Purple);
        /// <summary>Gets a pen for the color RebeccaPurple.</summary>
        public static Pen RebeccaPurple => Get (Color.RebeccaPurple);
        /// <summary>Gets a pen for the color Red.</summary>
        public static Pen Red => Get (Color.Red);
        /// <summary>Gets a pen for the color RosyBrown.</summary>
        public static Pen RosyBrown => Get (Color.RosyBrown);
        /// <summary>Gets a pen for the color RoyalBlue.</summary>
        public static Pen RoyalBlue => Get (Color.RoyalBlue);
        /// <summary>Gets a pen for the color SaddleBrown.</summary>
        public static Pen SaddleBrown => Get (Color.SaddleBrown);
        /// <summary>Gets a pen for the color Salmon.</summary>
        public static Pen Salmon => Get (Color.Salmon);
        /// <summary>Gets a pen for the color SandyBrown.</summary>
        public static Pen SandyBrown => Get (Color.SandyBrown);
        /// <summary>Gets a pen for the color SeaGreen.</summary>
        public static Pen SeaGreen => Get (Color.SeaGreen);
        /// <summary>Gets a pen for the color SeaShell.</summary>
        public static Pen SeaShell => Get (Color.SeaShell);
        /// <summary>Gets a pen for the color Sienna.</summary>
        public static Pen Sienna => Get (Color.Sienna);
        /// <summary>Gets a pen for the color Silver.</summary>
        public static Pen Silver => Get (Color.Silver);
        /// <summary>Gets a pen for the color SkyBlue.</summary>
        public static Pen SkyBlue => Get (Color.SkyBlue);
        /// <summary>Gets a pen for the color SlateBlue.</summary>
        public static Pen SlateBlue => Get (Color.SlateBlue);
        /// <summary>Gets a pen for the color SlateGray.</summary>
        public static Pen SlateGray => Get (Color.SlateGray);
        /// <summary>Gets a pen for the color Snow.</summary>
        public static Pen Snow => Get (Color.Snow);
        /// <summary>Gets a pen for the color SpringGreen.</summary>
        public static Pen SpringGreen => Get (Color.SpringGreen);
        /// <summary>Gets a pen for the color SteelBlue.</summary>
        public static Pen SteelBlue => Get (Color.SteelBlue);
        /// <summary>Gets a pen for the color Tan.</summary>
        public static Pen Tan => Get (Color.Tan);
        /// <summary>Gets a pen for the color Teal.</summary>
        public static Pen Teal => Get (Color.Teal);
        /// <summary>Gets a pen for the color Thistle.</summary>
        public static Pen Thistle => Get (Color.Thistle);
        /// <summary>Gets a pen for the color Tomato.</summary>
        public static Pen Tomato => Get (Color.Tomato);
        /// <summary>Gets a pen for the color Turquoise.</summary>
        public static Pen Turquoise => Get (Color.Turquoise);
        /// <summary>Gets a pen for the color Violet.</summary>
        public static Pen Violet => Get (Color.Violet);
        /// <summary>Gets a pen for the color Wheat.</summary>
        public static Pen Wheat => Get (Color.Wheat);
        /// <summary>Gets a pen for the color White.</summary>
        public static Pen White => Get (Color.White);
        /// <summary>Gets a pen for the color WhiteSmoke.</summary>
        public static Pen WhiteSmoke => Get (Color.WhiteSmoke);
        /// <summary>Gets a pen for the color Yellow.</summary>
        public static Pen Yellow => Get (Color.Yellow);
        /// <summary>Gets a pen for the color YellowGreen.</summary>
        public static Pen YellowGreen => Get (Color.YellowGreen);
    }
}
