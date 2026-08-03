using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Majorsilence.Forms
{
    // ControlPaint, DataFormats, DataObject, ListBox and the two composite ToolStrip items
    // (docs/winforms-gap-plan.md).
    //
    // DataObject's typed accessors are the ones worth having: ContainsImage/GetImage/SetImage and the
    // file-drop list are how ordinary drag-and-drop and clipboard code is written, and they are real
    // here because the underlying store is a format-keyed dictionary that can hold anything.
    //
    // ControlPaint's remaining members are the GDI ones -- reversible XOR drawing straight onto the
    // screen DC, and the three CreateHBitmap* methods that hand back a Win32 handle. Neither has a
    // counterpart on a Skia surface, and a handle cannot be invented, so those are the exceptions and
    // each says so.

    public partial class ControlPaint
    {
        /// <summary>Gets the colour used to darken a control's background for contrast.</summary>
        public static Color ContrastControlDark => SystemColors.ControlDark;

        /// <summary>Draws a caption button such as the close or minimise glyph.</summary>
        public static void DrawCaptionButton (Graphics graphics, Rectangle rectangle, CaptionButton button, ButtonState state) { }

        /// <inheritdoc cref="DrawCaptionButton(Graphics,Rectangle,CaptionButton,ButtonState)"/>
        public static void DrawCaptionButton (Graphics graphics, int x, int y, int width, int height, CaptionButton button, ButtonState state)
            => DrawCaptionButton (graphics, new Rectangle (x, y, width, height), button, state);

        /// <summary>Draws the grab handle of a container being resized in a designer.</summary>
        public static void DrawContainerGrabHandle (Graphics graphics, Rectangle bounds) { }

        /// <summary>Draws a designer grab handle.</summary>
        public static void DrawGrabHandle (Graphics graphics, Rectangle rectangle, bool primary, bool enabled) { }

        /// <summary>Draws the dashed frame that marks a locked designer control.</summary>
        public static void DrawLockedFrame (Graphics graphics, Rectangle rectangle, bool primary) { }

        /// <summary>Draws the frame around a selected designer control.</summary>
        public static void DrawSelectionFrame (Graphics graphics, bool active, Rectangle outsideRect, Rectangle insideRect, Color backColor) { }

        /// <summary>Draws a check box in its indeterminate state.</summary>
        public static void DrawMixedCheckBox (Graphics graphics, Rectangle rectangle, ButtonState state) { }

        /// <inheritdoc cref="DrawMixedCheckBox(Graphics,Rectangle,ButtonState)"/>
        public static void DrawMixedCheckBox (Graphics graphics, int x, int y, int width, int height, ButtonState state)
            => DrawMixedCheckBox (graphics, new Rectangle (x, y, width, height), state);

        /// <summary>Draws a border in the current visual style.</summary>
        public static void DrawVisualStyleBorder (Graphics graphics, Rectangle bounds)
            => DrawBorder (graphics, bounds, SystemColors.ControlDark, ButtonBorderStyle.Solid);

        /// <summary>Draws an image greyed out, the way a disabled control's image is drawn.</summary>
        /// <remarks>Unlike most of this class's stubs, this one draws: it desaturates the image and
        /// blends it towards the background, which is what the disabled state looks like.</remarks>
        public static void DrawImageDisabled (Graphics graphics, Majorsilence.Forms.Drawing.Image image, int x, int y, Color background)
        {
            ArgumentNullException.ThrowIfNull (graphics);

            if (image is null)
                return;

            using var disabled = ToolStripRenderer.CreateDisabledImage (image);

            if (disabled is not null)
                graphics.DrawImage (disabled, x, y);
        }

        // The reversible-drawing family. Every one of these XORs directly onto the screen's device
        // context so a drag outline can be erased by drawing it a second time. There is no screen DC
        // behind a Skia surface and nothing to XOR against, so these do nothing rather than drawing
        // something that could never be erased -- which would leave artefacts on screen.

        /// <summary>Draws a reversible frame, used for drag outlines. No-op in Majorsilence.Forms.</summary>
        public static void DrawReversibleFrame (Rectangle rectangle, Color backColor, FrameStyle style) { }

        /// <summary>Draws a reversible line. No-op in Majorsilence.Forms; see <see cref="DrawReversibleFrame"/>.</summary>
        public static void DrawReversibleLine (Point start, Point end, Color backColor) { }

        /// <summary>Fills a reversible rectangle. No-op in Majorsilence.Forms; see <see cref="DrawReversibleFrame"/>.</summary>
        public static void FillReversibleRectangle (Rectangle rectangle, Color backColor) { }

        // The three CreateHBitmap* methods hand back a Win32 GDI bitmap handle. There is no GDI here,
        // and IntPtr.Zero is what a Win32 caller already has to check for after a failed creation.

        /// <summary>Creates a 16-bit GDI bitmap handle. Returns zero in Majorsilence.Forms.</summary>
        public static IntPtr CreateHBitmap16Bit (Majorsilence.Forms.Drawing.Bitmap bitmap, Color background) => IntPtr.Zero;

        /// <inheritdoc cref="CreateHBitmap16Bit"/>
        public static IntPtr CreateHBitmapColorMask (Majorsilence.Forms.Drawing.Bitmap bitmap, IntPtr monochromeMask) => IntPtr.Zero;

        /// <inheritdoc cref="CreateHBitmap16Bit"/>
        public static IntPtr CreateHBitmapTransparencyMask (Majorsilence.Forms.Drawing.Bitmap bitmap) => IntPtr.Zero;
    }

    public partial class DataFormats
    {
        /// <summary>The device-independent bitmap format.</summary>
        public static DataFormat Dib { get; } = new DataFormat ("DeviceIndependentBitmap", 8);

        /// <summary>The data interchange format.</summary>
        public static DataFormat Dif { get; } = new DataFormat ("DataInterchangeFormat", 5);

        /// <summary>The enhanced metafile format.</summary>
        public static DataFormat EnhancedMetafile { get; } = new DataFormat ("EnhancedMetafile", 14);

        /// <summary>The locale identifier format.</summary>
        public static DataFormat Locale { get; } = new DataFormat ("Locale", 16);

        /// <summary>The Windows metafile picture format.</summary>
        public static DataFormat MetafilePict { get; } = new DataFormat ("MetaFilePict", 3);

        /// <summary>The colour palette format.</summary>
        public static DataFormat Palette { get; } = new DataFormat ("Palette", 9);

        /// <summary>The pen data format.</summary>
        public static DataFormat PenData { get; } = new DataFormat ("PenData", 10);

        /// <summary>The resource interchange file format.</summary>
        public static DataFormat Riff { get; } = new DataFormat ("RiffAudio", 11);

        /// <summary>The format used for objects serialised by the framework.</summary>
        public static DataFormat Serializable { get; } = new DataFormat ("WindowsForms10PersistentObject", 0xC010);

        /// <summary>The Windows string format.</summary>
        public static DataFormat StringFormat { get; } = new DataFormat ("System.String", 0xC011);

        /// <summary>The symbolic link format.</summary>
        public static DataFormat SymbolicLink { get; } = new DataFormat ("SymbolicLink", 4);

        /// <summary>The tagged image file format.</summary>
        public static DataFormat Tiff { get; } = new DataFormat ("TaggedImageFileFormat", 6);

        /// <summary>The wave audio format.</summary>
        public static DataFormat WaveAudio { get; } = new DataFormat ("WaveAudio", 12);
    }

    public partial class DataObject
    {
        /// <summary>Returns whether the object holds an image.</summary>
        public bool ContainsImage () => GetDataPresent (DataFormats.Bitmap.Name);

        /// <summary>Returns whether the object holds audio.</summary>
        public bool ContainsAudio () => GetDataPresent (DataFormats.WaveAudio.Name);

        /// <summary>Returns whether the object holds a list of dropped files.</summary>
        public bool ContainsFileDropList () => GetDataPresent (DataFormats.FileDrop.Name);

        /// <summary>Returns the stored image, or null.</summary>
        public Majorsilence.Forms.Drawing.Image? GetImage () => GetData (DataFormats.Bitmap.Name) as Majorsilence.Forms.Drawing.Image;

        /// <summary>Returns the stored audio as a stream, or null.</summary>
        public Stream? GetAudioStream () => GetData (DataFormats.WaveAudio.Name) as Stream;

        /// <summary>Returns the stored file paths; an empty collection when there are none.</summary>
        public StringCollection GetFileDropList ()
        {
            var paths = new StringCollection ();

            if (GetData (DataFormats.FileDrop.Name) is string[] files)
                paths.AddRange (files);

            return paths;
        }

        /// <summary>Stores an image.</summary>
        public void SetImage (Majorsilence.Forms.Drawing.Image image) => SetData (DataFormats.Bitmap.Name, image);

        /// <summary>Stores audio as a byte array.</summary>
        public void SetAudio (byte[] audioBytes) => SetData (DataFormats.WaveAudio.Name, new MemoryStream (audioBytes));

        /// <inheritdoc cref="SetAudio(byte[])"/>
        public void SetAudio (Stream audioStream) => SetData (DataFormats.WaveAudio.Name, audioStream);

        /// <summary>Stores a list of file paths.</summary>
        public void SetFileDropList (StringCollection filePaths)
        {
            ArgumentNullException.ThrowIfNull (filePaths);

            var paths = new string[filePaths.Count];
            filePaths.CopyTo (paths, 0);
            SetData (DataFormats.FileDrop.Name, paths);
        }

        /// <summary>Stores a value serialised as JSON under the given format.</summary>
        /// <remarks>Upstream added this so a data object can carry arbitrary values without the
        /// binary formatter.</remarks>
        [RequiresUnreferencedCode ("The stored type is serialised with reflection, as it is upstream.")]
        public void SetDataAsJson<T> (string format, T data)
            => SetData (format, System.Text.Json.JsonSerializer.Serialize (data));

        /// <inheritdoc cref="SetDataAsJson{T}(string,T)"/>
        [RequiresUnreferencedCode ("The stored type is serialised with reflection, as it is upstream.")]
        public void SetDataAsJson<T> (T data) => SetDataAsJson (typeof (T).FullName ?? typeof (T).Name, data);

        /// <summary>Returns the stored value when it is of the requested type.</summary>
        public bool TryGetData<T> (string format, out T? data)
        {
            if (GetData (format) is T stored) {
                data = stored;
                return true;
            }

            data = default;
            return false;
        }

        /// <inheritdoc cref="TryGetData{T}(string,out T)"/>
        public bool TryGetData<T> (out T? data) => TryGetData (typeof (T).FullName ?? typeof (T).Name, out data);
    }

    public partial class ListBox
    {
        /// <summary>Gets or sets the border drawn around the control.</summary>
        public BorderStyle BorderStyle { get; set; } = BorderStyle.Fixed3D;

        /// <summary>Gets the height a single item is drawn at by default.</summary>
        public const int DefaultItemHeight = 13;

        /// <summary>The index a string search returns when nothing matched.</summary>
        public const int NoMatches = -1;

        /// <summary>Gets or sets whether <see cref="CustomTabOffsets"/> is used.</summary>
        public bool UseCustomTabOffsets { get; set; }

        /// <summary>Gets the tab stops used when <see cref="UseCustomTabOffsets"/> is set.</summary>
        public IntegerCollection CustomTabOffsets => custom_tab_offsets ??= new IntegerCollection ();

        private IntegerCollection? custom_tab_offsets;

        /// <summary>Gets the height the control would like to be to show all its items.</summary>
        public int PreferredHeight => Items.Count * GetItemHeight (0);

        /// <summary>Gets the height of the item at the given index.</summary>
        /// <remarks>Every item is the same height here; variable-height owner draw is not implemented,
        /// so the index is validated but does not change the answer.</remarks>
        public int GetItemHeight (int index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative (index);
            return ItemHeight;
        }

        /// <summary>Returns the index of the item at the given point, or -1.</summary>
        public int IndexFromPoint (Point p) => IndexFromPoint (p.X, p.Y);

        /// <inheritdoc cref="IndexFromPoint(Point)"/>
        public int IndexFromPoint (int x, int y)
        {
            if (!ClientRectangle.Contains (x, y))
                return -1;

            var index = y / Math.Max (1, ItemHeight);
            return index >= 0 && index < Items.Count ? index : -1;
        }

        /// <summary>A collection of integers, used for a list box's custom tab stops.</summary>
        public class IntegerCollection : IList<int>
        {
            private readonly List<int> values = [];

            /// <inheritdoc/>
            public int Count => values.Count;

            /// <inheritdoc/>
            public bool IsReadOnly => false;

            /// <inheritdoc/>
            public int this[int index] {
                get => values[index];
                set => values[index] = value;
            }

            /// <inheritdoc/>
            public void Add (int item) => values.Add (item);

            /// <summary>Adds several values at once.</summary>
            public void AddRange (params int[] items)
            {
                ArgumentNullException.ThrowIfNull (items);
                values.AddRange (items);
            }

            /// <inheritdoc/>
            public void Clear () => values.Clear ();

            /// <inheritdoc/>
            public bool Contains (int item) => values.Contains (item);

            /// <inheritdoc/>
            public void CopyTo (int[] array, int arrayIndex) => values.CopyTo (array, arrayIndex);

            /// <inheritdoc/>
            public int IndexOf (int item) => values.IndexOf (item);

            /// <inheritdoc/>
            public void Insert (int index, int item) => values.Insert (index, item);

            /// <inheritdoc/>
            public bool Remove (int item) => values.Remove (item);

            /// <inheritdoc/>
            public void RemoveAt (int index) => values.RemoveAt (index);

            /// <inheritdoc/>
            public IEnumerator<int> GetEnumerator () => values.GetEnumerator ();

            IEnumerator IEnumerable.GetEnumerator () => GetEnumerator ();
        }
    }

    public partial class ToolStripSplitButton
    {
        /// <summary>Gets the bounds of the button half.</summary>
        public Rectangle ButtonBounds {
            get {
                var bounds = new Rectangle (Point.Empty, Size);
                return new Rectangle (bounds.X, bounds.Y, Math.Max (0, bounds.Width - DropDownButtonWidth), bounds.Height);
            }
        }

        /// <summary>Gets the bounds of the drop-down arrow half.</summary>
        public Rectangle DropDownButtonBounds {
            get {
                var bounds = new Rectangle (Point.Empty, Size);
                return new Rectangle (bounds.Right - DropDownButtonWidth, bounds.Y, DropDownButtonWidth, bounds.Height);
            }
        }

        /// <summary>Gets the bounds of the divider between the two halves.</summary>
        public Rectangle SplitterBounds {
            get {
                var bounds = new Rectangle (Point.Empty, Size);
                return new Rectangle (bounds.Right - DropDownButtonWidth, bounds.Y + 2, 1, Math.Max (0, bounds.Height - 4));
            }
        }

        /// <summary>Gets whether the button half is pressed.</summary>
        public bool ButtonPressed => Pressed && !DropDownButtonPressed;

        /// <summary>Gets whether the button half is highlighted.</summary>
        public bool ButtonSelected => Selected && !DropDownButtonSelected;

        /// <summary>Gets whether the drop-down half is pressed.</summary>
        public bool DropDownButtonPressed => IsDropDownOpened;

        /// <summary>Gets whether the drop-down half is highlighted.</summary>
        public bool DropDownButtonSelected => Selected && Hovered;

        /// <summary>Raises the button half's click without opening the drop-down.</summary>
        public void PerformButtonClick ()
        {
            if (Enabled)
                PerformClick ();
        }

        /// <summary>Restores <c>DropDownButtonWidth</c> to its default.</summary>
        public virtual void ResetDropDownButtonWidth () => DropDownButtonWidth = 11;

        /// <summary>Raises the <see cref="ToolStripSplitButton.ButtonDoubleClick"/> event.</summary>
        protected virtual void OnButtonDoubleClick (EventArgs e) => ButtonDoubleClick?.Invoke (this, e);

        // ButtonDoubleClick already exists on this type; OnButtonDoubleClick above is the raiser it
        // never had.
#pragma warning disable CS0067
        /// <summary>Raised when the item's default item changes. Not raised by this layer yet.</summary>
        public event EventHandler? DefaultItemChanged;
#pragma warning restore CS0067
    }

    public partial class ToolStripProgressBar
    {
        /// <summary>Gets the hosted progress bar.</summary>
        public ProgressBar ProgressBar => hosted_progress_bar ??= new ProgressBar ();

        private ProgressBar? hosted_progress_bar;

        /// <summary>Gets or sets how far <see cref="PerformStep"/> advances the value.</summary>
        public int Step {
            get => ProgressBar.Step;
            set => ProgressBar.Step = value;
        }

        /// <summary>Gets or sets how fast the marquee block moves, in milliseconds per step.</summary>
        public int MarqueeAnimationSpeed {
            get => ProgressBar.MarqueeAnimationSpeed;
            set => ProgressBar.MarqueeAnimationSpeed = value;
        }

        /// <summary>Gets or sets whether the bar fills right to left when RightToLeft is set.</summary>
        public virtual bool RightToLeftLayout {
            get => right_to_left_layout;
            set {
                if (right_to_left_layout == value)
                    return;

                right_to_left_layout = value;
                RightToLeftLayoutChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        private bool right_to_left_layout;

        /// <summary>Advances the value by <see cref="Step"/>.</summary>
        public void PerformStep () => ProgressBar.PerformStep ();

        /// <summary>Advances the value by the given amount.</summary>
        public void Increment (int value) => ProgressBar.Increment (value);

        /// <summary>Raised when <see cref="RightToLeftLayout"/> changes.</summary>
        public event EventHandler? RightToLeftLayoutChanged;

        // A progress bar takes no keyboard input and has nothing to validate; WinForms declares these
        // only so the designer can hide them.
#pragma warning disable CS0067
        /// <summary>Not raised: a progress bar takes no keyboard input.</summary>
        public event KeyEventHandler? KeyDown;

        /// <inheritdoc cref="KeyDown"/>
        public event KeyPressEventHandler? KeyPress;

        /// <inheritdoc cref="KeyDown"/>
        public event KeyEventHandler? KeyUp;

        /// <summary>Not raised: a progress bar has nothing to validate.</summary>
        public event EventHandler? Validated;

        /// <inheritdoc cref="Validated"/>
        public event System.ComponentModel.CancelEventHandler? Validating;
#pragma warning restore CS0067
    }
}
