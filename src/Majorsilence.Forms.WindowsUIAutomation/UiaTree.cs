using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms.Automation;

namespace Majorsilence.Forms.WindowsUIAutomation
{
    /// <summary>
    /// Pure tree navigation over an <see cref="AutomationElement"/> snapshot, keyed by child-index paths
    /// (an empty path is the root). Free of any Windows / UI Automation dependency so it can be unit-tested
    /// directly; <see cref="UiaBridge"/> layers the COM provider plumbing on top.
    /// </summary>
    internal static class UiaTree
    {
        /// <summary>Resolves the element at <paramref name="path"/>, or null if the path no longer exists.</summary>
        public static AutomationElement? Follow (AutomationElement root, int[] path)
        {
            var node = root;
            foreach (var i in path) {
                if (i < 0 || i >= node.Children.Count)
                    return null;
                node = node.Children[i];
            }
            return node;
        }

        public static int[]? Parent (int[] path) => path.Length == 0 ? null : path[..^1];

        public static int[]? FirstChild (AutomationElement root, int[] path)
        {
            var el = Follow (root, path);
            return el is { Children.Count: > 0 } ? Append (path, 0) : null;
        }

        public static int[]? LastChild (AutomationElement root, int[] path)
        {
            var el = Follow (root, path);
            return el is { Children.Count: > 0 } ? Append (path, el.Children.Count - 1) : null;
        }

        public static int[]? NextSibling (AutomationElement root, int[] path)
        {
            if (path.Length == 0)
                return null;
            var parent = Follow (root, path[..^1]);
            var idx = path[^1] + 1;
            return parent != null && idx < parent.Children.Count ? Replace (path, idx) : null;
        }

        public static int[]? PreviousSibling (AutomationElement root, int[] path)
        {
            if (path.Length == 0)
                return null;
            var parent = Follow (root, path[..^1]);
            var idx = path[^1] - 1;
            return parent != null && idx >= 0 ? Replace (path, idx) : null;
        }

        /// <summary>Returns the path of the deepest element whose bounds contain <paramref name="clientPt"/>
        /// (topmost in z-order), or an empty path (the root) when nothing deeper is hit.</summary>
        public static int[] HitTest (AutomationElement root, Point clientPt)
        {
            var acc = new List<int> ();
            Descend (root, clientPt, acc);
            return acc.ToArray ();
        }

        private static void Descend (AutomationElement node, Point pt, List<int> path)
        {
            // Last child wins ties — later siblings are drawn on top.
            for (var i = node.Children.Count - 1; i >= 0; i--) {
                var c = node.Children[i];
                if (c.Bounds.Contains (pt)) {
                    path.Add (i);
                    Descend (c, pt, path);
                    return;
                }
            }
        }

        /// <summary>Returns the path of the first focused element, or null if none is focused.</summary>
        public static int[]? FindFocused (AutomationElement root)
        {
            var acc = new List<int> ();
            return Find (root, acc) ? acc.ToArray () : null;
        }

        private static bool Find (AutomationElement node, List<int> path)
        {
            for (var i = 0; i < node.Children.Count; i++) {
                var c = node.Children[i];
                path.Add (i);
                if (c.Focused || Find (c, path))
                    return true;
                path.RemoveAt (path.Count - 1);
            }
            return false;
        }

        private static int[] Append (int[] path, int index)
        {
            var next = new int[path.Length + 1];
            System.Array.Copy (path, next, path.Length);
            next[^1] = index;
            return next;
        }

        private static int[] Replace (int[] path, int lastIndex)
        {
            var next = (int[]) path.Clone ();
            next[^1] = lastIndex;
            return next;
        }
    }
}
