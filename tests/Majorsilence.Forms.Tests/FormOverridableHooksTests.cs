using System;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// WinForms' Form derives from Control and so inherits its overridable hooks. WindowBase does not, so
// every hook ported code overrides has to be declared explicitly -- and until it is, the override is a
// compile error (CS0115 "no suitable method found to override"), which is how these were found: a real
// app's main window overrode OnLostFocus, OnMove, OnDragEnter and OnDragDrop and none of them existed.
public class FormOverridableHooksTests
{
    private sealed class HookForm : Form
    {
        public int MoveCount;
        public int GotFocusCount;
        public int LostFocusCount;
        public int DragEnterCount;
        public int DragDropCount;

        protected override void OnMove (EventArgs e) { MoveCount++; base.OnMove (e); }
        protected override void OnGotFocus (EventArgs e) { GotFocusCount++; base.OnGotFocus (e); }
        protected override void OnLostFocus (EventArgs e) { LostFocusCount++; base.OnLostFocus (e); }
        protected override void OnDragEnter (DragEventArgs e) { DragEnterCount++; base.OnDragEnter (e); }
        protected override void OnDragDrop (DragEventArgs e) { DragDropCount++; base.OnDragDrop (e); }

        // Exposed so a test can drive the protected hooks; nothing in the library raises the drag ones
        // yet (no OS drag source), so this is the only way to prove an override is reachable.
        public void RaiseDragEnter (DragEventArgs e) => OnDragEnter (e);
        public void RaiseDragDrop (DragEventArgs e) => OnDragDrop (e);
    }

    [Fact]
    public void OnMove_IsRaisedWhenTheWindowMoves ()
    {
        using var form = new HookForm ();

        form.Location = new System.Drawing.Point (40, 60);

        Assert.True (form.MoveCount > 0, "OnMove was never raised");
    }

    [Fact]
    public void OnMove_AlsoRaisesTheMoveEvent ()
    {
        // Move is an alias of LocationChanged, so the override must forward rather than swallow it.
        using var form = new HookForm ();
        var raised = 0;
        form.Move += (s, e) => raised++;

        form.Location = new System.Drawing.Point (11, 12);

        Assert.True (raised > 0, "the Move event did not fire");
    }

    [Fact]
    public void OnGotFocus_And_OnLostFocus_RideOnActivation ()
    {
        HeadlessRenderer.Use ();
        using var form = new HookForm ();
        form.Show ();

        // Showing the window already activates it, so compare deltas rather than absolutes.
        var gotBefore = form.GotFocusCount;
        var lostBefore = form.LostFocusCount;

        form.OnBackendActivated ();
        form.OnBackendDeactivated ();

        Assert.Equal (gotBefore + 1, form.GotFocusCount);
        Assert.Equal (lostBefore + 1, form.LostFocusCount);
    }

    [Fact]
    public void FocusEvents_ReachSubscribers ()
    {
        HeadlessRenderer.Use ();
        using var form = new HookForm ();
        var got = 0;
        var lost = 0;
        form.Show ();

        // Subscribed after Show so the activation Show itself performs is not counted.
        form.GotFocus += (s, e) => got++;
        form.LostFocus += (s, e) => lost++;

        form.OnBackendActivated ();
        form.OnBackendDeactivated ();

        Assert.Equal (1, got);
        Assert.Equal (1, lost);
    }

    [Fact]
    public void DragHooks_AreOverridableAndRaiseTheirEvents ()
    {
        // The events used to be declared `{ add { } remove { } }`, which discarded the handler, so
        // neither the subscriber nor an override could ever be reached.
        using var form = new HookForm ();
        var enterHandlerCalls = 0;
        var dropHandlerCalls = 0;
        form.DragEnter += (s, e) => enterHandlerCalls++;
        form.DragDrop += (s, e) => dropHandlerCalls++;

        var args = new DragEventArgs (null, 0, 0, 0, DragDropEffects.Copy, DragDropEffects.Copy);
        form.RaiseDragEnter (args);
        form.RaiseDragDrop (args);

        Assert.Equal (1, form.DragEnterCount);
        Assert.Equal (1, form.DragDropCount);
        Assert.Equal (1, enterHandlerCalls);
        Assert.Equal (1, dropHandlerCalls);
    }
}
