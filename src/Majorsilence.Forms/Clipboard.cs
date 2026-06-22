using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Class for interacting with an operating system's clipboard.
    /// </summary>
    public static class Clipboard
    {
        /// <summary>Gets the contents of the clipboard as text.</summary>
        public static Task<string?> GetTextAsync ()
            => Task.FromResult<string?> (Platform.Backend.GetClipboardText ());

        /// <summary>Sets the text contents of the clipboard.</summary>
        public static Task SetTextAsync (string text)
        {
            Platform.Backend.SetClipboardText (text);
            return Task.CompletedTask;
        }

        /// <summary>Clears the contents of the clipboard.</summary>
        public static Task ClearAsync ()
        {
            Platform.Backend.ClearClipboard ();
            return Task.CompletedTask;
        }

        // --- WinForms sync compatibility wrappers ---

        /// <summary>Gets the text contents of the clipboard synchronously.</summary>
        public static string GetText () => Platform.Backend.GetClipboardText ();

        /// <summary>Sets the text contents of the clipboard synchronously.</summary>
        public static void SetText (string text) => Platform.Backend.SetClipboardText (text);

        /// <summary>Clears the clipboard synchronously.</summary>
        public static void Clear () => Platform.Backend.ClearClipboard ();

        /// <summary>Returns whether the clipboard contains text.</summary>
        public static bool ContainsText () => !string.IsNullOrEmpty (GetText ());

        /// <summary>Returns whether the clipboard contains text in the specified format.</summary>
        public static bool ContainsText (TextDataFormat format) => ContainsText ();

        /// <summary>Returns whether the clipboard contains an image. Stub in Majorsilence.Forms — always returns false.</summary>
        public static bool ContainsImage () => false;

        /// <summary>Returns the image on the clipboard, or null. Stub in Majorsilence.Forms — always returns null.</summary>
        public static Majorsilence.Drawing.Image? GetImage () => null;

        /// <summary>Places an image on the clipboard. Stub in Majorsilence.Forms — no-op.</summary>
        public static void SetImage (Majorsilence.Drawing.Image image) { }

        /// <summary>Gets the text on the clipboard for the specified format.</summary>
        public static string GetText (TextDataFormat format) => GetText ();

        /// <summary>Sets the text on the clipboard in the specified format.</summary>
        public static void SetText (string text, TextDataFormat format) => SetText (text);

        /// <summary>Sets an IDataObject on the clipboard. Stub — stores text if the object supports it.</summary>
        public static void SetDataObject (object data, bool copy = false)
        {
            if (data is IDataObject dataObj) {
                var text = dataObj.GetData (DataFormats.Text.Name) as string;
                if (!string.IsNullOrEmpty (text))
                    SetText (text!);
            } else if (data is string s) {
                SetText (s);
            }
        }

        /// <summary>Gets an IDataObject from the clipboard. Returns a text-only stub.</summary>
        public static IDataObject GetDataObject () => new ClipboardDataObject (GetText ());

        /// <summary>Returns whether the clipboard contains data in the specified format.</summary>
        public static bool ContainsData (string format)
            => string.Equals (format, DataFormats.Text.Name, StringComparison.OrdinalIgnoreCase) ? ContainsText () : false;

        /// <summary>Sets data on the clipboard as a named format. Stub — stores text only.</summary>
        public static void SetData (string format, object? data)
        {
            if (data is string s)
                SetText (s);
        }

        /// <summary>Gets data from the clipboard in the specified format. Stub — returns text only.</summary>
        public static object? GetData (string format)
            => string.Equals (format, DataFormats.Text.Name, StringComparison.OrdinalIgnoreCase) ? (object?)GetText () : null;

        private sealed class ClipboardDataObject : IDataObject
        {
            private readonly string _text;
            public ClipboardDataObject (string text) => _text = text;
            public object? GetData (string format) => format == DataFormats.Text.Name ? (object)_text : null;
            public object? GetData (Type format) => null;
            public object? GetData (string format, bool autoConvert) => GetData (format);
            public bool GetDataPresent (string format) => format == DataFormats.Text.Name && !string.IsNullOrEmpty (_text);
            public bool GetDataPresent (Type format) => false;
            public bool GetDataPresent (string format, bool autoConvert) => GetDataPresent (format);
            public string[] GetFormats () => string.IsNullOrEmpty (_text) ? [] : [DataFormats.Text.Name];
            public string[] GetFormats (bool autoConvert) => GetFormats ();
            public void SetData (object data) { }
            public void SetData (string format, object? data) { }
            public void SetData (Type format, object? data) { }
            public void SetData (string format, bool autoConvert, object? data) { }
        }
    }

    /// <summary>Specifies the clipboard text data format.</summary>
    public enum TextDataFormat
    {
        /// <summary>Unicode text format.</summary>
        UnicodeText,
        /// <summary>Regular text format.</summary>
        Text,
        /// <summary>RTF text format.</summary>
        Rtf,
        /// <summary>HTML text format.</summary>
        Html,
        /// <summary>CommaSeparatedValue format.</summary>
        CommaSeparatedValue
    }

    /// <summary>Defines a format-independent mechanism for transferring data.</summary>
    public interface IDataObject
    {
        /// <summary>Gets the data in the specified format.</summary>
        object? GetData (string format);
        /// <summary>Gets the data in the specified type format.</summary>
        object? GetData (Type format);
        /// <summary>Gets the data in the specified format, optionally converting it.</summary>
        object? GetData (string format, bool autoConvert);
        /// <summary>Returns whether data is present in the specified format.</summary>
        bool GetDataPresent (string format);
        /// <summary>Returns whether data is present in the specified type format.</summary>
        bool GetDataPresent (Type format);
        /// <summary>Returns whether data is present in the specified format, optionally converting.</summary>
        bool GetDataPresent (string format, bool autoConvert);
        /// <summary>Returns a list of all formats the data is stored in.</summary>
        string[] GetFormats ();
        /// <summary>Returns a list of all formats, optionally converting.</summary>
        string[] GetFormats (bool autoConvert);
        /// <summary>Stores the specified data in this object.</summary>
        void SetData (object data);
        /// <summary>Stores the specified data in the specified format.</summary>
        void SetData (string format, object? data);
        /// <summary>Stores the specified data using the specified type as the format.</summary>
        void SetData (Type format, object? data);
        /// <summary>Stores the specified data and indicates whether the data can be converted to another format.</summary>
        void SetData (string format, bool autoConvert, object? data);
    }

    /// <summary>Implements <see cref="IDataObject"/> for transferring data. Stub in Majorsilence.Forms.</summary>
    public class DataObject : IDataObject
    {
        private readonly System.Collections.Generic.Dictionary<string, object?> _data = new ();

        /// <summary>Initializes an empty DataObject.</summary>
        public DataObject () { }

        /// <summary>Initializes a DataObject with a text value.</summary>
        public DataObject (string data) => SetData (DataFormats.Text.Name, data);

        /// <summary>Initializes a DataObject with data in the specified format.</summary>
        public DataObject (string format, object? data) => SetData (format, data);

        /// <inheritdoc/>
        public object? GetData (string format) => _data.TryGetValue (format, out var v) ? v : null;
        /// <inheritdoc/>
        public object? GetData (Type format) => _data.TryGetValue (format.FullName ?? format.Name, out var v) ? v : null;
        /// <inheritdoc/>
        public object? GetData (string format, bool autoConvert) => GetData (format);
        /// <inheritdoc/>
        public bool GetDataPresent (string format) => _data.ContainsKey (format);
        /// <inheritdoc/>
        public bool GetDataPresent (Type format) => _data.ContainsKey (format.FullName ?? format.Name);
        /// <inheritdoc/>
        public bool GetDataPresent (string format, bool autoConvert) => GetDataPresent (format);
        /// <inheritdoc/>
        public string[] GetFormats () => _data.Keys.ToArray ();
        /// <inheritdoc/>
        public string[] GetFormats (bool autoConvert) => GetFormats ();
        /// <inheritdoc/>
        public void SetData (object data) => _data[data.GetType ().FullName ?? data.GetType ().Name] = data;
        /// <inheritdoc/>
        public void SetData (string format, object? data) => _data[format] = data;
        /// <inheritdoc/>
        public void SetData (Type format, object? data) => _data[format.FullName ?? format.Name] = data;
        /// <inheritdoc/>
        public void SetData (string format, bool autoConvert, object? data) => SetData (format, data);

        /// <summary>Gets whether the data object contains text.</summary>
        public bool ContainsText () => GetDataPresent (DataFormats.Text.Name);

        /// <summary>Gets the text from the data object.</summary>
        public string GetText () => GetData (DataFormats.Text.Name) as string ?? string.Empty;

        /// <summary>Sets text on the data object.</summary>
        public void SetText (string text) => SetData (DataFormats.Text.Name, text);
    }
}
