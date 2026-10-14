using System;
using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;

namespace Majorsilence.Forms.Drawing.Common.Tests
{
    // A Region made from one RectangleF keeps the rectangle and makes no SKRegion or SKPath until something needs
    // one (performance plan F14): report engines make one per measured range and only read its bounds. Every answer
    // must be the one the region gave when the constructor made the native form at once. The oracle is the same
    // region with its native form forced (IsShaped makes it), compared bit for bit.
    public class RegionPendingRectangleTests
    {
        public static IEnumerable<object[]> Rectangles ()
        {
            var rects = new List<RectangleF> {
                new (0, 0, 10, 10),
                new (5, 7, 120, 15),                              // integral
                new (0.3f, 0.7f, 120.25f, 15.333333f),           // fractional
                new (10.25f, 3.5f, 90f, 15.335938f),
                new (-40.5f, -3.25f, 20.125f, 4.75f),            // negative position
                new (0, 0, 0, 10),                                // no width
                new (0, 0, 10, 0),
                new (5, 5, -10, 10),                              // negative size, integral and not
                new (5.5f, 5.5f, -10f, 10f),
                new (0.5f, 0.5f, 1e-6f, 10f),                     // slivers: left to the native form
                new (1000.5f, 0, 0.001f, 10f),
                new (16_000_000.5f, 0, 1f, 1f),                   // past float precision for halves
                new (0, 0, 1 << 28, 1 << 28),                     // the infinite extent
                new (float.NaN, 0, 10, 10),
                new (0, 0, float.PositiveInfinity, 10),
                new (1e20f, 0, 10, 10),
            };
            foreach (var rect in rects)
                yield return [rect.X, rect.Y, rect.Width, rect.Height];
        }

        [Theory]
        [MemberData (nameof (Rectangles))]
        public void Bounds_and_emptiness_match_the_native_form (float x, float y, float width, float height)
        {
            var rect = new RectangleF (x, y, width, height);
            using var lazy = new Region (rect);
            using var native = new Region (rect);
            _ = native.IsShaped;

            Assert.Equal (Bits (native.GetBounds ()), Bits (lazy.GetBounds ()));
            Assert.Equal (native.IsEmpty (), lazy.IsEmpty ());
        }

        [Theory]
        [MemberData (nameof (Rectangles))]
        public void Every_other_operation_matches_the_native_form (float x, float y, float width, float height)
        {
            var rect = new RectangleF (x, y, width, height);
            if (float.IsNaN (x) || float.IsInfinity (width) || Math.Abs (x) > 1e10f)
                return;   // the native form's own behaviour there is not this test's business

            var operations = new (string Name, Action<Region> Apply)[] {
                ("union", r => r.Union (new RectangleF (2.5f, 3, 40, 8))),
                ("intersect", r => r.Intersect (new Rectangle (0, 0, 50, 12))),
                ("exclude", r => r.Exclude (new RectangleF (1, 1, 3.5f, 3))),
                ("xor", r => r.Xor (new Rectangle (4, 4, 10, 10))),
                ("complement", r => r.Complement (new Rectangle (-5, -5, 200, 40))),
                ("translate int", r => r.Translate (3, -2)),
                ("translate float", r => r.Translate (0.5f, 0.25f)),
                ("transform", r => { using var m = new Matrix (); m.Scale (2, 0.5f); r.Transform (m); }),
                ("union region", r => { using var other = new Region (new RectangleF (1.5f, 2, 6, 6)); r.Union (other); }),
                ("into infinite", r => { using var inf = new Region (); inf.Intersect (r); r.Intersect (inf); }),
                ("nothing", _ => { }),
            };

            foreach (var (name, apply) in operations) {
                using var lazy = new Region (rect);
                using var native = new Region (rect);
                _ = native.IsShaped;

                apply (lazy);
                apply (native);

                Assert.True (Bits (native.GetBounds ()) == Bits (lazy.GetBounds ()), $"{name}: bounds {lazy.GetBounds ()}, native {native.GetBounds ()}");
                Assert.True (native.IsEmpty () == lazy.IsEmpty (), $"{name}: emptiness");
                Assert.Equal (native.GetRegionScans (null), lazy.GetRegionScans (null));
                Assert.True (native.IsVisible (6, 9) == lazy.IsVisible (6, 9), $"{name}: IsVisible");
                Assert.True (native.IsVisible (new RectangleF (3, 3, 2, 2)) == lazy.IsVisible (new RectangleF (3, 3, 2, 2)), $"{name}: IsVisible rect");
                Assert.True (native.IsShaped == lazy.IsShaped, $"{name}: integral or shaped");

                using var clone = new Region (rect).Clone ();
                Assert.Equal (Bits (new Region (rect).GetBounds ()), Bits (clone.GetBounds ()));
            }
        }

        [Fact]
        public void Reading_bounds_makes_no_native_object ()
        {
            using var region = new Region (new RectangleF (0.5f, 1.25f, 120, 15.333333f));

            region.GetBounds ();
            region.IsEmpty ();

            Assert.True (region.IsPending);
        }

        private static (int, int, int, int) Bits (RectangleF r)
            => (BitConverter.SingleToInt32Bits (r.X), BitConverter.SingleToInt32Bits (r.Y), BitConverter.SingleToInt32Bits (r.Width), BitConverter.SingleToInt32Bits (r.Height));
    }
}
