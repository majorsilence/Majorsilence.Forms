using System.Drawing;
using BenchmarkDotNet.Attributes;
using Majorsilence.Forms.Headless;

namespace Majorsilence.Forms.Benchmarks;

// A CSS transition runs once per display frame for as long as it lasts, on every control that is animating, so a frame of one must not
// allocate: the in-between style and the frame callback are made once when the transition starts and reused (Control.StyleTransitions.cs).
// The transition here is long enough never to finish, and the benchmark steps the Headless clock one frame at a time, which runs exactly
// the transition's frame work -- interpolating the values and requesting the next frame -- without painting.
[MemoryDiagnoser]
public class TransitionBenchmarks
{
    private Form form = null!;
    private Button button = null!;

    [GlobalSetup]
    public void Setup ()
    {
        Theme.LoadFromCss (@"
            Button { background-color: #000000; color: #ffffff; box-shadow: 4px 4px #202020; border-radius: 4px;
                     transition: background-color 100000s linear, color 100000s, box-shadow 100000s, border-radius 100000s; }
            Button:hover { background-color: #ffffff; color: #000000; box-shadow: 0px 0px #202020; border-radius: 12px; }");

        form = new Form { ClientSize = new Size (300, 200) };
        button = new Button { Location = new Point (20, 20), Size = new Size (120, 40) };
        form.Controls.Add (button);
        form.Show ();
        Frame.Render (form, 300, 200);

        button.RaiseMouseEnter (new MouseEventArgs (MouseButtons.None, 0, 5, 5, Point.Empty));
        Frame.Render (form, 300, 200);
        HeadlessRenderer.AnimationClock.Step ();

        // A gate that measures a transition that never started would pass at 0 B for the wrong reason.
        if (!button.IsStyleTransitionRunning)
            throw new InvalidOperationException ("The hover transition did not start.");
    }

    [GlobalCleanup]
    public void Cleanup ()
    {
        form.Dispose ();
        Theme.SetBuiltInTheme (BuiltInTheme.Light);
    }

    [Benchmark]
    [BenchmarkCategory (AllocationGate.Category)]
    public void HoverTransitionFrame () => HeadlessRenderer.AnimationClock.Step ();
}
