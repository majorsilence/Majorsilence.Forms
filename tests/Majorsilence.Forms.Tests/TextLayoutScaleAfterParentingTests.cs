using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// A control's text is laid out (and cached) using the display scale of its window. A control that is parented to a container with no window
// yet has no window to ask, so it reads scale 1; text laid out at that moment used to stay at scale 1 for good, and came out half the size on
// a scale-2 display. Composing a view in a constructor (make a panel, add controls, set their text, then add the panel to a form) is the
// ordinary way to build a screen, so this is the common path, not an edge.
//
// Every assertion is a RELATIONSHIP between two ways of building the same control, never an absolute pixel count (the ambient font measures
// differently per platform). Application.UiScale is set to 2 in the constructor, which multiplies into whatever the gate's own scale is, so
// the bug is visible in all four CI gates and not only the MF_HEADLESS_SCALE=2 one.
public sealed class TextLayoutScaleAfterParentingTests : IDisposable
{
    private readonly double original = Application.UiScale;

    public TextLayoutScaleAfterParentingTests () => Application.UiScale = 2;

    public void Dispose () => Application.UiScale = original;

    // The picture is 600x400 because at UiScale 2 the controls sit at twice their logical coordinates, below a title bar, and a smaller capture
    // would leave the text off the bottom of it (the first version of this test measured nothing for that reason).
    //
    // The vertical extent, in device pixels, of the dark ink in the picture: the height of the text (plus any dark border, identical in both
    // builds being compared).
    private static int InkHeight (Form form)
    {
        var png = HeadlessRenderer.CapturePng (form, 600, 400);
        using var bitmap = SKBitmap.Decode (png);

        int top = int.MaxValue, bottom = -1;
        for (var y = 0; y < bitmap.Height; y++) {
            for (var x = 0; x < bitmap.Width; x++) {
                var c = bitmap.GetPixel (x, y);
                if (c.Red < 110 && c.Green < 110 && c.Blue < 110) {
                    top = Math.Min (top, y);
                    bottom = Math.Max (bottom, y);
                }
            }
        }

        return bottom < 0 ? 0 : bottom - top + 1;
    }

    private static Control Make (string kind)
        => kind switch {
            "TextBox" => new TextBox { Left = 10, Top = 60, Width = 200, Height = 28 },
            "Label" => new Label { Left = 10, Top = 60, Width = 200, Height = 28, AutoSize = false },
            "Button" => new Button { Left = 10, Top = 60, Width = 200, Height = 32 },
            "CheckBox" => new CheckBox { Left = 10, Top = 60, Width = 200, Height = 28 },
            "RichTextBox" => new RichTextBox { Left = 10, Top = 60, Width = 200, Height = 40 },
            _ => throw new ArgumentException (kind),
        };

    // Text assigned BEFORE the control is parented: the reference, which was always right.
    private static int TextFirst (string kind)
    {
        var form = new Form ();
        var control = Make (kind);
        control.Text = "Hxg";
        form.Controls.Add (control);
        return InkHeight (form);
    }

    // Text assigned AFTER the control is parented to a panel that is not on a form yet, then the panel added to the form.
    private static int TextAfterParentingToALoosePanel (string kind)
    {
        var form = new Form ();
        var panel = new Panel { Left = 0, Top = 0, Width = 280, Height = 150 };
        var control = Make (kind);
        panel.Controls.Add (control);
        control.Text = "Hxg";
        form.Controls.Add (panel);
        return InkHeight (form);
    }

    // Text assigned after the control is parented straight to a form: a window exists, so this always worked.
    private static int TextAfterParentingToTheForm (string kind)
    {
        var form = new Form ();
        var control = Make (kind);
        form.Controls.Add (control);
        control.Text = "Hxg";
        return InkHeight (form);
    }

    [Theory]
    [InlineData ("TextBox")]
    [InlineData ("Label")]
    [InlineData ("Button")]
    [InlineData ("CheckBox")]
    [InlineData ("RichTextBox")]
    public void TextAssignedAfterParentingToAWindowlessPanel_IsTheSameSizeAsTextAssignedFirst (string kind)
    {
        var reference = TextFirst (kind);
        var afterParenting = TextAfterParentingToALoosePanel (kind);

        Assert.True (reference > 0, $"{kind}: the reference build drew no text at all, so the comparison means nothing");
        Assert.True (reference == afterParenting, $"{kind}: text assigned first drew {reference} px of ink, but text assigned after parenting to a windowless panel drew {afterParenting} px");
    }

    [Theory]
    [InlineData ("TextBox")]
    [InlineData ("Label")]
    [InlineData ("Button")]
    [InlineData ("CheckBox")]
    [InlineData ("RichTextBox")]
    public void TextAssignedAfterParentingToTheForm_IsTheSameSizeAsTextAssignedFirst (string kind)
    {
        // Not the reported bug (a window exists here) but its control case: it shows the comparison itself is sound, and passes with or
        // without the fix.
        var reference = TextFirst (kind);
        var afterParenting = TextAfterParentingToTheForm (kind);
        Assert.True (reference == afterParenting, $"{kind}: text first drew {reference} px, text after parenting to the form drew {afterParenting} px");
    }
}
