// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    public class TextBoxColorizerTests
    {
        [Fact]
        public void Colorizer_Null_ProducesSingleRun ()
        {
            using var control = new TextBox { Text = "hello world" };

            var block = control.document.GetTextBlock ();

            Assert.Single (block.Lines.SelectMany (l => l.Runs));
        }

        [Fact]
        public void Colorizer_Set_SplitsTextIntoMultipleRuns ()
        {
            using var control = new TextBox { Text = "hello world" };

            // Color just "world" (positions 6-10) red; "hello " stays the default color.
            control.Colorizer = text => new[] { new TextSpanStyle (6, 5, SKColors.Red) };

            var block = control.document.GetTextBlock ();
            var runs = block.Lines.SelectMany (l => l.Runs).ToList ();

            Assert.True (runs.Count >= 2);
        }

        [Fact]
        public void Colorizer_ChangedAfterTextSet_InvalidatesCache ()
        {
            using var control = new TextBox { Text = "abc" };
            var before = control.document.GetTextBlock ();

            control.Colorizer = text => new[] { new TextSpanStyle (0, 1, SKColors.Blue) };
            var after = control.document.GetTextBlock ();

            Assert.NotSame (before, after);
        }

        [Fact]
        public void Colorizer_OutOfRangeSpan_IsIgnoredWithoutThrowing ()
        {
            using var control = new TextBox { Text = "abc" };

            control.Colorizer = _ => new[] { new TextSpanStyle (10, 5, SKColors.Red) };

            var block = control.document.GetTextBlock ();

            Assert.NotNull (block);
        }

        [Fact]
        public void Colorizer_EmptyText_DoesNotThrow ()
        {
            using var control = new TextBox { Text = string.Empty };

            control.Colorizer = _ => System.Array.Empty<TextSpanStyle> ();

            var block = control.document.GetTextBlock ();

            Assert.NotNull (block);
        }
    }
}
