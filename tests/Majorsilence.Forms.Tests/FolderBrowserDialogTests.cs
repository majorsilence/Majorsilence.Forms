// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/FolderBrowserDialogTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms FolderBrowserDialogTests, adapted to the
    // Majorsilence.Forms API. Majorsilence.Forms exposes FolderBrowserDialog as a plain FileSystemDialog subclass
    // with no Win32 plumbing, so only the cross-platform behavioral surface that actually exists is
    // pinned here. Upstream members that Majorsilence.Forms does not implement (AddToRecent,
    // AutoUpgradeEnabled, Multiselect, OkRequiresInteraction, SelectedPaths, ShowHiddenFiles,
    // ShowPinnedPlaces, ClientGuid, Tag, Container/Site, HelpRequest) are intentionally omitted.
    // No dialog is ever shown (no ShowDialog / OS interaction).
    public class FolderBrowserDialogTests
    {
        [Fact]
        public void FolderBrowserDialog_Ctor_Default ()
        {
            var dialog = new FolderBrowserDialog ();

            Assert.Equal (string.Empty, dialog.Description);
            Assert.Equal (Environment.SpecialFolder.Desktop, dialog.RootFolder);
            Assert.Null (dialog.InitialDirectory);
            Assert.Equal (string.Empty, dialog.SelectedPath);
            Assert.True (dialog.ShowNewFolderButton);
            Assert.False (dialog.UseDescriptionForTitle);
            Assert.Equal (string.Empty, dialog.Title);
        }

        [Theory]
        [InlineData (null, "")]
        [InlineData ("", "")]
        [InlineData ("A description", "A description")]
        public void FolderBrowserDialog_Description_Set_GetReturnsExpected (string? value, string expected)
        {
            // Majorsilence.Forms coerces null to an empty string; the getter never returns null.
            var dialog = new FolderBrowserDialog { Description = value! };
            Assert.Equal (expected, dialog.Description);

            // Set same.
            dialog.Description = value!;
            Assert.Equal (expected, dialog.Description);
        }

        [Theory]
        [InlineData (null, "")]
        [InlineData ("", "")]
        [InlineData ("selectedPath", "selectedPath")]
        public void FolderBrowserDialog_SelectedPath_Set_GetReturnsExpected (string? value, string expected)
        {
            // Majorsilence.Forms coerces null to an empty string; the getter never returns null.
            var dialog = new FolderBrowserDialog { SelectedPath = value! };
            Assert.Equal (expected, dialog.SelectedPath);

            // Set same.
            dialog.SelectedPath = value!;
            Assert.Equal (expected, dialog.SelectedPath);
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("")]
        [InlineData ("/some/path")]
        public void FolderBrowserDialog_InitialDirectory_Set_GetReturnsExpected (string? value)
        {
            // InitialDirectory lives on the shared FileSystemDialog base and is stored verbatim
            // (no null coercion), matching the FileDialog behavior.
            var dialog = new FolderBrowserDialog { InitialDirectory = value };
            Assert.Equal (value, dialog.InitialDirectory);

            // Set same.
            dialog.InitialDirectory = value;
            Assert.Equal (value, dialog.InitialDirectory);
        }

        [Theory]
        [InlineData (Environment.SpecialFolder.Desktop)]
        [InlineData (Environment.SpecialFolder.StartMenu)]
        [InlineData (Environment.SpecialFolder.CommonAdminTools)]
        public void FolderBrowserDialog_RootFolder_Set_GetReturnsExpected (Environment.SpecialFolder value)
        {
            var dialog = new FolderBrowserDialog { RootFolder = value };
            Assert.Equal (value, dialog.RootFolder);

            // Set same.
            dialog.RootFolder = value;
            Assert.Equal (value, dialog.RootFolder);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FolderBrowserDialog_ShowNewFolderButton_Set_GetReturnsExpected (bool value)
        {
            var dialog = new FolderBrowserDialog { ShowNewFolderButton = value };
            Assert.Equal (value, dialog.ShowNewFolderButton);

            // Set same.
            dialog.ShowNewFolderButton = value;
            Assert.Equal (value, dialog.ShowNewFolderButton);

            // Set different.
            dialog.ShowNewFolderButton = !value;
            Assert.Equal (!value, dialog.ShowNewFolderButton);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FolderBrowserDialog_UseDescriptionForTitle_Set_GetReturnsExpected (bool value)
        {
            var dialog = new FolderBrowserDialog { UseDescriptionForTitle = value };
            Assert.Equal (value, dialog.UseDescriptionForTitle);

            // Set same.
            dialog.UseDescriptionForTitle = value;
            Assert.Equal (value, dialog.UseDescriptionForTitle);

            // Set different.
            dialog.UseDescriptionForTitle = !value;
            Assert.Equal (!value, dialog.UseDescriptionForTitle);
        }

        [Theory]
        [InlineData ("")]
        [InlineData ("Pick a folder")]
        public void FolderBrowserDialog_Title_Set_GetReturnsExpected (string value)
        {
            var dialog = new FolderBrowserDialog { Title = value };
            Assert.Equal (value, dialog.Title);

            // Set same.
            dialog.Title = value;
            Assert.Equal (value, dialog.Title);
        }

        [Fact]
        public void FolderBrowserDialog_Reset_Invoke_Success ()
        {
            var dialog = new FolderBrowserDialog {
                Description = "A description",
                RootFolder = Environment.SpecialFolder.CommonAdminTools,
                InitialDirectory = "/tmp",
                SelectedPath = "/tmp",
                ShowNewFolderButton = false,
                UseDescriptionForTitle = true,
                Title = "Title"
            };

            dialog.Reset ();

            Assert.Equal (string.Empty, dialog.Description);
            Assert.Equal (Environment.SpecialFolder.Desktop, dialog.RootFolder);
            Assert.Null (dialog.InitialDirectory);
            Assert.Equal (string.Empty, dialog.SelectedPath);
            Assert.True (dialog.ShowNewFolderButton);
            Assert.False (dialog.UseDescriptionForTitle);
            Assert.Equal (string.Empty, dialog.Title);
        }
    }
}
