using System.Collections;
using System.Collections.Generic;

namespace Majorsilence.Forms
{
    /// <summary>
    /// The style classes of a control (<see cref="Control.StyleClasses"/>): the names a theme's <c>.class</c> selectors match, as
    /// in <c>Button.primary { ... }</c>. A small set -- a control has a handful at most -- in the order they were added.
    /// Changing it restyles the control, with a CSS <c>transition</c> if the new style has one.
    /// </summary>
    public sealed class ControlStyleClasses : IEnumerable<string>
    {
        private static readonly char[] separators = { ' ', '\t', '\r', '\n' };

        private readonly Control owner;
        private readonly List<string> items = new (2);

        internal ControlStyleClasses (Control owner) => this.owner = owner;

        /// <summary>The number of classes.</summary>
        public int Count => items.Count;

        /// <summary>Whether the control has the class <paramref name="name"/> (class names are case-sensitive, as in CSS).</summary>
        public bool Contains (string name) => name is not null && items.Contains (name);

        /// <summary>Adds a class; false when the control already had it.</summary>
        /// <exception cref="ArgumentException"><paramref name="name"/> is not a class name a selector can write: a word of letters, digits, '-' and '_' that does not start with a digit.</exception>
        public bool Add (string name)
        {
            Validate (name);

            if (items.Contains (name))
                return false;

            items.Add (name);
            owner.StyleClassesChanged ();
            return true;
        }

        /// <summary>Removes a class; false when the control did not have it.</summary>
        public bool Remove (string name)
        {
            if (name is null || !items.Remove (name))
                return false;

            owner.StyleClassesChanged ();
            return true;
        }

        /// <summary>Adds the class if the control lacks it and removes it if it has it. Returns whether the control has it afterwards.</summary>
        public bool Toggle (string name)
        {
            Toggle (name, !Contains (name));
            return Contains (name);
        }

        /// <summary>Adds the class when <paramref name="present"/> is true and removes it when false -- <c>classList.toggle (name, force)</c>.</summary>
        public void Toggle (string name, bool present)
        {
            if (present)
                Add (name);
            else
                Remove (name);
        }

        /// <summary>Removes every class.</summary>
        public void Clear ()
        {
            if (items.Count == 0)
                return;

            items.Clear ();
            owner.StyleClassesChanged ();
        }

        // Replaces the classes with the space-separated names in `value` (Control.CssClass), restyling once.
        internal void Replace (string? value)
        {
            var names = (value ?? string.Empty).Split (separators, StringSplitOptions.RemoveEmptyEntries);

            foreach (var name in names)
                Validate (name);

            var next = new List<string> (names.Length);
            foreach (var name in names)
                if (!next.Contains (name))
                    next.Add (name);

            if (next.Count == items.Count && next.TrueForAll (items.Contains))
                return;

            items.Clear ();
            items.AddRange (next);
            owner.StyleClassesChanged ();
        }

        // A name a selector can write after '.': the tokenizer's identifier. One that could not be written would never match, which is
        // a silent no-op, so it is refused here.
        private static void Validate (string name)
        {
            Guard.ThrowIfNull (name);

            var valid = name.Length > 0 && (char.IsLetter (name[0]) || name[0] == '_' || name[0] == '-');

            for (var i = 1; valid && i < name.Length; i++)
                valid = char.IsLetterOrDigit (name[i]) || name[i] == '-' || name[i] == '_';

            if (!valid)
                throw new ArgumentException ($"'{name}' is not a style class name. A class is one word of letters, digits, '-' and '_' that does not start with a digit, such as 'primary' or 'btn-large'; separate several with spaces in CssClass.", nameof (name));
        }

        /// <summary>The classes separated by spaces -- what <see cref="Control.CssClass"/> returns.</summary>
        public override string ToString () => string.Join (" ", items);

        /// <inheritdoc/>
        public IEnumerator<string> GetEnumerator () => items.GetEnumerator ();

        IEnumerator IEnumerable.GetEnumerator () => GetEnumerator ();
    }
}
