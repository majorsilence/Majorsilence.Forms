using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;

// See the .csproj header. Exits 0 on success, 1 on any failure -- the `aot-smoke` CI job publishes
// this with PublishAot=true and runs the native binary.

try
{
    HeadlessRenderer.Use ();

    using var form = new Form { Text = "AOT smoke", Size = new Size (240, 200) };

    form.Controls.Add (new Label { Text = "Hello from NativeAOT", Left = 8, Top = 8, Width = 200, Height = 24 });
    form.Controls.Add (new Button { Text = "OK", Left = 8, Top = 40, Width = 80, Height = 28 });

    var tree = new TreeView { Left = 8, Top = 76, Width = 200, Height = 100 };
    for (var i = 0; i < 12; i++)
        tree.Nodes.Add ($"Node {i}");
    form.Controls.Add (tree);

    form.Show ();

    var png = HeadlessRenderer.CapturePng (form, 240, 200);

    if (png.Length < 200)
        return Fail ($"PNG is implausibly small ({png.Length} bytes) -- the scene did not render.");

    // PNG signature.
    ReadOnlySpan<byte> sig = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
    if (!png.AsSpan (0, 8).SequenceEqual (sig))
        return Fail ("Output is not a PNG.");

    // A blank fill compresses to almost nothing; a real rendered form with text and a tree does not.
    if (png.Length < 1500)
        return Fail ($"PNG is only {png.Length} bytes -- looks blank, the controls probably did not paint.");

    Console.WriteLine ($"AOT smoke OK: rendered a {png.Length}-byte PNG with a Label, Button and TreeView.");
    return 0;
}
catch (Exception ex)
{
    return Fail (ex.ToString ());
}

static int Fail (string message)
{
    Console.Error.WriteLine ($"AOT smoke FAILED: {message}");
    return 1;
}
