using System;
using System.Drawing;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using Xunit;
using MSBrushes = Majorsilence.Forms.Drawing.Brushes;
using MSPens = Majorsilence.Forms.Drawing.Pens;
using MSPen = Majorsilence.Forms.Drawing.Pen;
using MSSolidBrush = Majorsilence.Forms.Drawing.SolidBrush;

namespace Majorsilence.Forms.Tests
{
    // Pens, Brushes, SystemPens and SystemBrushes hand out one instance per colour to the whole process. A change made
    // through one (Pens.Black.Width = 3) used to show up in every other caller's drawing, on every thread. GDI+ makes
    // them immutable and throws (dotnet/winforms, System.Drawing.Pen/SolidBrush _immutable); so does this layer now.
    public class SharedPenBrushImmutabilityTests
    {
        public static TheoryData<string, Action<MSPen>> PenChanges => new () {
            { "Width", p => p.Width = 3 },
            { "Color", p => p.Color = Color.Red },
            { "DashStyle", p => p.DashStyle = DashStyle.Dash },
            { "DashPattern", p => p.DashPattern = [1f, 2f] },
            { "StartCap", p => p.StartCap = LineCap.Round },
            { "SetLineCap", p => p.SetLineCap (LineCap.Round, LineCap.Round, DashCap.Round) },
            { "Brush", p => p.Brush = new MSSolidBrush (Color.Red) },
            { "Transform", p => p.ScaleTransform (2, 2) },
            { "ResetTransform", p => p.ResetTransform () },
        };

        [Theory]
        [MemberData (nameof (PenChanges))]
        public void A_shared_pen_cannot_be_changed (string _, Action<MSPen> change)
        {
            Assert.Throws<ArgumentException> (() => change (MSPens.Black));
            Assert.Throws<ArgumentException> (() => change (SystemPens.ControlText));

            Assert.Equal (1f, MSPens.Black.Width);
            Assert.Equal (Color.Black.ToArgb (), MSPens.Black.Color.ToArgb ());
            Assert.Equal (DashStyle.Solid, MSPens.Black.DashStyle);
        }

        [Theory]
        [MemberData (nameof (PenChanges))]
        public void A_clone_of_a_shared_pen_and_a_new_pen_can_be_changed (string _, Action<MSPen> change)
        {
            change (MSPens.Black.Clone ());
            change (new MSPen (Color.Black));
        }

        [Fact]
        public void A_shared_brush_cannot_be_changed ()
        {
            var red = (MSSolidBrush)MSBrushes.Red;

            Assert.Throws<ArgumentException> (() => red.Color = Color.Blue);
            Assert.Throws<ArgumentException> (() => SystemBrushes.Control.Color = Color.Blue);
            Assert.Equal (Color.Red.ToArgb (), red.Color.ToArgb ());
        }

        [Fact]
        public void A_clone_of_a_shared_brush_and_a_new_brush_can_be_changed ()
        {
            var clone = (MSSolidBrush)MSBrushes.Red.Clone ();
            clone.Color = Color.Blue;
            new MSSolidBrush (Color.Red).Color = Color.Blue;

            Assert.Equal (Color.Blue.ToArgb (), clone.Color.ToArgb ());
        }

        // GDI+ throws here too; this layer does not. Disposing frees nothing, so it cannot hurt other users of the pen,
        // and ported code that wraps a shared pen in a using block keeps working.
        [Fact]
        public void Disposing_a_shared_pen_or_brush_is_harmless ()
        {
            MSPens.Black.Dispose ();
            MSBrushes.Red.Dispose ();

            Assert.Equal (1f, MSPens.Black.Width);
        }
    }
}
