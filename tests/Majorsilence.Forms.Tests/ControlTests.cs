using Xunit;

namespace Majorsilence.Forms.Tests
{
    public class ControlTests
    {
        [Fact]
        public void ClientSize ()
        {
            var control = new Control {
                Width = 100,
                Height = 100
            };

            Assert.Equal (100, control.ClientSize.Width);
            Assert.Equal (100, control.ClientSize.Height);

            control.Padding = new Padding (15);

            Assert.Equal (100, control.ClientSize.Width);
            Assert.Equal (100, control.ClientSize.Height);
        }

        [Fact]
        public void ClientRectangle ()
        {
            var control = new Control {
                Width = 100,
                Height = 100
            };

            Assert.Equal (0, control.ClientRectangle.Left);
            Assert.Equal (0, control.ClientRectangle.Top);
            Assert.Equal (100, control.ClientRectangle.Width);
            Assert.Equal (100, control.ClientRectangle.Height);

            control.Padding = new Padding (15);

            Assert.Equal (0, control.ClientRectangle.Left);
            Assert.Equal (0, control.ClientRectangle.Top);
            Assert.Equal (100, control.ClientRectangle.Width);
            Assert.Equal (100, control.ClientRectangle.Height);
        }

        [Fact]
        public void DisplayRectangle ()
        {
            var control = new Control {
                Width = 100,
                Height = 100
            };

            Assert.Equal (0, control.DisplayRectangle.Left);
            Assert.Equal (0, control.DisplayRectangle.Top);
            Assert.Equal (100, control.DisplayRectangle.Width);
            Assert.Equal (100, control.DisplayRectangle.Height);

            control.Padding = new Padding (15);

            Assert.Equal (0, control.DisplayRectangle.Left);
            Assert.Equal (0, control.DisplayRectangle.Top);
            Assert.Equal (100, control.DisplayRectangle.Width);
            Assert.Equal (100, control.DisplayRectangle.Height);
        }

        [Fact]
        public void Text_DefaultsToEmpty ()
        {
            using var control = new Control ();
            Assert.Equal (string.Empty, control.Text);
        }

        [Fact]
        public void Text_SetNull_CoercedToEmpty ()
        {
            // WinForms compat: Control.Text is never null; a null assignment becomes string.Empty.
            using var control = new Control { Text = "hello" };

            var changed = 0;
            control.TextChanged += (_, _) => changed++;

            control.Text = null!;
            Assert.Equal (string.Empty, control.Text);
            Assert.Equal (1, changed);

            // Setting null again when already empty is a no-op (no spurious TextChanged).
            control.Text = null!;
            Assert.Equal (string.Empty, control.Text);
            Assert.Equal (1, changed);
        }

        [Fact]
        public void GetNextControl_BasicTabIndex ()
        {
            var container = new Control ();
            var controls = new Control[5];

            for (var i = 0; i < 5; i++) {
                controls[i] = new Control {
                    TabIndex = i,
                    Text = "ctrl " + i
                };

                container.Controls.Add (controls[i]);
            }

            Assert.Equal (controls[0], container.GetNextControl (null, true));
            Assert.Equal (controls[4], container.GetNextControl (null, false));

            Assert.Equal (controls[1], container.GetNextControl (controls[0], true));
            Assert.Null (container.GetNextControl (controls[0], false));

            Assert.Equal (controls[2], container.GetNextControl (controls[1], true));
            Assert.Equal (controls[0], container.GetNextControl (controls[1], false));

            Assert.Equal (controls[3], container.GetNextControl (controls[2], true));
            Assert.Equal (controls[1], container.GetNextControl (controls[2], false));

            Assert.Equal (controls[4], container.GetNextControl (controls[3], true));
            Assert.Equal (controls[2], container.GetNextControl (controls[3], false));

            Assert.Null (container.GetNextControl (controls[4], true));
            Assert.Equal (controls[3], container.GetNextControl (controls[4], false));

            container.Dispose ();
        }

        [Fact]
        public void GetNextControl_ReverseTabIndex ()
        {
            var container = new Control ();
            var controls = new Control[5];

            for (var i = 0; i < 5; i++) {
                controls[i] = new Control {
                    TabIndex = 5 - i,
                    Text = "ctrl " + i
                };

                container.Controls.Add (controls[i]);
            }

            Assert.Equal ("ctrl 4", container.GetNextControl (null, true)?.Text);
            Assert.Equal ("ctrl 0", container.GetNextControl (null, false)?.Text);

            // Ignores passed in controls that are not child controls
            Assert.Equal ("ctrl 4", container.GetNextControl (new Control (), true)?.Text);
            Assert.Equal ("ctrl 0", container.GetNextControl (new Control (), false)?.Text);

            Assert.Null (container.GetNextControl (controls[0], true));
            Assert.Equal ("ctrl 1", container.GetNextControl (controls[0], false)?.Text);

            Assert.Equal ("ctrl 0", container.GetNextControl (controls[1], true)?.Text);
            Assert.Equal ("ctrl 2", container.GetNextControl (controls[1], false)?.Text);

            Assert.Equal ("ctrl 1", container.GetNextControl (controls[2], true)?.Text);
            Assert.Equal ("ctrl 3", container.GetNextControl (controls[2], false)?.Text);

            Assert.Equal ("ctrl 2", container.GetNextControl (controls[3], true)?.Text);
            Assert.Equal ("ctrl 4", container.GetNextControl (controls[3], false)?.Text);

            Assert.Equal ("ctrl 3", container.GetNextControl (controls[4], true)?.Text);
            Assert.Null (container.GetNextControl (controls[4], false));

            container.Dispose ();
        }

        [Fact]
        public void GetNextControl_DuplicateTabIndex ()
        {
            var container = new Control ();
            var controls = new Control[5];

            for (var i = 0; i < 5; i++) {
                controls[i] = new Control {
                    TabIndex = i,
                    Text = "ctrl " + i
                };

                container.Controls.Add (controls[i]);
            }

            controls[3].TabIndex = 2;

            Assert.Equal ("ctrl 0", container.GetNextControl (null, true)?.Text);
            Assert.Equal ("ctrl 4", container.GetNextControl (null, false)?.Text);

            Assert.Equal ("ctrl 1", container.GetNextControl (controls[0], true)?.Text);
            Assert.Null (container.GetNextControl (controls[0], false));

            Assert.Equal ("ctrl 2", container.GetNextControl (controls[1], true)?.Text);
            Assert.Equal ("ctrl 0", container.GetNextControl (controls[1], false)?.Text);

            Assert.Equal ("ctrl 3", container.GetNextControl (controls[2], true)?.Text);
            Assert.Equal ("ctrl 1", container.GetNextControl (controls[2], false)?.Text);

            Assert.Equal ("ctrl 4", container.GetNextControl (controls[3], true)?.Text);
            Assert.Equal ("ctrl 2", container.GetNextControl (controls[3], false)?.Text);

            Assert.Null (container.GetNextControl (controls[4], true));
            Assert.Equal ("ctrl 3", container.GetNextControl (controls[4], false)?.Text);

            container.Dispose ();
        }
        [Fact]
        public void GetNextControl_NestedControls ()
        {
            // - Form
            //   - Button 1
            //   - Panel 1  (Panels are not selectable and are not IContainerControl
            //     - Button 2
            //   - UserControl 1  (UserControls are selectable and is IContainerControl)
            //     - Button 3
            //   - Button 4

            var f = new Form ();

            var b1 = new Button { Text = "Button 1" };
            var b2 = new Button { Text = "Button 2" };
            var b3 = new Button { Text = "Button 3" };
            var b4 = new Button { Text = "Button 4", Top = 90 };

            f.Controls.Add (b1);

            var p1 = new Panel { Text = "Panel 1", Top = 30, Height = 30 };
            p1.Controls.Add (b2);
            f.Controls.Add (p1);

            var uc1 = new Control { Text = "UserControl 1", Top = 60, Height = 30 };
            uc1.Controls.Add (b3);
            f.Controls.Add (uc1);

            f.Controls.Add (b4);

            // Button 1 as "this"
            Assert.Null (b1.GetNextControl (b1, true));
            Assert.Null (b1.GetNextControl (p1, true));
            Assert.Null (b1.GetNextControl (b2, true));
            Assert.Null (b1.GetNextControl (uc1, true));
            Assert.Null (b1.GetNextControl (b3, true));
            Assert.Null (b1.GetNextControl (b4, true));

            // Panel 1 as "this"
            Assert.Equal ("Button 2", p1.GetNextControl (b1, true)?.Text);
            Assert.Equal ("Button 2", p1.GetNextControl (p1, true)?.Text);
            Assert.Null (p1.GetNextControl (b2, true));
            Assert.Equal ("Button 2", p1.GetNextControl (uc1, true)?.Text);
            Assert.Equal ("Button 2", p1.GetNextControl (b3, true)?.Text);
            Assert.Equal ("Button 2", p1.GetNextControl (b4, true)?.Text);

            // Form as "this"
            Assert.Equal ("Panel 1", f.GetNextControl (b1, true)?.Text);
            Assert.Equal ("Button 2", f.GetNextControl (p1, true)?.Text);
            Assert.Equal ("UserControl 1", f.GetNextControl (b2, true)?.Text);
            //Assert.Equal ("Button 4", f.GetNextControl (uc1, true).Text);
            Assert.Equal ("Button 4", f.GetNextControl (b3, true)?.Text);
            Assert.Null (f.GetNextControl (b4, true));
        }

        // Test double that records every OnVisibleChanged invocation, used to prove the
        // OnParentVisibleChanged cascade reaches arbitrarily deep descendants.
        private sealed class VisibleChangeRecordingControl : Control
        {
            public int VisibleChangedCount { get; private set; }

            protected override void OnVisibleChanged (EventArgs e)
            {
                VisibleChangedCount++;
                base.OnVisibleChanged (e);
            }
        }

        [Fact]
        public void OnParentVisibleChanged_CascadesThroughMultipleLevels_WhenAncestorHidden ()
        {
            // Form -> Panel A -> Panel B -> deeply-nested control
            using var form = new Form ();
            var panelA = new Panel ();
            var panelB = new Panel ();
            var leaf = new VisibleChangeRecordingControl ();

            panelB.Controls.Add (leaf);
            panelA.Controls.Add (panelB);
            form.Controls.Add (panelA);

            var baseline = leaf.VisibleChangedCount;

            panelA.Visible = false;

            // The cascade must reach the great-grandchild, not stop at panelB.
            Assert.False (leaf.Visible);
            Assert.True (leaf.VisibleChangedCount > baseline);
        }

        [Fact]
        public void OnParentVisibleChanged_CascadesThroughMultipleLevels_WhenAncestorRemovedFromParent ()
        {
            using var form = new Form ();
            var panelA = new Panel ();
            var panelB = new Panel ();
            var leaf = new VisibleChangeRecordingControl ();

            panelB.Controls.Add (leaf);
            panelA.Controls.Add (panelB);
            form.Controls.Add (panelA);

            var baseline = leaf.VisibleChangedCount;

            form.Controls.Remove (panelA);

            Assert.False (leaf.Visible);
            Assert.True (leaf.VisibleChangedCount > baseline);
        }

        [Fact]
        public void OnParentVisibleChanged_CascadesBackOnReshow_WhenAncestorReAdded ()
        {
            using var form = new Form ();
            var panelA = new Panel ();
            var panelB = new Panel ();
            var leaf = new VisibleChangeRecordingControl ();

            panelB.Controls.Add (leaf);
            panelA.Controls.Add (panelB);
            form.Controls.Add (panelA);

            panelA.Visible = false;
            Assert.False (leaf.Visible);

            var countWhileHidden = leaf.VisibleChangedCount;

            panelA.Visible = true;

            Assert.True (leaf.Visible);
            Assert.True (leaf.VisibleChangedCount > countWhileHidden);
        }

        [Fact]
        public void OnParentVisibleChanged_CascadesBackOnReshow_WhenAncestorReAddedToParent ()
        {
            using var form = new Form ();
            var panelA = new Panel ();
            var panelB = new Panel ();
            var leaf = new VisibleChangeRecordingControl ();

            panelB.Controls.Add (leaf);
            panelA.Controls.Add (panelB);
            form.Controls.Add (panelA);

            form.Controls.Remove (panelA);
            Assert.False (leaf.Visible);

            var countWhileRemoved = leaf.VisibleChangedCount;

            form.Controls.Add (panelA);

            Assert.True (leaf.Visible);
            Assert.True (leaf.VisibleChangedCount > countWhileRemoved);
        }

        [Fact]
        public void OnParentVisibleChanged_DoesNotFireForControlExplicitlyHiddenAtIntermediateLevel ()
        {
            // panelB is explicitly, locally hidden by the user. Toggling an ancestor's (panelA's)
            // visibility should not cause panelB's own OnVisibleChanged to fire again on the way down,
            // since panelB's local Visible flag is already false - matching the original guard's intent
            // of not spuriously re-notifying an already-(locally)-hidden subtree.
            using var form = new Form ();
            var panelA = new Panel ();
            var panelB = new VisibleChangeRecordingControl ();
            var leaf = new VisibleChangeRecordingControl ();

            panelB.Controls.Add (leaf);
            panelA.Controls.Add (panelB);
            form.Controls.Add (panelA);

            panelB.Visible = false;

            var panelBCountAfterOwnHide = panelB.VisibleChangedCount;
            var leafCountAfterPanelBHide = leaf.VisibleChangedCount;

            // Toggle the ancestor. panelB's local flag is still false, so panelB itself should not
            // receive another OnVisibleChanged call, and since panelB stays effectively invisible,
            // leaf (already effectively hidden) should likewise not fire again.
            panelA.Visible = false;
            panelA.Visible = true;

            Assert.Equal (panelBCountAfterOwnHide, panelB.VisibleChangedCount);
            Assert.Equal (leafCountAfterPanelBHide, leaf.VisibleChangedCount);

            // Sanity: leaf is still effectively hidden because panelB (its direct ancestor) is locally hidden.
            Assert.False (leaf.Visible);
        }
    }
}
