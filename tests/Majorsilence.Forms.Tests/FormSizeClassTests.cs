using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

[Collection ("Headless")]
public class FormSizeClassTests
{
    [Theory]
    [InlineData (0, WindowSizeClass.Compact)]
    [InlineData (599, WindowSizeClass.Compact)]
    [InlineData (600, WindowSizeClass.Medium)]
    [InlineData (839, WindowSizeClass.Medium)]
    [InlineData (840, WindowSizeClass.Expanded)]
    [InlineData (2000, WindowSizeClass.Expanded)]
    public void ClassForWidth_uses_the_Material_breakpoints (int width, WindowSizeClass expected)
        => Assert.Equal (expected, Form.ClassForWidth (width));

    [Fact]
    public void SizeClass_follows_the_client_width ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (400, 700) };
        form.Show ();
        Assert.Equal (WindowSizeClass.Compact, form.SizeClass);

        form.ClientSize = new Size (1000, 700);
        Assert.Equal (WindowSizeClass.Expanded, form.SizeClass);
    }

    // The resize notifications are raised by the render pass that picks up the new size, so a test has to
    // draw a frame after each resize, as the real backend does.
    private static void Resize (Form form, int width, int height)
    {
        form.ClientSize = new Size (width, height);
        HeadlessRenderer.CapturePng (form);
    }

    [Fact]
    public void SizeClassChanged_fires_only_when_the_class_changes ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (400, 700) };
        form.Show ();

        var changes = 0;
        form.SizeClassChanged += (_, _) => changes++;

        Resize (form, 450, 700);   // still Compact
        Assert.Equal (0, changes);

        Resize (form, 700, 700);   // Compact -> Medium
        Assert.Equal (1, changes);

        Resize (form, 720, 700);   // still Medium
        Assert.Equal (1, changes);

        Resize (form, 1000, 700);  // Medium -> Expanded
        Assert.Equal (2, changes);
    }
}
