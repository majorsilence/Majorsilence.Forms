using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;

// See the .csproj header. Exits 0 on success, 1 on any failure -- the `aot-smoke` CI job publishes
// this with PublishAot=true and runs the native binary.

try
{
    HeadlessRenderer.Use ();

    using var form = new Form { Text = "AOT smoke", Size = new Size (240, 260) };

    form.Controls.Add (new Label { Text = "Hello from NativeAOT", Left = 8, Top = 8, Width = 200, Height = 24 });
    form.Controls.Add (new Button { Text = "OK", Left = 8, Top = 40, Width = 80, Height = 28 });

    var tree = new TreeView { Left = 8, Top = 76, Width = 200, Height = 100 };
    for (var i = 0; i < 12; i++)
        tree.Nodes.Add ($"Node {i}");
    form.Controls.Add (tree);

    // Data binding finds members by name at run time, which is exactly what trimming and ILC remove, so it is checked here in both
    // directions rather than assumed.
    var model = new SmokeViewModel ("first");
    var titleLabel = new Label { Left = 8, Top = 180, Width = 200, Height = 24 };
    titleLabel.DataBindings.Add ("Text", model, nameof (SmokeViewModel.Title));
    var nameBox = new TextBox { Left = 8, Top = 210, Width = 200, Height = 24 };
    nameBox.DataBindings.Add ("Text", model, nameof (SmokeViewModel.Name), false, DataSourceUpdateMode.OnPropertyChanged);
    form.Controls.Add (titleLabel);
    form.Controls.Add (nameBox);

    form.Show ();

    var problems = new List<string> ();
    if (titleLabel.Text != "first")
        problems.Add ($"the bound label did not read the view model: it shows '{titleLabel.Text}', expected 'first'");
    model.ChangeTitle ("second");
    if (titleLabel.Text != "second")
        problems.Add ($"a view-model change did not reach the bound label: it shows '{titleLabel.Text}', expected 'second'");
    nameBox.Text = "typed";
    if (model.CurrentName () != "typed")
        problems.Add ($"typing into the bound text box did not reach the view model: it holds '{model.CurrentName ()}', expected 'typed'");
    if (problems.Count > 0)
        return Fail ("Binding: " + string.Join ("; ", problems) + ".");

    var png = HeadlessRenderer.CapturePng (form, 240, 260);

    if (png.Length < 200)
        return Fail ($"PNG is implausibly small ({png.Length} bytes) -- the scene did not render.");

    // PNG signature.
    ReadOnlySpan<byte> sig = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
    if (!png.AsSpan (0, 8).SequenceEqual (sig))
        return Fail ("Output is not a PNG.");

    // A blank fill compresses to almost nothing; a real rendered form with text and a tree does not.
    if (png.Length < 1500)
        return Fail ($"PNG is only {png.Length} bytes -- looks blank, the controls probably did not paint.");

    Console.WriteLine ($"AOT smoke OK: rendered a {png.Length}-byte PNG with a Label, Button and TreeView, and a view model bound in both directions.");
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

// No bound property is called from this program: binding reaches them by name and nothing else does. That is the case trimming and
// ILC cannot see, and the usual one in a real view model (a read-only status text that only the UI ever reads).
sealed class SmokeViewModel : INotifyPropertyChanged
{
    private string title;
    private string name = "";

    public SmokeViewModel (string title) => this.title = title;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title {
        get => title;
        set => title = value;
    }

    public string Name {
        get => name;
        set => name = value;
    }

    public void ChangeTitle (string value)
    {
        title = value;
        PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (nameof (Title)));
    }

    public string CurrentName () => name;
}
