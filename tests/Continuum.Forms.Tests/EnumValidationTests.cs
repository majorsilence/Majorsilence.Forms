// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Pins the WinForms-compatible behavior that setting an undefined enum value on a control
// property throws InvalidEnumArgumentException. These exercise the shared
// Continuum.Forms.Layout.SourceGenerated.EnumValidator used by many control property setters.

using System.ComponentModel;
using Xunit;

namespace Continuum.Forms.Tests
{
    public class EnumValidationTests
    {
        [Fact]
        public void Label_TextAlign_SetInvalid_Throws ()
        {
            using var control = new Label ();
            Assert.Throws<InvalidEnumArgumentException> (() => control.TextAlign = (ContentAlignment) 0);
        }

        [Fact]
        public void Label_TextAlign_SetValid_DoesNotThrow ()
        {
            using var control = new Label { TextAlign = ContentAlignment.MiddleCenter };
            Assert.Equal (ContentAlignment.MiddleCenter, control.TextAlign);
        }

        [Fact]
        public void Label_TextImageRelation_SetInvalid_Throws ()
        {
            using var control = new Label ();
            Assert.Throws<InvalidEnumArgumentException> (() => control.TextImageRelation = (TextImageRelation) 99);
        }

        [Fact]
        public void CheckBox_ImageAlign_SetInvalid_Throws ()
        {
            using var control = new CheckBox ();
            Assert.Throws<InvalidEnumArgumentException> (() => control.ImageAlign = (ContentAlignment) 0);
        }

        [Fact]
        public void CheckBox_AutoSizeMode_SetInvalid_Throws ()
        {
            using var control = new CheckBox ();
            Assert.Throws<InvalidEnumArgumentException> (() => control.AutoSizeMode = (AutoSizeMode) 5);
        }

        [Fact]
        public void Button_TextAlign_SetInvalid_Throws ()
        {
            using var control = new Button ();
            Assert.Throws<InvalidEnumArgumentException> (() => control.TextAlign = (ContentAlignment) 12345);
        }

        [Fact]
        public void Button_TextAlign_SetValid_DoesNotThrow ()
        {
            using var control = new Button { TextAlign = ContentAlignment.TopRight };
            Assert.Equal (ContentAlignment.TopRight, control.TextAlign);
        }

        [Fact]
        public void TableLayoutPanel_GrowStyle_SetInvalid_Throws ()
        {
            using var control = new TableLayoutPanel ();
            Assert.Throws<InvalidEnumArgumentException> (() => control.GrowStyle = (TableLayoutPanelGrowStyle) 99);
        }

        [Fact]
        public void TableLayoutPanel_GrowStyle_SetValid_DoesNotThrow ()
        {
            using var control = new TableLayoutPanel { GrowStyle = TableLayoutPanelGrowStyle.FixedSize };
            Assert.Equal (TableLayoutPanelGrowStyle.FixedSize, control.GrowStyle);
        }
    }
}
