using System;
using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Input and form plumbing left over from #348 (SVC-10, SVC-38), #340 (BND-32 and the two binding
    // leftovers recorded under BND-15/BND-17) and #345 (FRM-38's AutoSize).
    [Collection ("Headless")]
    public sealed class InputAndFormPlumbingTests : IDisposable
    {
        private readonly Keys original_modifiers = Control.ModifierKeys;

        public InputAndFormPlumbingTests () => HeadlessRenderer.Use ();

        public void Dispose () => Control.ModifierKeys = original_modifiers;

        private static HeadlessWindowHost Host (Form form) => (HeadlessWindowHost) form.Backend;

        // ── SVC-10: Control.ModifierKeys comes from input, not from constructing event args ────────

        [Fact]
        public void Constructing_event_args_does_not_change_ModifierKeys ()
        {
            // The common pattern: build a KeyEventArgs to call OnKeyDown by hand. Upstream's
            // ModifierKeys reads the live keyboard (GetKeyState, Control.cs), so this cannot touch it.
            Control.ModifierKeys = Keys.None;

            _ = new KeyEventArgs (Keys.Shift | Keys.A);
            _ = new MouseEventArgs (MouseButtons.Left, 1, 0, 0, Point.Empty, keyData: Keys.Control);

            Assert.Equal (Keys.None, Control.ModifierKeys);
        }

        [Fact]
        public void A_key_through_the_window_sets_ModifierKeys_and_its_release_clears_it ()
        {
            using var form = new Form { Width = 300, Height = 200 };
            form.Show ();

            HeadlessRenderer.KeyDown (form, Keys.Control | Keys.ControlKey);
            Assert.Equal (Keys.Control, Control.ModifierKeys);

            HeadlessRenderer.KeyDown (form, Keys.Control | Keys.Shift | Keys.C);
            Assert.Equal (Keys.Control | Keys.Shift, Control.ModifierKeys);

            HeadlessRenderer.KeyUp (form, Keys.ControlKey);
            Assert.Equal (Keys.None, Control.ModifierKeys);
        }

        [Fact]
        public void A_pointer_event_through_the_window_carries_the_modifiers_into_ModifierKeys ()
        {
            // Ctrl+wheel zoom reads Control.ModifierKeys in its handler, not e.Modifiers.
            using var form = new Form { Width = 300, Height = 200 };
            form.Show ();
            Control.ModifierKeys = Keys.None;

            Keys seen = Keys.None;
            form.MouseWheel += (s, e) => seen = Control.ModifierKeys;

            form.HandlePointerWheel (MouseButtons.None, 20, 20, new Point (0, 1), Keys.Control);
            Assert.Equal (Keys.Control, seen);

            form.HandlePointerMoved (MouseButtons.None, 30, 30, Keys.Alt);
            Assert.Equal (Keys.Alt, Control.ModifierKeys);
        }

        // ── SVC-38: Cursor.Hide / Cursor.Show ──────────────────────────────────────────────────

        [Fact]
        public void Cursor_Hide_hides_the_pointer_on_open_windows_and_Show_brings_it_back ()
        {
            using var form = new Form { Width = 300, Height = 200, Cursor = Cursors.Cross };
            form.Show ();
            Assert.Equal (CursorType.Cross, Host (form).Cursor);

            Cursor.Hide ();
            try {
                Assert.Equal (CursorType.None, Host (form).Cursor);
            } finally {
                Cursor.Show ();
            }

            Assert.Equal (CursorType.Cross, Host (form).Cursor);
        }

        [Fact]
        public void Hide_and_Show_are_counted_as_upstreams_ShowCursor_is ()
        {
            using var form = new Form { Width = 300, Height = 200 };
            form.Show ();

            Cursor.Hide ();
            Cursor.Hide ();
            try {
                Cursor.Show ();
                Assert.Equal (CursorType.None, Host (form).Cursor);   // one Hide still outstanding
            } finally {
                Cursor.Show ();
            }

            Assert.Equal (CursorType.Arrow, Host (form).Cursor);
        }

        [Fact]
        public void A_hidden_cursor_stays_hidden_when_the_pointer_moves_over_a_control_with_its_own ()
        {
            // Every path that hands the backend a cursor honours Hide -- here the mouse-move
            // WM_SETCURSOR path, which would otherwise put the button's Hand back.
            using var form = new Form { Width = 300, Height = 200 };
            var button = new Button { Left = 20, Top = 20, Width = 100, Height = 30, Cursor = Cursors.Hand };
            form.Controls.Add (button);
            form.Show ();

            var at = button.GetPositionInForm ();

            Cursor.Hide ();
            try {
                HeadlessRenderer.MouseMove (form, at.X + 5, at.Y + 5);
                HeadlessRenderer.MouseMove (form, at.X + 6, at.Y + 6);
                Assert.Equal (CursorType.None, Host (form).Cursor);
            } finally {
                Cursor.Show ();
            }

            HeadlessRenderer.MouseMove (form, at.X + 7, at.Y + 7);
            Assert.Equal (CursorType.Hand, Host (form).Cursor);
        }

        // ── BND-32: the formatting setters re-push a live binding ─────────────────────────────

        private sealed class Row
        {
            public decimal Amount { get; set; } = 1234.5m;
            public string? Note { get; set; }
            public string Name { get; set; } = "Ada";
        }

        private static (Form form, TextBox box, Binding binding) BoundBox (Row row, string member, bool formatting = true)
        {
            var form = new Form ();
            var box = new TextBox ();
            form.Controls.Add (box);
            var binding = box.DataBindings.Add ("Text", row, member, formatting);
            return (form, box, binding);
        }

        [Fact]
        public void Changing_FormatString_on_a_live_binding_reformats_the_control ()
        {
            var (form, box, binding) = BoundBox (new Row (), nameof (Row.Amount));
            using var _ = form;
            binding.FormatInfo = System.Globalization.CultureInfo.InvariantCulture;

            binding.FormatString = "F2";

            Assert.Equal ("1234.50", box.Text);
        }

        [Fact]
        public void Changing_FormatInfo_on_a_live_binding_reformats_the_control ()
        {
            var (form, box, binding) = BoundBox (new Row (), nameof (Row.Amount));
            using var _ = form;
            binding.FormatInfo = System.Globalization.CultureInfo.InvariantCulture;
            binding.FormatString = "N1";
            Assert.Equal ("1,234.5", box.Text);

            binding.FormatInfo = System.Globalization.CultureInfo.GetCultureInfo ("de-DE");

            Assert.Equal ("1.234,5", box.Text);
        }

        [Fact]
        public void Turning_FormattingEnabled_on_applies_the_format_already_set ()
        {
            var (form, box, binding) = BoundBox (new Row (), nameof (Row.Amount), formatting: false);
            using var _ = form;
            binding.FormatString = "F3";
            binding.FormatInfo = System.Globalization.CultureInfo.InvariantCulture;
            Assert.Equal ("1234.5", box.Text);   // the legacy path ignores FormatString

            binding.FormattingEnabled = true;

            Assert.Equal ("1234.500", box.Text);
        }

        [Fact]
        public void Changing_NullValue_reshows_a_null_source_value_and_leaves_a_real_one_alone ()
        {
            var row = new Row ();
            var (form, box, binding) = BoundBox (row, nameof (Row.Note));
            using var _ = form;

            binding.NullValue = "(none)";
            Assert.Equal ("(none)", box.Text);

            row.Note = "kept";
            binding.ReadValue ();
            box.Text = "edited";               // not yet written back
            binding.NullValue = "(empty)";    // source is not null, so upstream does not push

            Assert.Equal ("edited", box.Text);
        }

        [Fact]
        public void Leaving_ControlUpdateMode_Never_catches_the_control_up_with_the_source ()
        {
            var row = new Row ();
            var (form, box, binding) = BoundBox (row, nameof (Row.Name));
            using var _ = form;
            binding.ControlUpdateMode = ControlUpdateMode.Never;

            row.Name = "Grace";
            Assert.Equal ("Ada", box.Text);

            binding.ControlUpdateMode = ControlUpdateMode.OnPropertyChanged;

            Assert.Equal ("Grace", box.Text);
        }

        // ── BND-17 leftover: the short DataBindings.Add overloads reject a null source ─────────────

        [Fact]
        public void The_short_DataBindings_Add_overloads_throw_on_a_null_source ()
        {
            using var box = new TextBox ();

            Assert.Throws<ArgumentNullException> (() => box.DataBindings.Add ("Text", null, "Name"));
            Assert.Throws<ArgumentNullException> (() => box.DataBindings.Add ("Text", null, "Name", true));
            Assert.Empty (box.DataBindings);
        }

        // ── BND-15 leftover: Form.BindingContext = x re-homes the form's controls ───────────────────

        private sealed class Person
        {
            public Person (string name) => Name = name;
            public string Name { get; set; }
        }

        [Fact]
        public void Setting_Form_BindingContext_moves_the_controls_bindings_onto_it ()
        {
            var people = new List<Person> { new ("Ada"), new ("Grace"), new ("Linus") };
            using var form = new Form ();
            var box = new TextBox ();
            form.Controls.Add (box);
            box.DataBindings.Add ("Text", people, "Name");

            var raised = 0;
            box.BindingContextChanged += (s, e) => raised++;

            var context = new BindingContext ();
            form.BindingContext = context;

            Assert.Same (context[people], box.DataBindings[0].BindingManagerBase);
            context[people].Position = 2;
            Assert.Equal ("Linus", box.Text);
            Assert.Equal (1, raised);
        }

        [Fact]
        public void Setting_Form_BindingContext_moves_the_forms_own_bindings_too ()
        {
            var people = new List<Person> { new ("Ada"), new ("Grace") };
            using var form = new Form ();
            form.DataBindings.Add ("Text", people, "Name");

            var context = new BindingContext ();
            form.BindingContext = context;

            Assert.Same (context[people], form.DataBindings[0].BindingManagerBase);
        }

        // ── FRM-38: Form.AutoSize ──────────────────────────────────────────────────────────

        [Fact]
        public void An_AutoSize_form_grows_its_client_area_to_fit_a_child ()
        {
            using var form = new Form { Width = 120, Height = 90 };
            var child = new Panel { Left = 10, Top = 10, Width = 200, Height = 100 };
            form.Controls.Add (child);

            form.AutoSize = true;
            form.PerformLayout ();

            // The caption is non-client: the CLIENT area, not the window, must hold the child.
            Assert.True (form.ClientSize.Width >= child.Right, $"{form.ClientSize} vs {child.Bounds}");
            Assert.True (form.ClientSize.Height >= child.Bottom, $"{form.ClientSize} vs {child.Bounds}");
            Assert.Equal (form.PreferredSize, form.Size);
        }

        [Fact]
        public void The_forms_preferred_size_is_its_content_plus_the_caption ()
        {
            using var form = new Form { Width = 500, Height = 400 };
            var child = new Panel { Left = 0, Top = 0, Width = 200, Height = 100, Margin = Padding.Empty };
            form.Controls.Add (child);

            var preferred = form.PreferredSize;

            Assert.Equal (200, preferred.Width);
            Assert.Equal (100 + (form.Size.Height - form.ClientSize.Height), preferred.Height);
        }

        [Fact]
        public void GrowOnly_keeps_a_larger_form_and_GrowAndShrink_fits_it ()
        {
            using var form = new Form { Width = 600, Height = 500 };
            form.Controls.Add (new Panel { Left = 10, Top = 10, Width = 200, Height = 100 });

            form.AutoSize = true;
            Assert.Equal (new Size (600, 500), form.Size);

            form.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Assert.Equal (form.PreferredSize, form.Size);
            Assert.True (form.Width < 600 && form.Height < 500, form.Size.ToString ());
        }

        [Fact]
        public void An_AutoSize_form_follows_a_child_that_grows_later ()
        {
            using var form = new Form { Width = 100, Height = 100, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            var child = new Panel { Left = 0, Top = 0, Width = 50, Height = 50 };
            form.Controls.Add (child);
            var before = form.Size;

            child.Width = 300;

            Assert.True (form.ClientSize.Width >= child.Right, $"{form.ClientSize} vs {child.Bounds}");
            Assert.True (form.Width > before.Width);
        }
    }
}
