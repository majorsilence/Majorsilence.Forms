using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// PreferredHeight is a LOGICAL size -- the height a control is laid out at -- but every implementation measured "Wg" at the
// DEVICE font size and added logical padding, so at scale 2 it came out nearly doubled. A ToolStripTextBox that sizes itself
// from it was then 42px tall in a 40px row on Windows at scale 2 (PR #410), and an unsized TextBox or NumericUpDown in a
// container laid out at twice its height.
//
// The assertion is a relationship -- the same control at UiScale 1 and 2 -- never an absolute height, which the ambient font
// decides per platform. UiScale multiplies into the gate's own scale, so every CI shape sees the doubling.
[Collection ("Headless")]
public sealed class PreferredHeightScaleTests : IDisposable
{
    private readonly double original = Application.UiScale;

    public void Dispose () => Application.UiScale = original;

    private static int PreferredHeightAt (double scale, Func<Control> make, Func<Control, int> read)
    {
        HeadlessRenderer.Use ();
        Application.UiScale = scale;

        using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (400, 200) };
        var control = make ();
        form.Controls.Add (control);
        form.Show ();

        return read (control);
    }

    public static TheoryData<string> Kinds => new () { "TextBox", "RichTextBox", "NumericUpDown", "ComboBox", "DateTimePicker" };

    private static (Func<Control> Make, Func<Control, int> Read) For (string kind)
        => kind switch {
            "TextBox" => (() => new TextBox (), c => ((TextBox) c).PreferredHeight),
            "RichTextBox" => (() => new RichTextBox (), c => ((RichTextBox) c).PreferredHeight),
            "NumericUpDown" => (() => new NumericUpDown (), c => ((NumericUpDown) c).PreferredHeight),
            "ComboBox" => (() => new ComboBox (), c => ((ComboBox) c).PreferredHeight),
            "DateTimePicker" => (() => new DateTimePicker (), c => ((DateTimePicker) c).PreferredHeight),
            _ => throw new ArgumentException (kind),
        };

    [Theory]
    [MemberData (nameof (Kinds))]
    public void PreferredHeight_is_logical_and_does_not_grow_with_the_display_scale (string kind)
    {
        var (make, read) = For (kind);

        var single = PreferredHeightAt (1, make, read);
        var doubled = PreferredHeightAt (2, make, read);

        // One pixel of rounding either way: the text is measured at the device size and converted back.
        Assert.InRange (doubled, single - 1, single + 1);
    }
}
