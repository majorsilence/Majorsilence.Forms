using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    [Collection ("Headless")]
    public class MdiClientWorkspaceTests
    {
        [Fact]
        public void The_workspace_behind_the_children_is_AppWorkspace ()
        {
            // As upstream's MdiClient paints it. The workspace used to be the theme's control colour, which
            // a child window's own face barely stands out from.
            var parent = new Form { IsMdiContainer = true };
            using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (parent, 600, 400));

            Assert.Equal (SystemColors.AppWorkspace.ToSKColor (), bitmap.GetPixel (bitmap.Width / 2, bitmap.Height * 2 / 3));
        }
    }
}
