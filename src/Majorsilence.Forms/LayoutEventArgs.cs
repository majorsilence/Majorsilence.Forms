// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// Copyright (c) 2004 Novell, Inc.
//
// Authors:
//	Peter Bartok	pbartok@novell.com
//
//

using System.ComponentModel;

namespace Majorsilence.Forms
{
    /// <summary>
    ///  Provides data for the Layout event.
    /// </summary>
    /// <remarks>
    ///  One affected component, as upstream holds it (Layout/LayoutEventArgs.cs): the <see cref="Control"/>
    ///  constructor chains to the <see cref="IComponent"/> one and <see cref="AffectedControl"/> is that
    ///  component as a control. These were two fields, each written by one constructor, so every layout a
    ///  control raised reported a null <see cref="AffectedComponent"/> and one raised for a control through
    ///  the component overload a null <see cref="AffectedControl"/> (LAY-36). The reference is weak, as
    ///  upstream's is, so a cached instance does not keep a disposed control alive.
    /// </remarks>
    public sealed class LayoutEventArgs : EventArgs
    {
        // The component, held strongly while these args are only passed to a layout handler, and through a weak
        // reference once a control caches them for a later layout (Control.PerformLayout while layout is suspended):
        // upstream holds it weakly because cached args would otherwise keep a removed child alive. Every layout of
        // every control makes one of these, and a WeakReference is a GC handle with a finaliser; with a thousand
        // anchored children that was a fifth of what a layout pass allocated (LayoutBenchmarks).
        private IComponent? affected_strong;
        private WeakReference<IComponent>? affected_component;

        /// <summary>
        ///  Initializes a new instance of the LayoutEventArgs class.
        /// </summary>
        public LayoutEventArgs (Control? affectedControl, string? affectedProperty)
            : this ((IComponent?)affectedControl, affectedProperty)
        {
        }

        /// <summary>
        ///  Initializes a new instance of the LayoutEventArgs class.
        /// </summary>
        public LayoutEventArgs (IComponent? affectedComponent, string? affectedProperty)
        {
            affected_strong = affectedComponent;
            AffectedProperty = affectedProperty;
        }

        // Called before the args are kept beyond the layout they were made for.
        internal void HoldWeakly ()
        {
            if (affected_strong is { } component) {
                affected_component = new WeakReference<IComponent> (component);
                affected_strong = null;
            }
        }

        /// <summary>
        /// Gets the component affected by this layout event.
        /// </summary>
        public IComponent? AffectedComponent {
            get {
                if (affected_strong is { } component)
                    return component;

                IComponent? target = null;
                affected_component?.TryGetTarget (out target);
                return target;
            }
        }

        /// <summary>
        /// Gets the control affected by this layout event.
        /// </summary>
        public Control? AffectedControl => AffectedComponent as Control;

        /// <summary>
        /// Gets the property affected by this layout event.
        /// </summary>
        public string? AffectedProperty { get; }
    }
}
