using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// EVT-10 to EVT-13: the form lifecycle in upstream's order. Show raised VisibleChanged and Activated
// before Load, and Shown inline inside Show (); Close skipped Deactivate; and Controls.Add raised a
// child's VisibleChanged twice. Upstream: Form.SetVisibleCore raises OnLoad before base.SetVisibleCore
// (so VisibleChanged, then activation, follow Load), OnLoad posts Shown with BeginInvoke, and an active
// window's DestroyWindow delivers WM_ACTIVATE (WA_INACTIVE) between FormClosed and WM_DESTROY.
[Collection ("Headless")]
public class FormLifecycleOrderTests
{
    private static (Form form, EventRecorder recorder) Recorded ()
    {
        HeadlessRenderer.Use ();
        var form = new Form { Width = 300, Height = 200 };
        var recorder = EventRecorder.For (form, "HandleCreated", "Load", "VisibleChanged", "Activated", "Shown",
            "FormClosing", "FormClosed", "Deactivate", "HandleDestroyed");
        return (form, recorder);
    }

    [Fact]
    public void Show_raises_HandleCreated_Load_VisibleChanged_Activated_then_a_posted_Shown ()
    {
        var (form, recorder) = Recorded ();

        using (form)
        using (recorder) {
            bool? visibleDuringLoad = null;
            form.Load += (_, _) => visibleDuringLoad = form.Visible;

            form.Show ();
            recorder.AssertSequence ("HandleCreated", "Load", "VisibleChanged", "Activated");   // Shown not yet

            Application.DoEvents ();
            recorder.AssertSequence ("HandleCreated", "Load", "VisibleChanged", "Activated", "Shown");

            Assert.False (visibleDuringLoad);   // upstream: Visible is set after Load

            form.Close ();
        }
    }

    [Fact]
    public void Closing_an_active_form_deactivates_it_between_FormClosed_and_HandleDestroyed ()
    {
        var (form, recorder) = Recorded ();

        using (form)
        using (recorder) {
            form.Show ();
            Application.DoEvents ();
            recorder.Clear ();

            form.Close ();

            recorder.AssertSequence ("FormClosing", "VisibleChanged", "FormClosed", "Deactivate", "HandleDestroyed");
        }
    }

    [Fact]
    public void A_Show_from_a_Load_handler_does_not_start_a_second_show ()
    {
        // Visible is false during Load now, so Show () inside Load must not take that as "not shown yet".
        var (form, recorder) = Recorded ();

        using (form)
        using (recorder) {
            form.Load += (_, _) => form.Show ();

            form.Show ();
            Application.DoEvents ();

            Assert.Equal (1, recorder.Count ("Load"));
            Assert.Equal (1, recorder.Count ("Shown"));

            // And no second backend window: one form raising two surfaces splits input from painting.
            Assert.Equal (1, ((Majorsilence.Forms.Headless.HeadlessWindowHost) form.Backend).ShowCount);
            form.Close ();
        }
    }

    [Fact]
    public void Adding_a_child_raises_no_VisibleChanged ()
    {
        // Was "once" (EVT-13 removed the second raise); CTL-14 removed the first. An unparented
        // control is already visible, so upstream's AssignParent sees no change and raises nothing.
        HeadlessRenderer.Use ();
        using var form = new Form ();
        var child = new Button ();
        using var recorder = EventRecorder.For (child, "VisibleChanged");

        form.Controls.Add (child);

        Assert.Equal (0, recorder.Count ("VisibleChanged"));
    }
}
