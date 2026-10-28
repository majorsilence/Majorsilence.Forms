using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>The backgrounds an appointment can be drawn with, by <see cref="Appointment.BackgroundId"/>. Compat for Telerik's <c>AppointmentBackground</c>.</summary>
    public enum AppointmentBackground
    {
        /// <summary>No category: the default pale blue gradient.</summary>
        None = 1,
        /// <summary>Yellow.</summary>
        Important = 2,
        /// <summary>Purple.</summary>
        Business = 3,
        /// <summary>Green.</summary>
        Personal = 4,
        /// <summary>Pink.</summary>
        Vacation = 5,
        /// <summary>Blue.</summary>
        MustAttend = 6,
        /// <summary>Olive.</summary>
        TravelRequired = 7,
        /// <summary>Gray.</summary>
        NeedsPreparation = 8,
        /// <summary>Orange.</summary>
        Birthday = 9,
        /// <summary>Lavender.</summary>
        Anniversary = 10,
        /// <summary>Raspberry.</summary>
        PhoneCall = 11,
    }

    /// <summary>The availability an appointment shows, by <see cref="Appointment.StatusId"/>. Compat for Telerik's <c>AppointmentStatus</c>.</summary>
    public enum AppointmentStatus
    {
        /// <summary>Free: a green status strip.</summary>
        Free = 1,
        /// <summary>Busy: the default, which draws no status strip.</summary>
        Busy = 2,
        /// <summary>Unavailable: an amber status strip.</summary>
        Unavailable = 3,
        /// <summary>Tentative: a blue-hatched status strip.</summary>
        Tentative = 4,
    }

    // How an appointment is painted from its BackgroundId and StatusId: the colours of Telerik's default
    // Backgrounds and Statuses collections (measured on UI.for.WinForms 2026.2.520), and its layout -- a one-pixel
    // border, then for a status other than Busy a five-pixel strip in the status colour and a one-pixel border-coloured
    // line before the background. Busy draws neither strip nor line; an id outside the status set draws the line with
    // the background showing through the strip.
    internal static class AppointmentStyles
    {
        internal const int StripWidth = 5;

        internal static readonly Color BorderColor = Color.FromArgb (110, 153, 210);

        private static readonly Color[] backgrounds = [
            Color.FromArgb (232, 241, 252),     // None (a gradient down to the next colour)
            Color.FromArgb (255, 216, 107),     // Important
            Color.FromArgb (211, 187, 244),     // Business
            Color.FromArgb (142, 245, 196),     // Personal
            Color.FromArgb (248, 184, 245),     // Vacation
            Color.FromArgb (209, 226, 247),     // MustAttend
            Color.FromArgb (187, 232, 161),     // TravelRequired
            Color.FromArgb (219, 219, 219),     // NeedsPreparation
            Color.FromArgb (255, 186, 102),     // Birthday
            Color.FromArgb (204, 204, 255),     // Anniversary
            Color.FromArgb (232, 174, 212),     // PhoneCall
        ];

        internal static readonly Color NoneGradientEnd = Color.FromArgb (209, 226, 247);

        internal static readonly Color TentativeHatchBack = Color.White;

        /// <summary>The background colour an id is drawn with; an unknown id is drawn as <see cref="AppointmentBackground.None"/>.</summary>
        internal static Color Background (int backgroundId)
            => backgroundId >= 1 && backgroundId <= backgrounds.Length ? backgrounds[backgroundId - 1] : backgrounds[0];

        /// <summary>Whether the id is drawn with the <see cref="AppointmentBackground.None"/> gradient.</summary>
        internal static bool IsGradient (int backgroundId) => Background (backgroundId) == backgrounds[0];

        /// <summary>The status strip's colour, or null when the status draws no strip (Busy) or is not a known status.</summary>
        internal static Color? StatusColor (int statusId) => statusId switch {
            (int)AppointmentStatus.Free => Color.FromArgb (93, 174, 47),
            (int)AppointmentStatus.Unavailable => Color.FromArgb (199, 164, 2),
            (int)AppointmentStatus.Tentative => Color.FromArgb (1, 130, 229),
            _ => null,
        };

        /// <summary>Whether the status leaves room for a strip (every status but Busy).</summary>
        internal static bool HasStrip (int statusId) => statusId != (int)AppointmentStatus.Busy;

        /// <summary>Paints an appointment's block -- background, border and status strip -- and returns the area left for its text.</summary>
        internal static Rectangle Paint (SKCanvas canvas, Rectangle bounds, Appointment appointment, int scale)
        {
            var background = Background (appointment.BackgroundId);

            if (IsGradient (appointment.BackgroundId)) {
                using var paint = new SKPaint {
                    Shader = SKShader.CreateLinearGradient (new SKPoint (bounds.Left, bounds.Top), new SKPoint (bounds.Left, bounds.Bottom),
                        [background.ToSKColor (), NoneGradientEnd.ToSKColor ()], SKShaderTileMode.Clamp),
                };
                canvas.DrawRect (bounds.ToSKRect (), paint);
            } else {
                canvas.FillRectangle (bounds, background.ToSKColor ());
            }

            var border = Math.Max (1, scale);
            var text = Rectangle.FromLTRB (bounds.Left + border, bounds.Top + border, bounds.Right - border, bounds.Bottom - border);

            if (HasStrip (appointment.StatusId)) {
                var strip = new Rectangle (bounds.Left + border, bounds.Top + border, StripWidth * scale, Math.Max (0, bounds.Height - (2 * border)));

                if (StatusColor (appointment.StatusId) is { } status) {
                    if (appointment.StatusId == (int)AppointmentStatus.Tentative) {
                        canvas.FillRectangle (strip, TentativeHatchBack.ToSKColor ());
                        canvas.Save ();
                        canvas.ClipRect (strip.ToSKRect ());
                        using var hatch = new SKPaint { Color = status.ToSKColor (), StrokeWidth = scale, IsAntialias = true };
                        for (var y = strip.Top - strip.Width; y < strip.Bottom; y += 4 * scale)
                            canvas.DrawLine (strip.Left, y + strip.Width, strip.Right, y, hatch);
                        canvas.Restore ();
                    } else {
                        canvas.FillRectangle (strip, status.ToSKColor ());
                    }
                }

                canvas.FillRectangle (new Rectangle (strip.Right, strip.Top, border, strip.Height), BorderColor.ToSKColor ());
                text = Rectangle.FromLTRB (strip.Right + border, text.Top, text.Right, text.Bottom);
            }

            canvas.DrawRectangle (bounds, BorderColor.ToSKColor (), border);

            return text;
        }
    }
}
