using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.1, the Telerik sweep -- the docking cluster from #176.
    //
    // Four members that stored or ignored what they were given:
    //
    //   DockWindow                  parented nothing, so a docked window never appeared
    //   GetDefaultDocumentTabStrip  handed back a strip that was never parented to anything
    //   GetWindows (DockState)      ignored the state and always returned the same list
    //   CloseAction                 stored, never read -- closing always meant hiding
    //
    // DockWindows missing its documents is the same defect as the first: it enumerated only the tool
    // windows the dock had been *told* about, so DocumentWindows was empty even for the
    // designer-generated structure the rest of the docking layout reads.
    //
    // ContextMenuService.ContextMenuDisplaying is the one left unraised, and deliberately: the compat
    // dock has no context menu of its own to display, so there is no moment to announce. Same call as
    // RadGridView.CreateCell in #192.
    [Collection ("Headless")]
    public class RadDockWindowManagementTests
    {
        private static RadDock Built (out Form form)
        {
            HeadlessRenderer.Use ();

            form = new Form { Size = new System.Drawing.Size (500, 400) };

            var dock = new RadDock { Left = 0, Top = 0, Width = 480, Height = 360 };
            var container = new DocumentContainer ();

            dock.Controls.Add (container);
            dock.MainDocumentContainer = container;
            form.Controls.Add (dock);
            form.Show ();

            return dock;
        }

        // ---------------- DockWindow parents what it is given

        [Fact]
        public void Docking_a_document_puts_it_in_the_tree ()
        {
            using var dock = Built (out var form);

            try {
                var doc = new DocumentWindow { Name = "docA", Text = "Alpha" };

                dock.DockWindow (doc);

                Assert.NotNull (doc.Parent);
                Assert.IsType<DocumentTabStrip> (doc.Parent);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_docked_document_reaches_the_document_manager ()
        {
            // What the missing parenting cost: DocumentArray walks the control tree, so a document
            // that was never a child of anything could not appear in it however it was docked.
            using var dock = Built (out var form);

            try {
                var doc = new DocumentWindow { Name = "docA", Text = "Alpha" };

                dock.DockWindow (doc);

                Assert.Contains (doc, dock.DocumentManager.DocumentArray);
                Assert.Contains (doc, dock.DockWindows.DocumentWindows);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_document_strip_is_reused_rather_than_multiplied ()
        {
            // GUARD: creating a strip per call would park each document in a strip of its own, which
            // looks like it works from DocumentArray and shows one tab per strip on screen.
            using var dock = Built (out var form);

            try {
                var a = new DocumentWindow { Name = "docA" };
                var b = new DocumentWindow { Name = "docB" };

                dock.DockWindow (a);
                dock.DockWindow (b);

                Assert.Same (a.Parent, b.Parent);
                Assert.Single (dock.MainDocumentContainer!.Controls.OfType<DocumentTabStrip> ());
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Docking_a_tool_window_parents_it_to_the_dock ()
        {
            using var dock = Built (out var form);

            try {
                var tool = new ToolWindow ("Explorer");

                dock.DockWindow (tool);

                Assert.Same (dock, tool.Parent);
                Assert.Contains (tool, dock.DockWindows.ToolWindows);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Docking_a_window_twice_does_not_move_it ()
        {
            // GUARD: an unconditional reparent would tear a window out of the tab strip a previous
            // call (or the designer) put it in.
            using var dock = Built (out var form);

            try {
                var doc = new DocumentWindow { Name = "docA" };

                dock.DockWindow (doc);
                var first = doc.Parent;
                dock.DockWindow (doc);

                Assert.Same (first, doc.Parent);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_default_document_strip_is_part_of_the_tree ()
        {
            using var dock = Built (out var form);

            try {
                var strip = dock.GetDefaultDocumentTabStrip (createIfMissing: true);

                Assert.Same (dock.MainDocumentContainer, strip.Parent);
                Assert.Same (strip, dock.GetDefaultDocumentTabStrip (createIfMissing: true));
            } finally {
                form.Close ();
            }
        }

        // ---------------- GetWindows honours the state it is given

        [Fact]
        public void GetWindows_returns_only_the_windows_in_that_state ()
        {
            using var dock = Built (out var form);

            try {
                var docked = new ToolWindow ("Docked") { DockState = DockState.Docked };
                var floating = new ToolWindow ("Floating") { DockState = DockState.Floating };

                dock.DockWindow (docked);
                dock.DockWindow (floating);

                Assert.Equal (new[] { docked }, dock.GetWindows (DockState.Docked).ToArray ());
                Assert.Equal (new[] { floating }, dock.GetWindows (DockState.Floating).ToArray ());
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_state_with_nothing_in_it_returns_nothing ()
        {
            // The half that was most obviously wrong: every state used to answer the full list, so
            // asking for the auto-hidden windows of a dock that has none got all of them.
            using var dock = Built (out var form);

            try {
                dock.DockWindow (new ToolWindow ("Docked") { DockState = DockState.Docked });

                Assert.Empty (dock.GetWindows (DockState.AutoHide));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void GetWindows_sees_documents_too ()
        {
            using var dock = Built (out var form);

            try {
                var doc = new DocumentWindow { Name = "docA", DockState = DockState.TabbedDocument };

                dock.DockWindow (doc);

                Assert.Contains (doc, dock.GetWindows (DockState.TabbedDocument));
            } finally {
                form.Close ();
            }
        }

        // ---------------- CloseAction is read

        [Fact]
        public void Closing_a_Hide_window_hides_it_and_keeps_it ()
        {
            using var dock = Built (out var form);

            try {
                var tool = new ToolWindow ("Explorer") { CloseAction = DockWindowCloseAction.Hide };
                dock.DockWindow (tool);

                dock.CloseWindow (tool);

                Assert.False (tool.Visible);
                Assert.False (tool.IsDisposed);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Closing_a_CloseAndDispose_window_disposes_it ()
        {
            // The property's whole purpose, and what it cost while nothing read it: an application
            // that set CloseAndDispose to release a document's resources kept every one of them alive.
            using var dock = Built (out var form);

            try {
                var tool = new ToolWindow ("Explorer") { CloseAction = DockWindowCloseAction.CloseAndDispose };
                dock.DockWindow (tool);

                dock.CloseWindow (tool);

                Assert.True (tool.IsDisposed);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Close_on_the_window_itself_honours_it_as_well ()
        {
            // Telerik's DockWindowBase.Close reads CloseAction too; an application closing a window
            // directly rather than through the dock gets the same behaviour.
            using var doc = new DocumentWindow { CloseAction = DockWindowCloseAction.CloseAndDispose };

            doc.Close ();

            Assert.True (doc.IsDisposed);
        }

        [Fact]
        public void Closing_removes_a_tool_window_from_the_dock ()
        {
            // GUARD on the bookkeeping the close path also does: honouring CloseAction must not cost
            // the removal that was already working.
            using var dock = Built (out var form);

            try {
                var tool = new ToolWindow ("Explorer");
                dock.DockWindow (tool);

                Assert.Contains (tool, dock.DockWindows.ToolWindows);

                dock.CloseWindow (tool);

                Assert.DoesNotContain (tool, dock.DockWindows.ToolWindows);
            } finally {
                form.Close ();
            }
        }
    }
}
