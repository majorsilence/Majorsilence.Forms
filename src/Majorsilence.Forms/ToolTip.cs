using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Displays a small popup of descriptive text when the mouse hovers over an associated control.
    /// </summary>
    public partial class ToolTip : Component
    {
        private readonly Dictionary<Control, string> tips = new ();
        private PopupWindow? popup;
        private TipLabel? popup_label;
        private Control? associated_control;
        private IWin32Window? associated_window;

        internal Size? PopupSize => popup?.Size;
        internal string? PopupText => popup_label?.Text;
        internal Label? PopupLabel => popup_label;

        // OwnerDraw (W6 mechanisms). The tip's label paints itself unless the application asked to
        // draw the tip, in which case its paint pass becomes the Draw event: the label's canvas, the
        // control the tip is for, and the tip's rectangle and text. Popup has already been asked and
        // may have resized the tip, so Bounds is the size the handler will actually be given.
        private sealed class TipLabel : Label
        {
            internal ToolTip? Owner { get; set; }

            // Fading (W6 mechanisms): the tip's content is composited at this alpha, which the owner's
            // fade timer walks from 0 to 255 after a show. 255 is fully opaque.
            internal byte Alpha { get; set; } = 255;

            // The glyph drawn at the leading edge for ToolTipIcon; the text is shifted past it.
            internal ToolTipIcon Icon { get; set; }

            internal const int IconInset = 20;

            protected override void OnPaint (PaintEventArgs e)
            {
                using var device = e.DeviceSpace ();   // laid out in device pixels (EVT-37)
                if (Owner is { OwnerDraw: true } owner && owner.RaiseDraw (this, e))
                    return;

                if (Alpha < 255) {
                    using var layer = new SKPaint { Color = new SKColor (0, 0, 0, Alpha) };
                    e.Canvas.SaveLayer (layer);
                } else
                    e.Canvas.Save ();

                if (Icon != ToolTipIcon.None) {
                    var box = e.LogicalToDeviceUnits (14);
                    var glyph = new Rectangle (e.LogicalToDeviceUnits (4), (ScaledHeight - box) / 2, box, box);
                    RenderIcon (e, Icon, glyph);
                    e.Canvas.Translate (e.LogicalToDeviceUnits (IconInset), 0);
                }

                base.OnPaint (e);
                e.Canvas.Restore ();
            }

            private static void RenderIcon (PaintEventArgs e, ToolTipIcon icon, Rectangle box)
            {
                if (Renderers.MessageGlyphs.For (icon) is { } glyph)
                    Renderers.MessageGlyphs.Draw (e.Canvas, glyph, box);
            }
        }

        // The fade (UseAnimation + UseFading): five steps of 20 ms from transparent to opaque.
        private Timer? fade_timer;

        /// <summary>The tip's current alpha, 0..255; 255 when no fade is running (tests).</summary>
        internal int TipAlpha => popup_label?.Alpha ?? 255;

        /// <summary>Advances a running fade by one step. The fade timer calls this every tick.</summary>
        internal void AdvanceFade ()
        {
            if (popup_label is not { } label)
                return;

            label.Alpha = (byte) Math.Min (255, label.Alpha + 51);
            label.Invalidate ();

            if (label.Alpha == 255)
                fade_timer?.Stop ();
        }

        private void BeginFade ()
        {
            if (popup_label is not { } label)
                return;

            if (!UseAnimation || !UseFading) {
                label.Alpha = 255;
                fade_timer?.Stop ();
                return;
            }

            label.Alpha = 0;
            fade_timer ??= new Timer { Interval = 20 };
            fade_timer.Tick -= FadeTimer_Tick;
            fade_timer.Tick += FadeTimer_Tick;
            fade_timer.Start ();
        }

        private void FadeTimer_Tick (object? sender, EventArgs e) => AdvanceFade ();

        internal bool RaiseDraw (Control surface, PaintEventArgs e)
        {
            if (Draw is null || associated_control is null)
                return false;

            var bounds = new Rectangle (0, 0, surface.ScaledWidth, surface.ScaledHeight);

            Draw (this, new DrawToolTipEventArgs (e.Graphics, associated_window ?? associated_control, associated_control,
                bounds, surface.Text, BackColor, ForeColor, surface.Font));

            return true;
        }

        /// <summary>Initializes a new instance of ToolTip.</summary>
        public ToolTip () { }

        /// <summary>Initializes a new instance of ToolTip and adds it to the specified container.</summary>
        public ToolTip (IContainer container) { container.Add (this); }

        /// <summary>Gets or sets whether the ToolTip is currently active.</summary>
        public bool Active { get; set; } = true;

        /// <summary>Gets or sets whether the ToolTip is shown even when its parent form is not active.</summary>
        /// <remarks>Read as of W6 mechanisms: off (upstream's default), a tip for a control on a form
        /// other than <see cref="Form.ActiveForm"/> is not shown.</remarks>
        public bool ShowAlways { get; set; }

        private int automatic_delay = 500;
        private int auto_pop_delay = 5000;
        private int reshow_delay = 100;
        private int initial_delay = 500;

        /// <summary>Gets or sets the automatic delay, in milliseconds. Setting this adjusts the other delays.</summary>
        public int AutomaticDelay {
            get => automatic_delay;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException (nameof (value), value, null);

                automatic_delay = value;
                AutoPopDelay = value * 10;
                InitialDelay = value;
                ReshowDelay = value / 5;
            }
        }

        /// <summary>Gets or sets the period, in milliseconds, that the ToolTip remains visible.</summary>
        public int AutoPopDelay {
            get => auto_pop_delay;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException (nameof (value), value, null);

                auto_pop_delay = value;
            }
        }

        /// <summary>Gets or sets the delay, in milliseconds, before a subsequent ToolTip appears.</summary>
        public int ReshowDelay {
            get => reshow_delay;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException (nameof (value), value, null);

                reshow_delay = value;
            }
        }

        /// <summary>Gets or sets the delay, in milliseconds, before the ToolTip first appears.</summary>
        public int InitialDelay {
            get => initial_delay;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException (nameof (value), value, null);

                initial_delay = value;
            }
        }

        /// <summary>Gets or sets the background color of the ToolTip.</summary>
        public Color BackColor { get; set; } = Color.FromArgb (255, 255, 255, 225);

        /// <summary>Gets or sets the foreground color of the ToolTip.</summary>
        public Color ForeColor { get; set; } = Color.Black;

        /// <summary>Sets the tip shown over a window's content.</summary>
        /// <remarks>Real as of W6 mechanisms: the tip is set on the control that fills the window, so
        /// the pointer anywhere over its content shows it. It used to do nothing, and this is the
        /// overload a caller reaches for when the thing they have is a Form.</remarks>
        public void SetToolTip (WindowBase window, string caption)
        {
            if (window?.ContentRoot is { } content)
                SetToolTip (content, caption);
        }

        /// <summary>
        /// Associates ToolTip text with the specified control.
        /// </summary>
        public void SetToolTip (Control control, string caption)
        {
            Guard.ThrowIfNull (control);

            control.MouseEnter -= Control_MouseEnter;
            control.MouseLeave -= Control_MouseLeave;
            control.MouseDown -= Control_MouseDown;

            if (string.IsNullOrEmpty (caption)) {
                tips.Remove (control);
                return;
            }

            tips[control] = caption;
            control.MouseEnter += Control_MouseEnter;
            control.MouseLeave += Control_MouseLeave;
            control.MouseDown += Control_MouseDown;
        }

        /// <summary>
        /// Returns the ToolTip text associated with the specified control.
        /// </summary>
        public string GetToolTip (Control control)
            => control is not null && tips.TryGetValue (control, out var t) ? t : string.Empty;

        /// <summary>
        /// Removes all ToolTip text associated with this component.
        /// </summary>
        public void RemoveAll ()
        {
            foreach (var control in tips.Keys) {
                control.MouseEnter -= Control_MouseEnter;
                control.MouseLeave -= Control_MouseLeave;
                control.MouseDown -= Control_MouseDown;
            }

            tips.Clear ();
            HidePopup ();
        }

        /// <summary>
        /// Hides any visible ToolTip for the specified control.
        /// </summary>
        public void Hide (Control control) => HidePopup ();

        // The IWin32Window overloads. That interface is implemented by Control and Form here, so the
        // owner is resolved back to a Control rather than the argument being dropped -- which is what
        // makes these do the same thing as the Control-typed ones rather than merely compile.

        /// <inheritdoc cref="Hide(Control)"/>
        public void Hide (IWin32Window win) => HidePopup ();

        /// <inheritdoc cref="Show(string,Control)"/>
        public void Show (string text, IWin32Window window) => ShowFor (text, window, null, 0);

        /// <inheritdoc cref="Show(string,Control,int)"/>
        public void Show (string text, IWin32Window window, int duration) => ShowFor (text, window, null, CheckDuration (duration));

        /// <inheritdoc cref="Show(string,Control,int,int)"/>
        public void Show (string text, IWin32Window window, int x, int y) => ShowFor (text, window, new System.Drawing.Point (x, y), 0);

        /// <inheritdoc cref="Show(string,Control,int,int,int)"/>
        public void Show (string text, IWin32Window window, int x, int y, int duration) => ShowFor (text, window, new System.Drawing.Point (x, y), CheckDuration (duration));

        /// <inheritdoc cref="Show(string,Control,int,int)"/>
        public void Show (string text, IWin32Window window, System.Drawing.Point point) => ShowFor (text, window, point, 0);

        /// <inheritdoc cref="Show(string,Control,int,int,int)"/>
        public void Show (string text, IWin32Window window, System.Drawing.Point point, int duration) => ShowFor (text, window, point, CheckDuration (duration));

        // Upstream validates every duration overload this way (ToolTip.cs, Show: ThrowIfNegative).
        private static int CheckDuration (int duration)
        {
            if (duration < 0)
                throw new ArgumentOutOfRangeException (nameof (duration), duration, null);

            return duration;
        }

        // The single path for every Show overload (TSM-17). Each one used to be SetToolTip: nothing was
        // displayed until the pointer next entered the control, the position and the duration were
        // dropped, and the control's own hover tip was overwritten with the hint. Upstream shows at once,
        // at the given point in the window's coordinates (or on the control when none is given), leaves
        // the control's tip alone, and hides after the duration when there is one
        // (ToolTip.cs, Show/ShowTooltip/StartTimer).
        private void ShowFor (string text, IWin32Window window, System.Drawing.Point? at, int duration)
        {
            Guard.ThrowIfNull (window);

            var control = window as Control ?? (window as WindowBase)?.ContentRoot;

            if (control is null)
                return;

            CancelPendingShow ();

            if (string.IsNullOrEmpty (text)) {
                HidePopup ();
                return;
            }

            // No point: the middle of the control, which is where upstream puts a tip whose control the
            // cursor is not over (ShowTooltip).
            var location = at ?? new System.Drawing.Point (control.Width / 2, control.Height / 2);

            ShowPopup (control, text, location, beside_cursor: false, hide_after: duration);
        }

        /// <summary>Gets or sets whether the ToolTip appears as a balloon. Stub in Majorsilence.Forms.</summary>
        public bool IsBalloon { get; set; }

        /// <summary>Gets or sets whether the ToolTip strips ampersands from the text. Stub in Majorsilence.Forms.</summary>
        public bool StripAmpersands { get; set; }

        /// <summary>Gets or sets whether the ToolTip uses animation.</summary>
        /// <remarks>Read as of W6 mechanisms: with this and <see cref="UseFading"/> on, a tip fades in
        /// over five 20 ms steps; off, it appears at once. There is no slide animation.</remarks>
        public bool UseAnimation { get; set; } = true;

        /// <summary>Gets or sets whether the ToolTip fades in. See <see cref="UseAnimation"/>.</summary>
        public bool UseFading { get; set; } = true;

        /// <summary>Gets or sets the title text for balloon tooltips. Stub in Majorsilence.Forms.</summary>
        public string ToolTipTitle { get; set; } = string.Empty;

        /// <summary>Gets or sets the icon shown beside the tooltip text.</summary>
        /// <remarks>Drawn as of W6 mechanisms: an information, warning or error glyph at the tip's
        /// leading edge, with the text shifted past it.</remarks>
        public ToolTipIcon ToolTipIcon { get; set; } = ToolTipIcon.None;

        /// <summary>Shows a ToolTip with the given text over the control, at once, until it is hidden.</summary>
        /// <remarks>The tip sits in the middle of the control. It does not replace the control's own
        /// hover tip (<see cref="SetToolTip(Control, string)"/>), as upstream's does not.</remarks>
        public void Show (string text, Control control) => ShowFor (text, control, null, 0);

        /// <summary>Shows a ToolTip with the given text over the control for <paramref name="duration"/> milliseconds.</summary>
        public void Show (string text, Control control, int duration) => ShowFor (text, control, null, CheckDuration (duration));

        /// <summary>Shows a ToolTip with the given text at a point in the control's client coordinates.</summary>
        public void Show (string text, Control control, int x, int y) => ShowFor (text, control, new System.Drawing.Point (x, y), 0);

        /// <summary>Shows a ToolTip with the given text at a point in the control's client coordinates for <paramref name="duration"/> milliseconds.</summary>
        public void Show (string text, Control control, int x, int y, int duration) => ShowFor (text, control, new System.Drawing.Point (x, y), CheckDuration (duration));

        // ── the hover delays (TSM-34) ─────────────────────────────────────────────────────────────
        // InitialDelay, AutoPopDelay and ReshowDelay were validated, stored and read by nothing: a tip
        // appeared the instant the pointer crossed the control and stayed until it left. Upstream hands
        // them to the native tip (TTM_SETDELAYTIME); here they are two timers. A delay of zero acts at
        // once, since a Timer cannot run with a zero interval.
        private Timer? show_timer;
        private Timer? hide_timer;
        private Control? pending_control;
        private int last_tip_left_at = int.MinValue;
        private bool tip_was_left;

        /// <summary>The delay the pending hover show is waiting out, or null when none is (tests).</summary>
        internal int? PendingShowDelay { get; private set; }

        /// <summary>Whether a hide is scheduled for the visible tip (tests).</summary>
        internal bool IsHidePending => hide_timer?.Enabled == true;

        /// <summary>Whether the tip window is on screen (tests).</summary>
        internal bool IsTipShown => popup?.Visible == true;

        /// <summary>Runs out the pending show delay now; the show timer calls this when it fires.</summary>
        internal void ElapseShowDelay ()
        {
            var control = pending_control;

            CancelPendingShow ();

            if (control is not null && Active && tips.TryGetValue (control, out var text) && !string.IsNullOrEmpty (text))
                ShowPopup (control, text, control.LastMousePosition, beside_cursor: true, hide_after: AutoPopDelay);
        }

        /// <summary>Runs out the visible tip's time now; the hide timer calls this when it fires.</summary>
        internal void ElapseHideDelay () => HidePopup ();

        private void CancelPendingShow ()
        {
            show_timer?.Stop ();
            pending_control = null;
            PendingShowDelay = null;
        }

        private void StartHideTimer (int milliseconds)
        {
            hide_timer?.Stop ();

            if (milliseconds <= 0)
                return;

            hide_timer ??= new Timer ();
            hide_timer.Tick -= HideTimer_Tick;
            hide_timer.Tick += HideTimer_Tick;
            hide_timer.Interval = milliseconds;
            hide_timer.Start ();
        }

        private void ShowTimer_Tick (object? sender, EventArgs e) => ElapseShowDelay ();

        private void HideTimer_Tick (object? sender, EventArgs e) => ElapseHideDelay ();

        private void Control_MouseEnter (object? sender, EventArgs e)
        {
            if (!Active || sender is not Control control)
                return;

            if (!tips.TryGetValue (control, out var text) || string.IsNullOrEmpty (text))
                return;

            // ReshowDelay is the wait when the pointer moves on from one tip to the next control: a tip
            // was up when the pointer left the last one, and the pointer arrived here within the
            // initial delay of that. Otherwise the full InitialDelay.
            var reshow = tip_was_left && unchecked (Environment.TickCount - last_tip_left_at) < InitialDelay;
            var delay = reshow ? ReshowDelay : InitialDelay;

            CancelPendingShow ();

            if (delay <= 0) {
                ShowPopup (control, text, control.LastMousePosition, beside_cursor: true, hide_after: AutoPopDelay);
                return;
            }

            pending_control = control;
            PendingShowDelay = delay;

            show_timer ??= new Timer ();
            show_timer.Tick -= ShowTimer_Tick;
            show_timer.Tick += ShowTimer_Tick;
            show_timer.Interval = delay;
            show_timer.Start ();
        }

        private void Control_MouseLeave (object? sender, EventArgs e)
        {
            tip_was_left = IsTipShown;
            last_tip_left_at = Environment.TickCount;

            CancelPendingShow ();
            HidePopup ();
        }

        private void Control_MouseDown (object? sender, MouseEventArgs e)
        {
            CancelPendingShow ();
            HidePopup ();
        }

        // The per-ITEM entry points. Control drives these from its mouse-move path when a control
        // reports tip text for the point under the cursor -- a cell, an item, a node, a tab, a strip
        // button. SetToolTip's MouseEnter/MouseLeave pair cannot express that: the text has to change
        // as the pointer moves WITHIN one control, and the popup has to follow it. AutoPopDelay applies
        // to these as to any hover tip.
        internal void ShowItemTip (Control control, string text, System.Drawing.Point at)
            => ShowPopup (control, text, at, beside_cursor: true, hide_after: AutoPopDelay);

        internal void HideItemTip () => HidePopup ();

        // beside_cursor: a hover tip is offset below-right of the pointer; an explicit Show puts the
        // tip's corner at the point it was given. hide_after: milliseconds until it hides, 0 for never.
        private void ShowPopup (Control control, string text, System.Drawing.Point at, bool beside_cursor, int hide_after)
        {
            try {
                var window = control.FindWindow ();

                if (window is null)
                    return;

                // ShowAlways (W6 mechanisms): an inactive form's controls show no tip unless asked to.
                if (!ShowAlways && window is Form owner_form && Form.ActiveForm is { } active_form && !ReferenceEquals (active_form, owner_form))
                    return;

                if (popup is null || popup_label is null) {
                    popup = new PopupWindow (window, activates: false) { IsToolTip = true };
                    popup_label = popup.Controls.Add (new TipLabel { Dock = DockStyle.Fill, Owner = this });
                    popup_label.Style.Border.Width = 1;
                }

                associated_control = control;
                associated_window = window as IWin32Window;
                popup.AccessibleOwner = control;

                // StripAmpersands drops the mnemonic marker a caller copied from a button's Text;
                // ToolTipTitle heads the tip on its own line (W6.2 sweep).
                if (StripAmpersands)
                    text = text.Replace ("&&", "\u0001").Replace ("&", string.Empty).Replace ("\u0001", "&");

                if (!string.IsNullOrEmpty (ToolTipTitle))
                    text = ToolTipTitle + Environment.NewLine + text;

                popup_label.Text = text;
                popup_label.Style.BackgroundColor = BackColor.ToSKColor ();
                popup_label.Style.ForegroundColor = ForeColor.ToSKColor ();

                // ToolTipIcon (W6 mechanisms): the glyph takes a band at the leading edge.
                popup_label.Icon = ToolTipIcon;

                var measured = TextMeasurer.MeasureText (text, Theme.UIFont, Theme.FontSize);
                var size = new Size ((int)measured.Width + 16 + (ToolTipIcon == ToolTipIcon.None ? 0 : TipLabel.IconInset), (int)measured.Height + 10);

                // Upstream asks before every show and a cancelled Popup shows nothing (W6.1 sweep).
                var popup_args = new PopupEventArgs (window as IWin32Window ?? control, control, IsBalloon, size);
                Popup?.Invoke (this, popup_args);


                if (popup_args.Cancel) {
                    HidePopup ();
                    return;
                }

                // A handler may resize the tip through the args, as upstream honours (W6.2 sweep).
                popup.Size = popup_args.ToolTipSize;

                BeginFade ();

                // Offset below/right of the cursor like a standard tooltip.
                if (beside_cursor)
                    popup.Show (control, at.X + 12, at.Y + 18);
                else
                    popup.Show (control, at.X, at.Y);

                StartHideTimer (hide_after);
            } catch {
                // A tooltip must never take down the host application.
            }
        }

        private void HidePopup ()
        {
            try {
                fade_timer?.Stop ();
                hide_timer?.Stop ();
                popup?.Hide ();
            } catch {
            }
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                RemoveAll ();
                CancelPendingShow ();
                popup?.Hide ();
                show_timer?.Dispose ();
                hide_timer?.Dispose ();
            }

            base.Dispose (disposing);
        }
    }
}
