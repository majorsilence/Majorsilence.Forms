// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Pins the HelpProvider compat surface legacy designer code depends on: a parameterless
    // constructor, and Set*/Get* overloads that accept a Form (WindowBase) target — in real
    // WinForms a Form IS a Control and designer code calls SetHelpKeyword(Me, ...) on the form.
    public class HelpProviderTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var provider = new HelpProvider ();

            Assert.Equal (string.Empty, provider.HelpNamespace);
            Assert.Null (provider.Tag);
        }

        [Fact]
        public void Ctor_IContainer ()
        {
            using var container = new Container ();
            using var provider = new HelpProvider (container);

            Assert.Same (container, provider.Container);
        }

        [Fact]
        public void Control_Targets_RoundTrip ()
        {
            using var provider = new HelpProvider ();
            var button = new Button ();

            provider.SetHelpString (button, "help text");
            provider.SetHelpKeyword (button, "KEYWORD");
            provider.SetHelpNavigator (button, HelpNavigator.KeywordIndex);
            provider.SetShowHelp (button, true);

            Assert.Equal ("help text", provider.GetHelpString (button));
            Assert.Equal ("KEYWORD", provider.GetHelpKeyword (button));
            Assert.True (provider.GetShowHelp (button));
        }

        [Fact]
        public void Form_Targets_RoundTrip ()
        {
            using var provider = new HelpProvider ();
            using var form = new Form ();

            // The designer-generated call shape: the target is the form itself.
            provider.SetHelpString (form, "Opens the sample dialog");
            provider.SetHelpKeyword (form, "CHAPTER 1                          SAMPLE DIALOG");
            provider.SetHelpNavigator (form, HelpNavigator.KeywordIndex);
            provider.SetShowHelp (form, true);

            Assert.Equal ("Opens the sample dialog", provider.GetHelpString (form));
            Assert.Equal ("CHAPTER 1                          SAMPLE DIALOG", provider.GetHelpKeyword (form));
            Assert.True (provider.GetShowHelp (form));
        }

        [Fact]
        public void Unset_Targets_ReturnEmpty ()
        {
            using var provider = new HelpProvider ();
            var button = new Button ();
            using var form = new Form ();

            Assert.Equal (string.Empty, provider.GetHelpString (button));
            Assert.Equal (string.Empty, provider.GetHelpKeyword (button));
            Assert.Equal (string.Empty, provider.GetHelpString (form));
            Assert.Equal (string.Empty, provider.GetHelpKeyword (form));
        }
    }
}
