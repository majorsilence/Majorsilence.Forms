using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Majorsilence.Forms
{
    // background-image: none, or a comma-separated list of CSS gradients -- linear-gradient(), radial-gradient(), conic-gradient() and
    // their repeating- forms. Images (url()) are not: a theme has no file to load them from. Each layer is built from thunks, so a stop
    // written as a token follows the token.
    internal static class ThemeCssGradients
    {
        public const string Help =
            "background-image takes gradients: linear-gradient([<angle> | to <side>,] <color> [<position>], ...), "
            + "radial-gradient([circle | ellipse] [<size>] [at <position>], ...), conic-gradient([from <angle>] [at <position>], ...) "
            + "and their repeating- forms, or 'none'.";

        private static readonly string[] names = {
            "linear-gradient", "repeating-linear-gradient", "radial-gradient", "repeating-radial-gradient", "conic-gradient", "repeating-conic-gradient"
        };

        public static bool TryParse (List<CssComponent> components, out Func<IReadOnlyList<ControlGradient>> layers, out string? error)
        {
            layers = null!;
            error = null;

            if (components.Count == 1 && components[0] is CssIdent { Name: var none } && none.Equals ("none", StringComparison.OrdinalIgnoreCase)) {
                var empty = Array.Empty<ControlGradient> ();
                layers = () => empty;
                return true;
            }

            var parsed = new List<Func<ControlGradient>> ();

            foreach (var group in ThemeCssValues.SplitCommas (components)) {
                if (group.Count != 1 || group[0] is not CssFunction fn) {
                    error = $"'{ThemeCssValues.Join (group)}' is not a gradient. {Help}";
                    return false;
                }

                if (!TryParseGradient (fn, out var layer, out error))
                    return false;

                parsed.Add (layer);
            }

            var thunks = parsed.ToArray ();
            layers = () => thunks.Select (t => t ()).ToArray ();
            return true;
        }

        private static bool TryParseGradient (CssFunction fn, out Func<ControlGradient> gradient, out string? error)
        {
            gradient = null!;
            var name = fn.Name.ToLowerInvariant ();

            if (name == "url" || name == "image-set" || name == "image" || name == "cross-fade" || name == "element") {
                error = $"'{fn.Name}()' is not supported: a theme has no file to load an image from, so background-image takes gradients only. {Help}";
                return false;
            }

            if (!names.Contains (name)) {
                error = $"'{fn.Name}()' is not a gradient.{ThemeCssValues.Suggest (name, names)} {Help}";
                return false;
            }

            var repeating = name.StartsWith ("repeating-", StringComparison.Ordinal);
            var kind = name.EndsWith ("linear-gradient", StringComparison.Ordinal) ? ControlGradientKind.Linear
                : name.EndsWith ("radial-gradient", StringComparison.Ordinal) ? ControlGradientKind.Radial
                : ControlGradientKind.Conic;

            var groups = fn.Arguments;

            if (groups.Count == 0) {
                error = $"'{fn.Raw}' needs at least two colour stops.";
                return false;
            }

            // The first argument is the gradient's shape unless it begins with a colour.
            var configuration = !StartsWithColor (groups[0]) ? groups[0] : null;

            if (configuration is not null && configuration.Any (c => c is CssIdent { Name: var word } && word.Equals ("in", StringComparison.OrdinalIgnoreCase))) {
                error = $"'{fn.Raw}': an 'in <colorspace>' interpolation method is not supported; gradients interpolate in sRGB, premultiplied.";
                return false;
            }

            var stopGroups = configuration is null ? groups : groups.Skip (1).ToList ();

            if (stopGroups.Count < 2) {
                error = $"'{fn.Raw}' needs at least two colour stops, e.g. {name}(#0d6efd, #6610f2).";
                return false;
            }

            var stops = new List<(Func<SKColor> Color, double Position, ControlGradient.StopUnit Unit)> ();

            foreach (var group in stopGroups) {
                if (!TryParseStop (fn, group, kind, stops, out error))
                    return false;
            }

            Func<IReadOnlyList<ControlGradient.Stop>> resolveStops = () => stops.Select (s => new ControlGradient.Stop (s.Color (), s.Position, s.Unit)).ToArray ();
            var css = fn.Raw;

            switch (kind) {
                case ControlGradientKind.Linear: {
                    double angle = 180;
                    (bool, bool, bool, bool)? corner = null;

                    if (configuration is not null && !TryParseLinearDirection (fn, configuration, out angle, out corner, out error))
                        return false;

                    gradient = () => new ControlGradient (css, kind, repeating, resolveStops ()) { Angle = angle, Corner = corner };
                    break;
                }

                case ControlGradientKind.Radial: {
                    var shape = new RadialShape ();

                    if (configuration is not null && !TryParseRadialShape (fn, configuration, ref shape, out error))
                        return false;

                    gradient = () => new ControlGradient (css, kind, repeating, resolveStops ()) {
                        Circle = shape.Circle, Size = shape.Size, SizeX = shape.SizeX, SizeY = shape.SizeY, X = shape.X, Y = shape.Y
                    };
                    break;
                }

                default: {
                    double from = 0;
                    var x = ControlGradient.Offset.Center;
                    var y = ControlGradient.Offset.Center;

                    if (configuration is not null && !TryParseConicShape (fn, configuration, out from, out x, out y, out error))
                        return false;

                    gradient = () => new ControlGradient (css, kind, repeating, resolveStops ()) { From = from, X = x, Y = y };
                    break;
                }
            }

            error = null;
            return true;
        }

        private static bool StartsWithColor (List<CssComponent> group)
            => group.Count > 0 && group[0] is not CssNumber && !ThemeCssCalc.IsMathFunction (group[0])
               && ThemeCssValues.TryParseColor (new List<CssComponent> { group[0] }, out _, out _);

        // <color> [<position> [<position>]]: two positions are two stops of the same colour (a hard band).
        private static bool TryParseStop (CssFunction fn, List<CssComponent> group, ControlGradientKind kind,
            List<(Func<SKColor>, double, ControlGradient.StopUnit)> stops, out string? error)
        {
            error = null;

            if (group.Count == 0) {
                error = $"'{fn.Raw}': a colour stop is empty.";
                return false;
            }

            if (!ThemeCssValues.TryParseColor (new List<CssComponent> { group[0] }, out var color, out var colorError)) {
                error = group.Count == 1 && (group[0] is CssNumber || ThemeCssCalc.IsMathFunction (group[0]))
                    ? $"'{fn.Raw}': '{group[0].Raw}' on its own is a transition hint, which is not supported; give every stop a colour."
                    : $"'{fn.Raw}': {colorError}";
                return false;
            }

            if (group.Count > 3) {
                error = $"'{fn.Raw}': a colour stop is a colour and at most two positions.";
                return false;
            }

            if (group.Count == 1) {
                stops.Add ((color, 0, ControlGradient.StopUnit.Auto));
                return true;
            }

            for (var i = 1; i < group.Count; i++) {
                if (!TryParseStopPosition (group[i], kind, out var position, out var unit, out var positionError)) {
                    error = $"'{fn.Raw}': {positionError}";
                    return false;
                }
                stops.Add ((color, position, unit));
            }

            return true;
        }

        private static bool TryParseStopPosition (CssComponent component, ControlGradientKind kind, out double position, out ControlGradient.StopUnit unit, out string? error)
        {
            position = 0;
            unit = ControlGradient.StopUnit.Auto;

            if (!ThemeCssCalc.TryCompile (component, null, out var value, out error))
                return false;

            switch (value.Unit) {
                case CalcUnit.Percent:
                    unit = ControlGradient.StopUnit.Percent;
                    break;
                case CalcUnit.Length when kind != ControlGradientKind.Conic:
                    unit = ControlGradient.StopUnit.Length;
                    break;
                case CalcUnit.Number when value.IsConstant && value.Evaluate () == 0:
                    unit = kind == ControlGradientKind.Conic ? ControlGradient.StopUnit.Angle : ControlGradient.StopUnit.Length;
                    break;
                case CalcUnit.Angle when kind == ControlGradientKind.Conic:
                    unit = ControlGradient.StopUnit.Angle;
                    break;
                default:
                    error = kind == ControlGradientKind.Conic
                        ? $"'{component.Raw}': a conic gradient's stop positions are angles or percentages."
                        : $"'{component.Raw}': a stop position is a length or a percentage.";
                    return false;
            }

            // Positions are fixed when the theme is applied; a rem or a token in one is read then.
            position = value.Evaluate ();
            error = null;
            return true;
        }

        // <angle> | to [left | right] || [top | bottom]
        private static bool TryParseLinearDirection (CssFunction fn, List<CssComponent> group, out double angle, out (bool, bool, bool, bool)? corner, out string? error)
        {
            angle = 180;
            corner = null;
            error = null;

            if (group.Count == 1 && ThemeCssCalc.TryCompile (group[0], null, out var value, out _) && (value.Unit == CalcUnit.Angle || (value.Unit == CalcUnit.Number && value.Evaluate () == 0))) {
                angle = value.Evaluate ();
                return true;
            }

            if (group.Count is 2 or 3 && group[0] is CssIdent { Name: var to } && to.Equals ("to", StringComparison.OrdinalIgnoreCase)) {
                bool top = false, bottom = false, left = false, right = false;

                foreach (var component in group.Skip (1)) {
                    switch ((component as CssIdent)?.Name.ToLowerInvariant ()) {
                        case "top": top = true; break;
                        case "bottom": bottom = true; break;
                        case "left": left = true; break;
                        case "right": right = true; break;
                        default:
                            error = $"'{fn.Raw}': 'to' is followed by a side or a corner -- top, bottom, left, right, or e.g. 'to top right'.";
                            return false;
                    }
                }

                if ((top && bottom) || (left && right)) {
                    error = $"'{fn.Raw}': a direction cannot point two opposite ways.";
                    return false;
                }

                if (group.Count == 2) {
                    angle = top ? 0 : right ? 90 : bottom ? 180 : 270;
                    return true;
                }

                corner = (top, bottom, left, right);
                return true;
            }

            error = $"'{fn.Raw}': '{ThemeCssValues.Join (group)}' is not a direction; write an angle (45deg, 0.25turn) or 'to right', 'to bottom left'.";
            return false;
        }

        private struct RadialShape
        {
            public RadialShape () { }

            public bool Circle;
            public ControlGradient.RadialSize Size = ControlGradient.RadialSize.FarthestCorner;
            public ControlGradient.Offset SizeX;
            public ControlGradient.Offset SizeY;
            public ControlGradient.Offset X = ControlGradient.Offset.Center;
            public ControlGradient.Offset Y = ControlGradient.Offset.Center;
        }

        // [circle | ellipse] || [<size-keyword> | <length> | <length-percentage>{2}] [at <position>]
        private static bool TryParseRadialShape (CssFunction fn, List<CssComponent> group, ref RadialShape shape, out string? error)
        {
            error = null;
            var at = group.FindIndex (c => c is CssIdent { Name: var word } && word.Equals ("at", StringComparison.OrdinalIgnoreCase));
            var head = at < 0 ? group : group.Take (at).ToList ();
            bool? circle = null;
            var sizes = new List<CssComponent> ();

            foreach (var component in head) {
                switch ((component as CssIdent)?.Name.ToLowerInvariant ()) {
                    case "circle": circle = true; continue;
                    case "ellipse": circle = false; continue;
                    case "closest-side": shape.Size = ControlGradient.RadialSize.ClosestSide; continue;
                    case "closest-corner": shape.Size = ControlGradient.RadialSize.ClosestCorner; continue;
                    case "farthest-side": shape.Size = ControlGradient.RadialSize.FarthestSide; continue;
                    case "farthest-corner": shape.Size = ControlGradient.RadialSize.FarthestCorner; continue;
                }

                sizes.Add (component);
            }

            if (sizes.Count > 0) {
                var offsets = new List<ControlGradient.Offset> ();

                foreach (var component in sizes) {
                    if (!TryParseOffset (component, out var offset, out _)) {
                        error = $"'{fn.Raw}': '{component.Raw}' is not a radial gradient size; write a length (circle), two lengths or percentages (ellipse), or closest-side, closest-corner, farthest-side, farthest-corner.";
                        return false;
                    }
                    offsets.Add (offset);
                }

                circle ??= offsets.Count == 1;

                if (circle.Value && (offsets.Count != 1 || !offsets[0].IsLength)) {
                    error = $"'{fn.Raw}': a circle's size is one length (not a percentage).";
                    return false;
                }

                if (!circle.Value && offsets.Count != 2) {
                    error = $"'{fn.Raw}': an ellipse's size is two lengths or percentages, horizontal then vertical.";
                    return false;
                }

                shape.Size = ControlGradient.RadialSize.Explicit;
                shape.SizeX = offsets[0];
                shape.SizeY = offsets.Count > 1 ? offsets[1] : offsets[0];
            }

            shape.Circle = circle ?? false;

            if (at >= 0 && !TryParsePosition (fn, group.Skip (at + 1).ToList (), out shape.X, out shape.Y, out error))
                return false;

            return true;
        }

        // [from <angle>] [at <position>]
        private static bool TryParseConicShape (CssFunction fn, List<CssComponent> group, out double from, out ControlGradient.Offset x, out ControlGradient.Offset y, out string? error)
        {
            from = 0;
            x = ControlGradient.Offset.Center;
            y = ControlGradient.Offset.Center;
            error = null;
            var position = 0;

            if (position < group.Count && group[position] is CssIdent { Name: var fromWord } && fromWord.Equals ("from", StringComparison.OrdinalIgnoreCase)) {
                if (position + 1 >= group.Count || !ThemeCssCalc.TryCompile (group[position + 1], null, out var angle, out _) || angle.Unit != CalcUnit.Angle) {
                    error = $"'{fn.Raw}': 'from' is followed by an angle, e.g. 'from 90deg'.";
                    return false;
                }
                from = angle.Evaluate ();
                position += 2;
            }

            if (position < group.Count) {
                if (group[position] is not CssIdent { Name: var atWord } || !atWord.Equals ("at", StringComparison.OrdinalIgnoreCase)) {
                    error = $"'{fn.Raw}': expected 'from <angle>' and/or 'at <position>', found '{group[position].Raw}'.";
                    return false;
                }
                return TryParsePosition (fn, group.Skip (position + 1).ToList (), out x, out y, out error);
            }

            return true;
        }

        // <position>: one or two of left | center | right | top | bottom | <length> | <percentage>.
        private static bool TryParsePosition (CssFunction fn, List<CssComponent> group, out ControlGradient.Offset x, out ControlGradient.Offset y, out string? error)
        {
            x = ControlGradient.Offset.Center;
            y = ControlGradient.Offset.Center;
            error = null;

            if (group.Count is < 1 or > 2) {
                error = $"'{fn.Raw}': 'at' is followed by a position: one or two of left, center, right, top, bottom, a length or a percentage.";
                return false;
            }

            ControlGradient.Offset? horizontal = null, vertical = null;
            var unassigned = new List<ControlGradient.Offset> ();

            foreach (var component in group) {
                switch ((component as CssIdent)?.Name.ToLowerInvariant ()) {
                    case "left": horizontal = new ControlGradient.Offset (0, false); continue;
                    case "right": horizontal = new ControlGradient.Offset (1, false); continue;
                    case "top": vertical = new ControlGradient.Offset (0, false); continue;
                    case "bottom": vertical = new ControlGradient.Offset (1, false); continue;
                    case "center": unassigned.Add (ControlGradient.Offset.Center); continue;
                }

                if (!TryParseOffset (component, out var offset, out _)) {
                    error = $"'{fn.Raw}': '{component.Raw}' is not a position.";
                    return false;
                }

                unassigned.Add (offset);
            }

            // Lengths and 'center' fill whichever axis a keyword did not, horizontal first, as CSS reads <position>.
            foreach (var offset in unassigned) {
                if (horizontal is null)
                    horizontal = offset;
                else if (vertical is null)
                    vertical = offset;
                else {
                    error = $"'{fn.Raw}': the position names one axis twice.";
                    return false;
                }
            }

            x = horizontal ?? ControlGradient.Offset.Center;
            y = vertical ?? ControlGradient.Offset.Center;
            return true;
        }

        private static bool TryParseOffset (CssComponent component, out ControlGradient.Offset offset, out string? error)
        {
            offset = ControlGradient.Offset.Center;

            if (!ThemeCssCalc.TryCompile (component, null, out var value, out error))
                return false;

            switch (value.Unit) {
                case CalcUnit.Percent:
                    offset = new ControlGradient.Offset (value.Evaluate () / 100, false);
                    return true;
                case CalcUnit.Length:
                case CalcUnit.Number when value.IsConstant && value.Evaluate () == 0:
                    offset = new ControlGradient.Offset (value.Evaluate (), true);
                    return true;
                default:
                    error = $"'{component.Raw}' is not a length or a percentage.";
                    return false;
            }
        }
    }
}
