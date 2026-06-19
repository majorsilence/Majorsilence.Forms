// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests (OpenFileDialogTests.cs / SaveFileDialogTests.cs under
// src/test/unit/System.Windows.Forms/System/Windows/Forms/), rewritten for the Modern.Forms API.
// Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.IO;
using Xunit;

namespace Modern.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms FileDialog/OpenFileDialog/SaveFileDialog
    // tests, adapted to the Modern.Forms API. Modern.Forms exposes the dialogs as a plain object
    // hierarchy (FileSystemDialog -> FileDialog -> OpenFileDialog/SaveFileDialog) with no Win32
    // Options/Instance/Events plumbing, so only the cross-platform behavioral surface is pinned here.
    // No dialog is ever shown (no ShowDialog / OS interaction).
    public class FileDialogTests
    {
        [Fact]
        public void OpenFileDialog_Ctor_Default ()
        {
            var dialog = new OpenFileDialog ();

            Assert.True (dialog.AddExtension);
            Assert.True (dialog.CheckFileExists);
            Assert.True (dialog.CheckPathExists);
            Assert.True (dialog.DereferenceLinks);
            Assert.Equal (string.Empty, dialog.DefaultExt);
            Assert.Null (dialog.FileName);
            Assert.Empty (dialog.FileNames);
            Assert.Equal (1, dialog.FilterIndex);
            Assert.Equal (string.Empty, dialog.Filter);
            Assert.Null (dialog.InitialDirectory);
            Assert.False (dialog.Multiselect);
            Assert.False (dialog.AllowMultiple);
            Assert.False (dialog.ReadOnlyChecked);
            Assert.False (dialog.ShowReadOnly);
            Assert.False (dialog.ShowHelp);
            Assert.False (dialog.RestoreDirectory);
            Assert.False (dialog.SupportMultiDottedExtensions);
            Assert.True (dialog.ValidateNames);
            Assert.Equal (string.Empty, dialog.Title);
        }

        [Fact]
        public void SaveFileDialog_Ctor_Default ()
        {
            var dialog = new SaveFileDialog ();

            Assert.True (dialog.AddExtension);
            Assert.True (dialog.CheckFileExists);
            Assert.True (dialog.CheckPathExists);
            Assert.False (dialog.CreatePrompt);
            Assert.True (dialog.OverwritePrompt);
            Assert.True (dialog.DereferenceLinks);
            Assert.Equal (string.Empty, dialog.DefaultExt);
            Assert.Null (dialog.DefaultExtension);
            Assert.Null (dialog.FileName);
            Assert.Empty (dialog.FileNames);
            Assert.Equal (1, dialog.FilterIndex);
            Assert.Equal (string.Empty, dialog.Filter);
            Assert.Null (dialog.InitialDirectory);
            Assert.False (dialog.RestoreDirectory);
            Assert.True (dialog.ValidateNames);
            Assert.Equal (string.Empty, dialog.Title);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void OpenFileDialog_CheckFileExists_Set_GetReturnsExpected (bool value)
        {
            var dialog = new OpenFileDialog { CheckFileExists = value };
            Assert.Equal (value, dialog.CheckFileExists);

            // Set same.
            dialog.CheckFileExists = value;
            Assert.Equal (value, dialog.CheckFileExists);

            // Set different.
            dialog.CheckFileExists = !value;
            Assert.Equal (!value, dialog.CheckFileExists);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void OpenFileDialog_Multiselect_Set_GetReturnsExpected (bool value)
        {
            var dialog = new OpenFileDialog { Multiselect = value };
            Assert.Equal (value, dialog.Multiselect);

            // Multiselect is a WinForms-compatible alias for AllowMultiple.
            Assert.Equal (value, dialog.AllowMultiple);

            // Set same.
            dialog.Multiselect = value;
            Assert.Equal (value, dialog.Multiselect);

            // Set different.
            dialog.Multiselect = !value;
            Assert.Equal (!value, dialog.Multiselect);
            Assert.Equal (!value, dialog.AllowMultiple);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void OpenFileDialog_AllowMultiple_Set_GetReturnsExpected (bool value)
        {
            var dialog = new OpenFileDialog { AllowMultiple = value };
            Assert.Equal (value, dialog.AllowMultiple);
            Assert.Equal (value, dialog.Multiselect);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void OpenFileDialog_ShowReadOnly_Set_GetReturnsExpected (bool value)
        {
            var dialog = new OpenFileDialog { ShowReadOnly = value };
            Assert.Equal (value, dialog.ShowReadOnly);

            dialog.ShowReadOnly = !value;
            Assert.Equal (!value, dialog.ShowReadOnly);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void OpenFileDialog_ReadOnlyChecked_Set_GetReturnsExpected (bool value)
        {
            var dialog = new OpenFileDialog { ReadOnlyChecked = value };
            Assert.Equal (value, dialog.ReadOnlyChecked);

            dialog.ReadOnlyChecked = !value;
            Assert.Equal (!value, dialog.ReadOnlyChecked);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void SaveFileDialog_CreatePrompt_Set_GetReturnsExpected (bool value)
        {
            var dialog = new SaveFileDialog { CreatePrompt = value };
            Assert.Equal (value, dialog.CreatePrompt);

            // Set same.
            dialog.CreatePrompt = value;
            Assert.Equal (value, dialog.CreatePrompt);

            // Set different.
            dialog.CreatePrompt = !value;
            Assert.Equal (!value, dialog.CreatePrompt);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void SaveFileDialog_OverwritePrompt_Set_GetReturnsExpected (bool value)
        {
            var dialog = new SaveFileDialog { OverwritePrompt = value };
            Assert.Equal (value, dialog.OverwritePrompt);

            // Set same.
            dialog.OverwritePrompt = value;
            Assert.Equal (value, dialog.OverwritePrompt);

            // Set different.
            dialog.OverwritePrompt = !value;
            Assert.Equal (!value, dialog.OverwritePrompt);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FileDialog_AddExtension_Set_GetReturnsExpected (bool value)
        {
            var dialog = new OpenFileDialog { AddExtension = value };
            Assert.Equal (value, dialog.AddExtension);

            dialog.AddExtension = !value;
            Assert.Equal (!value, dialog.AddExtension);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FileDialog_CheckPathExists_Set_GetReturnsExpected (bool value)
        {
            var dialog = new OpenFileDialog { CheckPathExists = value };
            Assert.Equal (value, dialog.CheckPathExists);

            dialog.CheckPathExists = !value;
            Assert.Equal (!value, dialog.CheckPathExists);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FileDialog_DereferenceLinks_Set_GetReturnsExpected (bool value)
        {
            var dialog = new OpenFileDialog { DereferenceLinks = value };
            Assert.Equal (value, dialog.DereferenceLinks);

            dialog.DereferenceLinks = !value;
            Assert.Equal (!value, dialog.DereferenceLinks);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FileDialog_RestoreDirectory_Set_GetReturnsExpected (bool value)
        {
            var dialog = new OpenFileDialog { RestoreDirectory = value };
            Assert.Equal (value, dialog.RestoreDirectory);

            dialog.RestoreDirectory = !value;
            Assert.Equal (!value, dialog.RestoreDirectory);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FileDialog_SupportMultiDottedExtensions_Set_GetReturnsExpected (bool value)
        {
            var dialog = new OpenFileDialog { SupportMultiDottedExtensions = value };
            Assert.Equal (value, dialog.SupportMultiDottedExtensions);

            dialog.SupportMultiDottedExtensions = !value;
            Assert.Equal (!value, dialog.SupportMultiDottedExtensions);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FileDialog_ValidateNames_Set_GetReturnsExpected (bool value)
        {
            var dialog = new OpenFileDialog { ValidateNames = value };
            Assert.Equal (value, dialog.ValidateNames);

            dialog.ValidateNames = !value;
            Assert.Equal (!value, dialog.ValidateNames);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FileDialog_ShowHelp_Set_GetReturnsExpected (bool value)
        {
            var dialog = new OpenFileDialog { ShowHelp = value };
            Assert.Equal (value, dialog.ShowHelp);

            dialog.ShowHelp = !value;
            Assert.Equal (!value, dialog.ShowHelp);
        }

        [Theory]
        [InlineData ("")]
        [InlineData ("txt")]
        [InlineData (".txt")]
        public void FileDialog_DefaultExt_Set_GetReturnsExpected (string value)
        {
            // Modern.Forms stores DefaultExt verbatim (no leading-dot normalization).
            var dialog = new OpenFileDialog { DefaultExt = value };
            Assert.Equal (value, dialog.DefaultExt);

            // Set same.
            dialog.DefaultExt = value;
            Assert.Equal (value, dialog.DefaultExt);
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("txt")]
        public void SaveFileDialog_DefaultExtension_Set_GetReturnsExpected (string? value)
        {
            var dialog = new SaveFileDialog { DefaultExtension = value };
            Assert.Equal (value, dialog.DefaultExtension);
        }

        [Fact]
        public void FileDialog_FileName_SetNull_ClearsFileNames ()
        {
            var dialog = new OpenFileDialog { FileName = "file.txt" };
            Assert.NotEmpty (dialog.FileNames);

            dialog.FileName = null;
            Assert.Null (dialog.FileName);
            Assert.Empty (dialog.FileNames);
        }

        [Fact]
        public void FileDialog_FileName_Set_GetReturnsFullPath ()
        {
            // Modern.Forms resolves FileName through Path.GetFullPath, so the getter returns
            // a rooted path that ends with the supplied name.
            var dialog = new OpenFileDialog { FileName = "file.txt" };

            Assert.NotNull (dialog.FileName);
            Assert.True (Path.IsPathRooted (dialog.FileName));
            Assert.Equal ("file.txt", Path.GetFileName (dialog.FileName));
            Assert.Single (dialog.FileNames);
        }

        [Fact]
        public void FileDialog_FileName_SetReplacesPreviousSelection ()
        {
            var dialog = new OpenFileDialog ();
            dialog.FileName = "first.txt";
            dialog.FileName = "second.txt";

            Assert.Single (dialog.FileNames);
            Assert.Equal ("second.txt", Path.GetFileName (dialog.FileName));
        }

        [Theory]
        [InlineData (null, "")]
        [InlineData ("", "")]
        [InlineData ("Text Files|*.txt", "Text Files|*.txt")]
        [InlineData ("Text Files|*.txt|All Files|*.*", "Text Files|*.txt|All Files|*.*")]
        public void FileDialog_Filter_Set_GetReturnsExpected (string? value, string expected)
        {
            var dialog = new OpenFileDialog { Filter = value! };
            Assert.Equal (expected, dialog.Filter);

            // Set same.
            dialog.Filter = value!;
            Assert.Equal (expected, dialog.Filter);
        }

        [Theory]
        [InlineData ("filter")]
        [InlineData ("a|b|c")]
        public void FileDialog_Filter_SetInvalid_ThrowsArgumentException (string value)
        {
            var dialog = new OpenFileDialog ();
            Assert.Throws<ArgumentException> (() => dialog.Filter = value);
        }

        [Fact]
        public void FileDialog_Filter_SetInvalid_DoesNotChangeValue ()
        {
            var dialog = new OpenFileDialog { Filter = "Text Files|*.txt" };
            Assert.Throws<ArgumentException> (() => dialog.Filter = "bad");
            Assert.Equal ("Text Files|*.txt", dialog.Filter);
        }

        [Fact]
        public void FileDialog_Filter_PopulatesFilterList ()
        {
            var dialog = new OpenFileDialog { Filter = "Text Files|*.txt|All Files|*.*" };
            Assert.Equal (2, dialog.filters.Count);
        }

        [Theory]
        [InlineData (-1)]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (2)]
        [InlineData (int.MaxValue)]
        public void FileDialog_FilterIndex_Set_GetReturnsExpected (int value)
        {
            var dialog = new OpenFileDialog { FilterIndex = value };
            Assert.Equal (value, dialog.FilterIndex);

            // Set same.
            dialog.FilterIndex = value;
            Assert.Equal (value, dialog.FilterIndex);
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("")]
        [InlineData ("/some/path")]
        public void FileDialog_InitialDirectory_Set_GetReturnsExpected (string? value)
        {
            var dialog = new OpenFileDialog { InitialDirectory = value };
            Assert.Equal (value, dialog.InitialDirectory);

            dialog.InitialDirectory = value;
            Assert.Equal (value, dialog.InitialDirectory);
        }

        [Theory]
        [InlineData ("")]
        [InlineData ("Open a file")]
        public void FileDialog_Title_Set_GetReturnsExpected (string value)
        {
            var dialog = new OpenFileDialog { Title = value };
            Assert.Equal (value, dialog.Title);

            dialog.Title = value;
            Assert.Equal (value, dialog.Title);
        }

        [Fact]
        public void FileDialog_AddFilter_AddsToFilterList ()
        {
            var dialog = new OpenFileDialog ();
            dialog.AddFilter ("Text Files", "*.txt", "*.log");
            Assert.Single (dialog.filters);
        }

        [Fact]
        public void OpenFileDialog_Reset_RestoresDefaults ()
        {
            var dialog = new OpenFileDialog {
                CheckFileExists = false,
                CheckPathExists = false,
                Multiselect = true,
                ReadOnlyChecked = true,
                ShowReadOnly = true,
                ShowHelp = true,
                RestoreDirectory = true,
                SupportMultiDottedExtensions = true,
                ValidateNames = false,
                AddExtension = false,
                DereferenceLinks = false,
                DefaultExt = "txt",
                Filter = "Text Files|*.txt",
                FilterIndex = 2,
                FileName = "file.txt",
                InitialDirectory = "/tmp",
                Title = "Title"
            };

            dialog.Reset ();

            Assert.True (dialog.CheckFileExists);
            Assert.True (dialog.CheckPathExists);
            Assert.False (dialog.Multiselect);
            Assert.False (dialog.ReadOnlyChecked);
            Assert.False (dialog.ShowReadOnly);
            Assert.False (dialog.ShowHelp);
            Assert.False (dialog.RestoreDirectory);
            Assert.False (dialog.SupportMultiDottedExtensions);
            Assert.True (dialog.ValidateNames);
            Assert.True (dialog.AddExtension);
            Assert.True (dialog.DereferenceLinks);
            Assert.Equal (string.Empty, dialog.DefaultExt);
            Assert.Equal (string.Empty, dialog.Filter);
            Assert.Empty (dialog.filters);
            Assert.Equal (1, dialog.FilterIndex);
            Assert.Null (dialog.FileName);
            Assert.Empty (dialog.FileNames);
            Assert.Null (dialog.InitialDirectory);
            Assert.Equal (string.Empty, dialog.Title);
        }

        [Fact]
        public void SaveFileDialog_Reset_RestoresDefaults ()
        {
            var dialog = new SaveFileDialog {
                CheckFileExists = false,
                CreatePrompt = true,
                OverwritePrompt = false,
                DefaultExtension = "txt",
                DefaultExt = "txt",
                FilterIndex = 2,
                FileName = "file.txt",
                Title = "Title"
            };

            dialog.Reset ();

            Assert.True (dialog.CheckFileExists);
            Assert.False (dialog.CreatePrompt);
            Assert.True (dialog.OverwritePrompt);
            Assert.Null (dialog.DefaultExtension);
            Assert.Equal (string.Empty, dialog.DefaultExt);
            Assert.Equal (1, dialog.FilterIndex);
            Assert.Null (dialog.FileName);
            Assert.Empty (dialog.FileNames);
            Assert.Equal (string.Empty, dialog.Title);
        }
    }
}
