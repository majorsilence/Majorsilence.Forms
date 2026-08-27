using System;
using Xunit;

namespace Majorsilence.Forms.Tests;

// The recorder is test infrastructure that later phases assert ordering with, so it gets its own
// tests: a sequence assertion that silently records nothing would turn every ordering test green.
public class EventRecorderTests
{
    [Fact]
    public void Records_events_in_the_order_they_are_raised ()
    {
        using var control = new Button ();
        using var recorder = EventRecorder.For (control, "TextChanged", "EnabledChanged");

        control.Text = "one";
        control.Enabled = false;
        control.Text = "two";

        recorder.AssertSequence ("TextChanged", "EnabledChanged", "TextChanged");
    }

    [Fact]
    public void Records_nothing_when_nothing_is_raised ()
    {
        using var control = new Button ();
        using var recorder = EventRecorder.For (control, "TextChanged");

        Assert.Empty (recorder.Entries);
    }

    [Fact]
    public void AssertSequence_fails_when_the_order_differs ()
    {
        using var control = new Button ();
        using var recorder = EventRecorder.For (control, "TextChanged", "EnabledChanged");

        control.Enabled = false;
        control.Text = "one";

        // The whole point of the helper: this is the failure a per-event test would miss.
        var failure = Assert.ThrowsAny<Exception> (
            () => recorder.AssertSequence ("TextChanged", "EnabledChanged"));

        Assert.Contains ("EnabledChanged -> TextChanged", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Labels_keep_two_sources_apart ()
    {
        using var first = new Button ();
        using var second = new Button ();

        using var recorder = EventRecorder.For (first, "TextChanged");
        recorder.Also (second, "b", "TextChanged");

        second.Text = "second";
        first.Text = "first";

        recorder.AssertSequence ("b.TextChanged", "TextChanged");
    }

    [Fact]
    public void AssertOrder_ignores_events_in_between ()
    {
        using var control = new Button ();
        using var recorder = EventRecorder.For (control, "TextChanged", "EnabledChanged");

        control.Text = "one";
        control.Enabled = false;
        control.Text = "two";

        recorder.AssertOrder ("TextChanged", "EnabledChanged");
        Assert.Equal (2, recorder.Count ("TextChanged"));
    }

    [Fact]
    public void Clear_forgets_what_was_recorded ()
    {
        using var control = new Button ();
        using var recorder = EventRecorder.For (control, "TextChanged");

        control.Text = "one";
        recorder.Clear ();
        control.Text = "two";

        recorder.AssertSequence ("TextChanged");
    }

    [Fact]
    public void Dispose_detaches_the_handlers ()
    {
        using var control = new Button ();
        var recorder = EventRecorder.For (control, "TextChanged");

        control.Text = "one";
        recorder.Dispose ();
        control.Text = "two";

        recorder.AssertSequence ("TextChanged");
    }

    [Fact]
    public void An_unknown_event_name_is_an_error_rather_than_a_silent_no_match ()
    {
        using var control = new Button ();

        Assert.Throws<ArgumentException> (() => EventRecorder.For (control, "NoSuchEvent"));
    }
}
