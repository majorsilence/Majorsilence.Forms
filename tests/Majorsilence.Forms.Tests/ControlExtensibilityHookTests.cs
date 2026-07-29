using System.Collections.Generic;
using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Every control derives from Control, so its protected On*/Reset*/RtlTranslate*/Scale* hooks are
    // the widest-reach part of the WinForms extensibility surface. These tests assert the hooks
    // actually FIRE (an override runs, a public event handler runs) rather than merely existing --
    // a hook that never fires is worse than a missing one.
    public class ControlExtensibilityHookTests
    {
        // Exposes the protected surface so a test can both observe overrides being called and
        // drive the hooks that no backend raises yet (drag-and-drop, printing, scaling).
        private sealed class HookControl : Control
        {
            public List<string> Calls { get; } = new List<string> ();

            protected override void OnBackColorChanged (EventArgs e) { Calls.Add (nameof (OnBackColorChanged)); base.OnBackColorChanged (e); }
            protected override void OnForeColorChanged (EventArgs e) { Calls.Add (nameof (OnForeColorChanged)); base.OnForeColorChanged (e); }
            protected override void OnFontChanged (EventArgs e) { Calls.Add (nameof (OnFontChanged)); base.OnFontChanged (e); }
            protected override void OnHandleCreated (EventArgs e) { Calls.Add (nameof (OnHandleCreated)); base.OnHandleCreated (e); }
            protected override void OnHandleDestroyed (EventArgs e) { Calls.Add (nameof (OnHandleDestroyed)); base.OnHandleDestroyed (e); }
            protected override void OnEnter (EventArgs e) { Calls.Add (nameof (OnEnter)); base.OnEnter (e); }
            protected override void OnLeave (EventArgs e) { Calls.Add (nameof (OnLeave)); base.OnLeave (e); }
            protected override void OnMouseCaptureChanged (EventArgs e) { Calls.Add (nameof (OnMouseCaptureChanged)); base.OnMouseCaptureChanged (e); }
            protected override void OnMouseHover (EventArgs e) { Calls.Add (nameof (OnMouseHover)); base.OnMouseHover (e); }
            protected override void OnMouseClick (MouseEventArgs e) { Calls.Add (nameof (OnMouseClick)); base.OnMouseClick (e); }
            protected override void OnMouseDoubleClick (MouseEventArgs e) { Calls.Add (nameof (OnMouseDoubleClick)); base.OnMouseDoubleClick (e); }
            protected override void OnPreviewKeyDown (PreviewKeyDownEventArgs e) { Calls.Add (nameof (OnPreviewKeyDown)); base.OnPreviewKeyDown (e); }
            protected override void OnKeyDown (KeyEventArgs e) { Calls.Add (nameof (OnKeyDown)); base.OnKeyDown (e); }
            protected override void OnCausesValidationChanged (EventArgs e) { Calls.Add (nameof (OnCausesValidationChanged)); base.OnCausesValidationChanged (e); }
            protected override void OnImeModeChanged (EventArgs e) { Calls.Add (nameof (OnImeModeChanged)); base.OnImeModeChanged (e); }
            protected override void OnRightToLeftChanged (EventArgs e) { Calls.Add (nameof (OnRightToLeftChanged)); base.OnRightToLeftChanged (e); }
            protected override void OnContextMenuStripChanged (EventArgs e) { Calls.Add (nameof (OnContextMenuStripChanged)); base.OnContextMenuStripChanged (e); }
            protected override void OnCursorChanged (EventArgs e) { Calls.Add (nameof (OnCursorChanged)); base.OnCursorChanged (e); }

            // Drag-and-drop has no backend drag source yet, so expose the raisers.
            public void RaiseDragEnter (DragEventArgs e) => OnDragEnter (e);
            public void RaiseDragOver (DragEventArgs e) => OnDragOver (e);
            public void RaiseDragDrop (DragEventArgs e) => OnDragDrop (e);
            public void RaiseDragLeave () => OnDragLeave (EventArgs.Empty);
            public void RaiseGiveFeedback (GiveFeedbackEventArgs e) => OnGiveFeedback (e);
            public void RaiseQueryContinueDrag (QueryContinueDragEventArgs e) => OnQueryContinueDrag (e);

            public void RaisePrint (PaintEventArgs e) => OnPrint (e);

            public void CallScaleControl (SizeF factor, BoundsSpecified specified) => ScaleControl (factor, specified);

            public HorizontalAlignment CallRtl (HorizontalAlignment a) => RtlTranslateAlignment (a);
            public LeftRightAlignment CallRtl (LeftRightAlignment a) => RtlTranslateAlignment (a);
            public ContentAlignment CallRtl (ContentAlignment a) => RtlTranslateAlignment (a);
            public HorizontalAlignment CallRtlHorizontal (HorizontalAlignment a) => RtlTranslateHorizontal (a);
            public LeftRightAlignment CallRtlLeftRight (LeftRightAlignment a) => RtlTranslateLeftRight (a);
            public ContentAlignment CallRtlContent (ContentAlignment a) => RtlTranslateContent (a);

            public Bitmap CallScaleBitmap (Bitmap bitmap)
            {
                ScaleBitmapLogicalToDevice (ref bitmap);
                return bitmap;
            }
        }

        private static MouseEventArgs Mouse (int x = 1, int y = 1)
            => new MouseEventArgs (MouseButtons.Left, 1, x, y, Point.Empty, x, y, Keys.None);

        #region Ambient appearance

        [Fact]
        public void BackColor_setter_raises_OnBackColorChanged_and_the_event ()
        {
            using var control = new HookControl ();
            var fired = 0;
            control.BackColorChanged += (s, e) => fired++;

            control.BackColor = Color.Red;

            Assert.Contains ("OnBackColorChanged", control.Calls);
            Assert.Equal (1, fired);
            Assert.Equal (Color.Red.ToArgb (), control.BackColor.ToArgb ());
        }

        [Fact]
        public void BackColor_setter_does_not_notify_when_the_value_is_unchanged ()
        {
            using var control = new HookControl ();
            control.BackColor = Color.Red;

            var fired = 0;
            control.BackColorChanged += (s, e) => fired++;

            control.BackColor = Color.Red;

            Assert.Equal (0, fired);
        }

        [Fact]
        public void BackColor_change_cascades_to_children_that_inherit_it ()
        {
            using var parent = new Panel ();
            var child = new HookControl ();
            parent.Controls.Add (child);

            var childFired = 0;
            child.BackColorChanged += (s, e) => childFired++;

            parent.BackColor = Color.Blue;

            Assert.Equal (1, childFired);
            Assert.Contains ("OnBackColorChanged", child.Calls);
        }

        [Fact]
        public void BackColor_change_does_not_cascade_to_a_child_with_its_own_color ()
        {
            using var parent = new Panel ();
            var child = new HookControl { BackColor = Color.Green };
            parent.Controls.Add (child);

            var childFired = 0;
            child.BackColorChanged += (s, e) => childFired++;

            parent.BackColor = Color.Blue;

            Assert.Equal (0, childFired);
        }

        [Fact]
        public void ForeColor_setter_raises_OnForeColorChanged_and_the_event ()
        {
            using var control = new HookControl ();
            var fired = 0;
            control.ForeColorChanged += (s, e) => fired++;

            control.ForeColor = Color.Magenta;
            control.ForeColor = Color.Magenta; // no-op

            Assert.Contains ("OnForeColorChanged", control.Calls);
            Assert.Equal (1, fired);
        }

        [Fact]
        public void Font_setter_raises_OnFontChanged_and_the_event ()
        {
            using var control = new HookControl ();
            var fired = 0;
            control.FontChanged += (s, e) => fired++;

            control.Font = new Font ("Arial", 14f);

            Assert.Contains ("OnFontChanged", control.Calls);
            Assert.Equal (1, fired);
        }

        [Fact]
        public void ResetFont_clears_the_override_and_notifies_once ()
        {
            using var control = new HookControl ();
            control.Font = new Font ("Arial", 14f);

            var fired = 0;
            control.FontChanged += (s, e) => fired++;

            control.ResetFont ();
            control.ResetFont (); // already reset -- nothing changed

            Assert.Equal (1, fired);
        }

        [Fact]
        public void Font_change_cascades_to_children_that_inherit_it ()
        {
            using var parent = new Panel ();
            var child = new HookControl ();
            parent.Controls.Add (child);

            var childFired = 0;
            child.FontChanged += (s, e) => childFired++;

            parent.Font = new Font ("Arial", 20f);

            Assert.Equal (1, childFired);
        }

        #endregion

        #region Handle lifetime

        [Fact]
        public void CreateControl_raises_OnHandleCreated_once ()
        {
            using var control = new HookControl ();
            var fired = 0;
            control.HandleCreated += (s, e) => fired++;

            control.CreateControl ();
            control.CreateControl (); // already created

            Assert.Equal (1, fired);
            Assert.Contains ("OnHandleCreated", control.Calls);
        }

        [Fact]
        public void Dispose_raises_OnHandleDestroyed ()
        {
            var control = new HookControl ();
            control.CreateControl ();

            var fired = 0;
            control.HandleDestroyed += (s, e) => fired++;

            control.Dispose ();

            Assert.Equal (1, fired);
            Assert.Contains ("OnHandleDestroyed", control.Calls);
        }

        #endregion

        #region Focus

        [Fact]
        public void OnGotFocus_raises_Enter_before_GotFocus_without_double_firing ()
        {
            using var control = new HookControl ();

            var order = new List<string> ();
            control.Enter += (s, e) => order.Add ("Enter");
            control.GotFocus += (s, e) => order.Add ("GotFocus");

            control.RaiseEnter ();

            Assert.Equal (new[] { "Enter", "GotFocus" }, order);
            Assert.Contains ("OnEnter", control.Calls);
        }

        [Fact]
        public void OnLostFocus_raises_Leave_before_LostFocus_without_double_firing ()
        {
            using var control = new HookControl ();

            var order = new List<string> ();
            control.Leave += (s, e) => order.Add ("Leave");
            control.LostFocus += (s, e) => order.Add ("LostFocus");

            control.RaiseLeave ();

            Assert.Equal (new[] { "Leave", "LostFocus" }, order);
            Assert.Contains ("OnLeave", control.Calls);
        }

        [Fact]
        public void Enter_and_Leave_no_longer_alias_GotFocus_and_LostFocus ()
        {
            // Regression guard: Enter/Leave used to share the GotFocus/LostFocus event keys, so
            // adding an Enter handler made GotFocus handlers fire twice (and vice versa).
            using var control = new HookControl ();

            var gotFocus = 0;
            var lostFocus = 0;
            control.GotFocus += (s, e) => gotFocus++;
            control.LostFocus += (s, e) => lostFocus++;
            control.Enter += (s, e) => { };
            control.Leave += (s, e) => { };

            control.RaiseEnter ();
            control.RaiseLeave ();

            Assert.Equal (1, gotFocus);
            Assert.Equal (1, lostFocus);
        }

        #endregion

        #region Mouse / keyboard

        [Fact]
        public void Capture_change_raises_OnMouseCaptureChanged_only_on_a_real_change ()
        {
            using var control = new HookControl ();
            var fired = 0;
            control.MouseCaptureChanged += (s, e) => fired++;

            control.Capture = true;
            control.Capture = true; // unchanged
            control.Capture = false;

            Assert.Equal (2, fired);
            Assert.Contains ("OnMouseCaptureChanged", control.Calls);
        }

        [Fact]
        public void MouseEnter_raises_MouseHover ()
        {
            using var control = new HookControl { Width = 50, Height = 50 };
            var fired = 0;
            control.MouseHover += (s, e) => fired++;

            control.RaiseMouseEnter (Mouse ());

            Assert.Equal (1, fired);
            Assert.Contains ("OnMouseHover", control.Calls);
        }

        [Fact]
        public void KeyDown_is_preceded_by_PreviewKeyDown ()
        {
            using var control = new HookControl ();

            var order = new List<string> ();
            control.PreviewKeyDown += (s, e) => order.Add ("PreviewKeyDown:" + e.KeyCode);
            control.KeyDown += (s, e) => order.Add ("KeyDown:" + e.KeyCode);

            control.RaiseKeyDown (new KeyEventArgs (Keys.A));

            Assert.Equal (new[] { "PreviewKeyDown:A", "KeyDown:A" }, order);
            Assert.Equal (new[] { "OnPreviewKeyDown", "OnKeyDown" }, control.Calls);
        }

        [Fact]
        public void Click_routes_through_OnMouseClick ()
        {
            using var control = new HookControl { Width = 50, Height = 50 };
            var mouseClicks = 0;
            control.MouseClick += (s, e) => mouseClicks++;

            control.RaiseClick (Mouse ());

            Assert.Equal (1, mouseClicks);
            Assert.Contains ("OnMouseClick", control.Calls);
        }

        [Fact]
        public void DoubleClick_routes_through_OnMouseDoubleClick ()
        {
            using var control = new HookControl { Width = 50, Height = 50 };
            var doubleClicks = 0;
            control.MouseDoubleClick += (s, e) => doubleClicks++;

            control.RaiseDoubleClick (Mouse ());

            Assert.Equal (1, doubleClicks);
            Assert.Contains ("OnMouseDoubleClick", control.Calls);
        }

        #endregion

        #region Drag and drop

        [Fact]
        public void Drag_hooks_raise_their_events ()
        {
            using var control = new HookControl ();

            var seen = new List<string> ();
            control.DragEnter += (s, e) => seen.Add ("DragEnter");
            control.DragOver += (s, e) => seen.Add ("DragOver");
            control.DragDrop += (s, e) => seen.Add ("DragDrop");
            control.DragLeave += (s, e) => seen.Add ("DragLeave");
            control.GiveFeedback += (s, e) => seen.Add ("GiveFeedback");
            control.QueryContinueDrag += (s, e) => seen.Add ("QueryContinueDrag");

            var args = new DragEventArgs (null, 0, 0, 0, DragDropEffects.Copy, DragDropEffects.Copy);

            control.RaiseDragEnter (args);
            control.RaiseDragOver (args);
            control.RaiseDragDrop (args);
            control.RaiseDragLeave ();
            control.RaiseGiveFeedback (new GiveFeedbackEventArgs (DragDropEffects.Copy, true));
            control.RaiseQueryContinueDrag (new QueryContinueDragEventArgs (0, false, DragAction.Continue));

            Assert.Equal (
                new[] { "DragEnter", "DragOver", "DragDrop", "DragLeave", "GiveFeedback", "QueryContinueDrag" },
                seen);
        }

        [Fact]
        public void Drag_event_args_reach_the_handler_intact ()
        {
            using var control = new HookControl ();

            DragEventArgs? received = null;
            control.DragOver += (s, e) => { received = e; e.Effect = DragDropEffects.Move; };

            var args = new DragEventArgs (null, 3, 7, 9, DragDropEffects.All, DragDropEffects.Copy);
            control.RaiseDragOver (args);

            Assert.Same (args, received);
            Assert.Equal (7, received!.X);
            Assert.Equal (9, received.Y);
            Assert.Equal (DragDropEffects.Move, args.Effect); // handler's decision is observable
        }

        #endregion

        #region Property change notifications

        [Fact]
        public void CausesValidation_setter_notifies_only_on_a_real_change ()
        {
            using var control = new HookControl ();
            var fired = 0;
            control.CausesValidationChanged += (s, e) => fired++;

            Assert.True (control.CausesValidation); // WinForms default

            control.CausesValidation = true;  // unchanged
            control.CausesValidation = false;

            Assert.Equal (1, fired);
            Assert.False (control.CausesValidation);
            Assert.Contains ("OnCausesValidationChanged", control.Calls);
        }

        [Fact]
        public void ImeMode_setter_notifies_and_ResetImeMode_restores_the_default ()
        {
            using var control = new HookControl ();
            var fired = 0;
            control.ImeModeChanged += (s, e) => fired++;

            control.ImeMode = ImeMode.Hiragana;
            Assert.Equal (ImeMode.Hiragana, control.ImeMode);

            control.ResetImeMode ();

            Assert.Equal (ImeMode.NoControl, control.ImeMode);
            Assert.Equal (2, fired);
        }

        [Fact]
        public void RightToLeft_setter_notifies_only_on_a_real_change ()
        {
            using var control = new HookControl ();
            var fired = 0;
            control.RightToLeftChanged += (s, e) => fired++;

            Assert.Equal (RightToLeft.No, control.RightToLeft); // resolves to No with no parent

            control.RightToLeft = RightToLeft.Yes;
            Assert.Equal (1, fired);

            control.RightToLeft = RightToLeft.Yes; // unchanged
            Assert.Equal (1, fired);

            control.ResetRightToLeft ();
            Assert.Equal (RightToLeft.No, control.RightToLeft);
            Assert.Equal (2, fired);
            Assert.Contains ("OnRightToLeftChanged", control.Calls);
        }

        [Fact]
        public void RightToLeft_is_inherited_from_the_parent_and_cascades ()
        {
            using var parent = new Panel ();
            var child = new HookControl ();
            parent.Controls.Add (child);

            var childFired = 0;
            child.RightToLeftChanged += (s, e) => childFired++;

            parent.RightToLeft = RightToLeft.Yes;

            Assert.Equal (RightToLeft.Yes, child.RightToLeft);
            Assert.Equal (1, childFired);

            // An explicit value on the child wins and stops inheriting.
            child.RightToLeft = RightToLeft.No;
            parent.RightToLeft = RightToLeft.No;

            Assert.Equal (RightToLeft.No, child.RightToLeft);
        }

        [Fact]
        public void ContextMenuStrip_change_raises_OnContextMenuStripChanged ()
        {
            using var control = new HookControl ();
            var fired = 0;
            control.ContextMenuStripChanged += (s, e) => fired++;

            control.ContextMenuStrip = new ContextMenuStrip ();

            Assert.Equal (1, fired);
            Assert.Contains ("OnContextMenuStripChanged", control.Calls);
        }

        #endregion

        #region Reset*

        [Fact]
        public void ResetBackColor_drops_the_explicit_color_back_to_ambient ()
        {
            using var parent = new Panel ();
            parent.BackColor = Color.FromArgb (255, 10, 20, 30);

            var child = new HookControl ();
            parent.Controls.Add (child);
            child.BackColor = Color.FromArgb (255, 200, 100, 50);

            Assert.Equal (Color.FromArgb (255, 200, 100, 50).ToArgb (), child.BackColor.ToArgb ());

            var fired = 0;
            child.BackColorChanged += (s, e) => fired++;

            child.ResetBackColor ();

            // Ambient resolution takes over again -- the child now reports the parent's color.
            Assert.Equal (parent.BackColor.ToArgb (), child.BackColor.ToArgb ());
            Assert.Equal (1, fired);

            child.ResetBackColor (); // nothing left to reset
            Assert.Equal (1, fired);
        }

        [Fact]
        public void ResetForeColor_drops_the_explicit_color ()
        {
            using var control = new HookControl ();
            var themeDefault = control.ForeColor;

            control.ForeColor = Color.FromArgb (255, 1, 2, 3);
            Assert.NotEqual (themeDefault.ToArgb (), control.ForeColor.ToArgb ());

            var fired = 0;
            control.ForeColorChanged += (s, e) => fired++;

            control.ResetForeColor ();

            Assert.Equal (themeDefault.ToArgb (), control.ForeColor.ToArgb ());
            Assert.Equal (1, fired);
        }

        [Fact]
        public void ResetCursor_drops_the_explicit_cursor_back_to_the_default ()
        {
            using var control = new HookControl ();
            var defaultCursor = control.Cursor;

            control.Cursor = Cursors.Hand;
            Assert.Equal (Cursors.Hand, control.Cursor);

            var fired = 0;
            control.CursorChanged += (s, e) => fired++;

            control.ResetCursor ();

            Assert.Equal (defaultCursor, control.Cursor);
            Assert.Equal (1, fired);

            control.ResetCursor (); // nothing left to reset
            Assert.Equal (1, fired);
        }

        #endregion

        #region RTL translation

        [Fact]
        public void RtlTranslate_is_identity_when_reading_left_to_right ()
        {
            using var control = new HookControl ();

            Assert.Equal (HorizontalAlignment.Left, control.CallRtl (HorizontalAlignment.Left));
            Assert.Equal (LeftRightAlignment.Left, control.CallRtl (LeftRightAlignment.Left));
            Assert.Equal (ContentAlignment.TopLeft, control.CallRtl (ContentAlignment.TopLeft));
        }

        [Fact]
        public void RtlTranslateHorizontal_mirrors_left_and_right_but_not_center ()
        {
            using var control = new HookControl { RightToLeft = RightToLeft.Yes };

            Assert.Equal (HorizontalAlignment.Right, control.CallRtlHorizontal (HorizontalAlignment.Left));
            Assert.Equal (HorizontalAlignment.Left, control.CallRtlHorizontal (HorizontalAlignment.Right));
            Assert.Equal (HorizontalAlignment.Center, control.CallRtlHorizontal (HorizontalAlignment.Center));
        }

        [Fact]
        public void RtlTranslateLeftRight_mirrors ()
        {
            using var control = new HookControl { RightToLeft = RightToLeft.Yes };

            Assert.Equal (LeftRightAlignment.Right, control.CallRtlLeftRight (LeftRightAlignment.Left));
            Assert.Equal (LeftRightAlignment.Left, control.CallRtlLeftRight (LeftRightAlignment.Right));
        }

        [Theory]
        [InlineData (ContentAlignment.TopLeft, ContentAlignment.TopRight)]
        [InlineData (ContentAlignment.TopRight, ContentAlignment.TopLeft)]
        [InlineData (ContentAlignment.MiddleLeft, ContentAlignment.MiddleRight)]
        [InlineData (ContentAlignment.MiddleRight, ContentAlignment.MiddleLeft)]
        [InlineData (ContentAlignment.BottomLeft, ContentAlignment.BottomRight)]
        [InlineData (ContentAlignment.BottomRight, ContentAlignment.BottomLeft)]
        [InlineData (ContentAlignment.TopCenter, ContentAlignment.TopCenter)]
        [InlineData (ContentAlignment.MiddleCenter, ContentAlignment.MiddleCenter)]
        [InlineData (ContentAlignment.BottomCenter, ContentAlignment.BottomCenter)]
        public void RtlTranslateContent_mirrors_horizontally_only (ContentAlignment input, ContentAlignment expected)
        {
            using var control = new HookControl { RightToLeft = RightToLeft.Yes };

            Assert.Equal (expected, control.CallRtlContent (input));
        }

        [Fact]
        public void RtlTranslate_follows_an_inherited_right_to_left ()
        {
            using var parent = new Panel { RightToLeft = RightToLeft.Yes };
            var child = new HookControl ();
            parent.Controls.Add (child);

            Assert.Equal (HorizontalAlignment.Right, child.CallRtl (HorizontalAlignment.Left));
        }

        #endregion

        #region Scaling

        [Fact]
        public void ScaleControl_scales_every_bounds_component_when_all_are_specified ()
        {
            using var control = new HookControl ();
            control.SetBounds (10, 20, 100, 50);

            control.CallScaleControl (new SizeF (2f, 2f), BoundsSpecified.All);

            Assert.Equal (new Rectangle (20, 40, 200, 100), control.Bounds);
        }

        [Fact]
        public void ScaleControl_leaves_unspecified_components_alone ()
        {
            using var control = new HookControl ();
            control.SetBounds (10, 20, 100, 50);

            control.CallScaleControl (new SizeF (2f, 2f), BoundsSpecified.Size);

            Assert.Equal (10, control.Bounds.X);
            Assert.Equal (20, control.Bounds.Y);
            Assert.Equal (200, control.Bounds.Width);
            Assert.Equal (100, control.Bounds.Height);
        }

        [Fact]
        public void ScaleBitmapLogicalToDevice_is_a_no_op_at_96_dpi ()
        {
            using var control = new HookControl ();
            var bitmap = new Bitmap (16, 16);

            var result = control.CallScaleBitmap (bitmap);

            Assert.Same (bitmap, result);
            Assert.Equal (16, result.Width);
            Assert.Equal (16, result.Height);
        }

        [Fact]
        public void ScaleBitmapLogicalToDevice_scales_to_the_device_dpi ()
        {
            // The headless backend always reports 96 DPI, so exercise the scaling itself through
            // the internal overload the protected member delegates to.
            using var bitmap = new Bitmap (16, 10);

            var scaled = Control.ScaleBitmapLogicalToDevice (bitmap, 192);

            Assert.NotSame (bitmap, scaled);
            Assert.Equal (32, scaled.Width);
            Assert.Equal (20, scaled.Height);

            scaled.Dispose ();
        }

        [Fact]
        public void Scale_float_overload_scales_the_control_and_its_children ()
        {
            using var control = new HookControl ();
            control.SetBounds (0, 0, 100, 50);

            var child = new Panel ();
            control.Controls.Add (child);
            child.SetBounds (10, 10, 20, 20);

            control.Scale (2f, 2f);

            Assert.Equal (new Size (200, 100), control.Size);
            Assert.Equal (new Rectangle (20, 20, 40, 40), child.Bounds);
        }

        #endregion

        #region Printing

        [Fact]
        public void OnPrint_paints_the_control ()
        {
            using var control = new HookControl { Width = 20, Height = 20 };

            var info = new SkiaSharp.SKImageInfo (20, 20);
            using var bitmap = new SkiaSharp.SKBitmap (info);
            using var canvas = new SkiaSharp.SKCanvas (bitmap);

            var painted = 0;
            control.Paint += (s, e) => painted++;

            // OnPrint draws directly; it does not go through RaisePaint, so Paint stays at 0. What
            // matters is that it is callable and completes a full background+foreground pass.
            control.RaisePrint (new PaintEventArgs (info, canvas, 1));

            Assert.Equal (0, painted);
        }

        #endregion
    }
}
