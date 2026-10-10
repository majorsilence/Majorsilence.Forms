using Xunit;

namespace Majorsilence.Forms.Tests;

// Android reports the keyboard opening and, when the window loses focus to another activity (a file picker), may never report it closing, so
// the form stays short by the keyboard's height. The host re-reads the input pane's own state on every layout; this is the decision it makes.
public class InputPaneResyncTests
{
    [Fact]
    public void A_keyboard_that_opened_and_is_now_reported_closed_is_cleared ()
    {
        var resync = new InputPaneResync ();
        resync.Changed (occludedHeight: 303);

        Assert.True (resync.ShouldClear (paneClosed: true));
    }

    [Fact]
    public void It_clears_only_once ()
    {
        var resync = new InputPaneResync ();
        resync.Changed (303);
        resync.ShouldClear (paneClosed: true);

        Assert.False (resync.ShouldClear (paneClosed: true));
    }

    [Fact]
    public void A_keyboard_that_is_really_open_is_left_alone ()
    {
        var resync = new InputPaneResync ();
        resync.Changed (303);

        Assert.False (resync.ShouldClear (paneClosed: false));
        Assert.False (resync.ShouldClear (paneClosed: false));
    }

    [Fact]
    public void With_no_keyboard_there_is_nothing_to_clear ()
    {
        var resync = new InputPaneResync ();

        Assert.False (resync.ShouldClear (paneClosed: true));
    }

    [Fact]
    public void A_keyboard_closed_through_its_own_event_is_not_cleared_a_second_time ()
    {
        var resync = new InputPaneResync ();
        resync.Changed (303);
        resync.Changed (0);

        Assert.False (resync.ShouldClear (paneClosed: true));
    }
}
