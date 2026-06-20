using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using Continuum.Forms.Renderers;
using SkiaSharp;

namespace Continuum.Forms
{
    /// <summary>
    /// Represents a PropertyGrid control for browsing object properties at runtime.
    /// Displays public browsable properties sorted by category. Editing is not implemented.
    /// </summary>
    public class PropertyGrid : ScrollableControl
    {
        private object? _selected_object;
        private List<PropertyEntry> _entries = [];
        private int _selected_index = -1;
        private const int ROW_HEIGHT = 22;
        private const int NAME_COL_RATIO_PCT = 40;

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (300, 400);

        /// <inheritdoc/>
        public new static readonly ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => {
                style.Border.Width = 1;
                style.BackgroundColor = Theme.ControlLowColor;
            });

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>Gets or sets the object for which the grid displays properties.</summary>
        public object? SelectedObject {
            get => _selected_object;
            set {
                _selected_object = value;
                RebuildEntries ();
                Invalidate ();
            }
        }

        /// <summary>Gets or sets the objects for which the grid displays properties.</summary>
        public object[]? SelectedObjects {
            get => _selected_object == null ? null : new[] { _selected_object };
            set => SelectedObject = value?.Length > 0 ? value[0] : null;
        }

        /// <summary>Refreshes the displayed properties.</summary>
        public new void Refresh ()
        {
            RebuildEntries ();
            Invalidate ();
        }

        /// <summary>Expands all categories. Stub in Continuum.Forms.</summary>
        public void ExpandAllGridItems () { }

        /// <summary>Collapses all categories. Stub in Continuum.Forms.</summary>
        public void CollapseAllGridItems () { }

        /// <summary>Gets or sets the background color for the property value area.</summary>
        public Color ViewBackColor { get; set; } = SystemColors.Window;

        /// <summary>Gets or sets the foreground color for the property value area.</summary>
        public Color ViewForeColor { get; set; } = SystemColors.WindowText;

        /// <summary>Gets or sets the background color for the help panel.</summary>
        public Color HelpBackColor { get; set; } = SystemColors.Control;

        /// <summary>Gets or sets the foreground color for the help panel.</summary>
        public Color HelpForeColor { get; set; } = SystemColors.ControlText;

        /// <summary>Gets or sets the color for grid lines.</summary>
        public Color LineColor { get; set; } = SystemColors.InactiveBorder;

        /// <summary>Gets or sets whether the help panel is visible. Stub in Continuum.Forms.</summary>
        public bool HelpVisible { get; set; } = true;

        /// <summary>Gets or sets whether the toolbar is visible. Stub in Continuum.Forms.</summary>
        public bool ToolbarVisible { get; set; } = true;

        /// <summary>Gets or sets the sort order for properties.</summary>
        public PropertySort PropertySort { get; set; } = PropertySort.CategorizedAlphabetical;

        /// <summary>Raised when the selected property changes.</summary>
        public event EventHandler? SelectedGridItemChanged { add { } remove { } }

        /// <summary>Raised when the selected object changes.</summary>
        public event EventHandler? SelectedObjectsChanged { add { } remove { } }

        /// <summary>Raised when a property value changes.</summary>
        public event EventHandler? PropertyValueChanged { add { } remove { } }

        /// <summary>Gets the currently selected grid item. Stub in Continuum.Forms — returns null.</summary>
        public object? SelectedGridItem => null;

        /// <summary>Gets or sets whether commands pane is shown when available. Stub in Continuum.Forms.</summary>
        public bool CommandsVisibleIfAvailable { get; set; } = true;

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "PropertyGrid uses reflection at runtime; trimming is not supported for this control.")]
        private void RebuildEntries ()
        {
            _entries.Clear ();
            _selected_index = -1;

            if (_selected_object == null)
                return;

            var props = TypeDescriptor.GetProperties (_selected_object)
                .Cast<PropertyDescriptor> ()
                .Where (p => p.IsBrowsable);

            if (PropertySort == PropertySort.Alphabetical || PropertySort == PropertySort.CategorizedAlphabetical)
                props = props.OrderBy (p => p.Name);

            if (PropertySort == PropertySort.CategorizedAlphabetical || PropertySort == PropertySort.Categorized) {
                props = props.OrderBy (p => p.Category == "Misc" ? "zzz" : p.Category ?? "zzz")
                             .ThenBy (p => p.Name);
            }

            string? last_category = null;

            foreach (var prop in props) {
                var cat = prop.Category ?? "Misc";

                if (PropertySort == PropertySort.CategorizedAlphabetical || PropertySort == PropertySort.Categorized) {
                    if (cat != last_category) {
                        _entries.Add (new PropertyEntry { IsCategory = true, Name = cat });
                        last_category = cat;
                    }
                }

                string valueText;

                try {
                    var val = prop.GetValue (_selected_object);
                    valueText = val == null ? "(null)" : val.ToString () ?? string.Empty;
                } catch {
                    valueText = "(error)";
                }

                _entries.Add (new PropertyEntry {
                    IsCategory = false,
                    Name = prop.DisplayName,
                    Value = valueText
                });
            }

            AutoScrollMinSize = new Size (0, _entries.Count * ROW_HEIGHT);
        }

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            var y = e.Y - AutoScrollPosition.Y;
            var row = y / ROW_HEIGHT;

            if (row >= 0 && row < _entries.Count && !_entries[row].IsCategory) {
                _selected_index = row;
                Invalidate ();
            }
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            var g = e.Canvas;
            var r = ClientRectangle;

            if (_selected_object == null || _entries.Count == 0) {
                g.DrawText ("(no object selected)", Theme.UIFont, 11, r,
                    Theme.ForegroundColor, ContentAlignment.MiddleCenter);
                return;
            }

            var name_col_w = r.Width * NAME_COL_RATIO_PCT / 100;
            var scroll_y = AutoScrollPosition.Y;
            var line_color = new SKColor (LineColor.R, LineColor.G, LineColor.B, LineColor.A);
            var back_color = new SKColor (ViewBackColor.R, ViewBackColor.G, ViewBackColor.B);
            var fore_color = new SKColor (ViewForeColor.R, ViewForeColor.G, ViewForeColor.B);
            var cat_back = new SKColor (Theme.ControlMidColor.Red, Theme.ControlMidColor.Green,
                Theme.ControlMidColor.Blue, 255);
            var sel_back = new SKColor (Theme.AccentColor.Red, Theme.AccentColor.Green,
                Theme.AccentColor.Blue, Theme.AccentColor.Alpha);
            var sel_fore = new SKColor (Theme.ForegroundColorOnAccent.Red, Theme.ForegroundColorOnAccent.Green,
                Theme.ForegroundColorOnAccent.Blue, 255);

            for (var i = 0; i < _entries.Count; i++) {
                var row_y = scroll_y + i * ROW_HEIGHT;

                if (row_y + ROW_HEIGHT < 0 || row_y > r.Height)
                    continue;

                var row_rect = new Rectangle (r.X, row_y, r.Width, ROW_HEIGHT);
                var entry = _entries[i];

                if (entry.IsCategory) {
                    g.FillRectangle (new Rectangle (row_rect.X, row_rect.Y, row_rect.Width, row_rect.Height), cat_back);
                    g.DrawText (entry.Name, Theme.UIFont, 10,
                        new Rectangle (row_rect.X + 4, row_rect.Y, row_rect.Width - 4, row_rect.Height),
                        Theme.ForegroundColor, ContentAlignment.MiddleLeft);
                } else {
                    var is_selected = i == _selected_index;
                    var row_bg = is_selected ? sel_back : back_color;
                    var row_fg = is_selected ? sel_fore : fore_color;

                    g.FillRectangle (new Rectangle (row_rect.X, row_rect.Y, row_rect.Width, row_rect.Height), row_bg);

                    var name_rect = new Rectangle (row_rect.X + 2, row_rect.Y, name_col_w - 2, row_rect.Height);
                    var val_rect = new Rectangle (row_rect.X + name_col_w + 2, row_rect.Y,
                        row_rect.Width - name_col_w - 4, row_rect.Height);

                    g.DrawText (entry.Name, Theme.UIFont, 10, name_rect, row_fg, ContentAlignment.MiddleLeft);
                    g.DrawText (entry.Value, Theme.UIFont, 10, val_rect, row_fg, ContentAlignment.MiddleLeft);
                }

                // divider line
                g.DrawLine (row_rect.Left, row_rect.Bottom - 1, row_rect.Right, row_rect.Bottom - 1, line_color);
            }

            // column separator line
            g.DrawLine (r.X + name_col_w, r.Y, r.X + name_col_w, r.Y + _entries.Count * ROW_HEIGHT + scroll_y, line_color);
        }

        private sealed class PropertyEntry
        {
            public bool IsCategory;
            public string Name = string.Empty;
            public string Value = string.Empty;
        }
    }

}
