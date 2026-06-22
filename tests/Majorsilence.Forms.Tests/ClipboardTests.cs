// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests (ClipboardTests.cs under
// src/test/unit/System.Windows.Forms/), rewritten for the Majorsilence.Forms API.
// Original work Copyright (c) .NET Foundation and Contributors.

using Xunit;

namespace Majorsilence.Forms.Tests
{
    public class ClipboardTests
    {
        [Fact]
        public void SetText_GetText_RoundTrips ()
        {
            Clipboard.SetText ("clipboard-value");
            Assert.Equal ("clipboard-value", Clipboard.GetText ());
            Assert.True (Clipboard.ContainsText ());
        }

        [Fact]
        public void Clear_EmptiesClipboard ()
        {
            Clipboard.SetText ("to-be-cleared");
            Clipboard.Clear ();

            Assert.Equal (string.Empty, Clipboard.GetText ());
            Assert.False (Clipboard.ContainsText ());
        }

        [Fact]
        public void GetText_WithFormat_DelegatesToText ()
        {
            Clipboard.SetText ("formatted");
            Assert.Equal ("formatted", Clipboard.GetText (TextDataFormat.UnicodeText));
            Assert.True (Clipboard.ContainsText (TextDataFormat.Text));
        }

        [Fact]
        public void SetData_GetData_Text_RoundTrips ()
        {
            Clipboard.SetData (DataFormats.Text.Name, "data-value");

            Assert.Equal ("data-value", Clipboard.GetData (DataFormats.Text.Name));
            Assert.True (Clipboard.ContainsData (DataFormats.Text.Name));
        }

        [Fact]
        public void GetData_UnknownFormat_ReturnsNull ()
        {
            Clipboard.SetText ("anything");
            Assert.Null (Clipboard.GetData ("application/x-not-a-format"));
            Assert.False (Clipboard.ContainsData ("application/x-not-a-format"));
        }

        [Fact]
        public void SetDataObject_GetDataObject_RoundTripsText ()
        {
            Clipboard.SetText ("via-data-object");
            var data = Clipboard.GetDataObject ();

            Assert.NotNull (data);
            Assert.True (data.GetDataPresent (DataFormats.Text.Name));
            Assert.Equal ("via-data-object", data.GetData (DataFormats.Text.Name));
        }

        [Fact]
        public void ContainsImage_IsFalse_AndGetImage_IsNull ()
        {
            // Image clipboard is not supported in the cross-platform backend.
            Assert.False (Clipboard.ContainsImage ());
            Assert.Null (Clipboard.GetImage ());
        }
    }
}
