using System;
using System.ComponentModel;
using System.Runtime.Serialization;
using Majorsilence.Forms.Layout;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // LAY-37: a localizable form stores its TableLayoutPanel grid in the .resx as an XML string under
    // "tableLayoutPanel1.LayoutSettings"; InitializeComponent reads it back through
    // TableLayoutSettingsTypeConverter and assigns it to TableLayoutPanel.LayoutSettings. Upstream:
    // Panels/TableLayoutPanel/TableLayoutSettingsTypeConverter.cs, TableLayoutSettings.cs, TableLayoutPanel.cs.
    public class TableLayoutSettingsSerializationTests
    {
        // The shape the WinForms designer writes into a .resx.
        private const string ResxXml =
            "<?xml version=\"1.0\" encoding=\"utf-16\"?><TableLayoutSettings><Controls>" +
            "<Control Name=\"ok\" Row=\"1\" RowSpan=\"1\" Column=\"1\" ColumnSpan=\"1\" />" +
            "<Control Name=\"caption\" Row=\"0\" RowSpan=\"1\" Column=\"0\" ColumnSpan=\"2\" />" +
            "</Controls><Columns Styles=\"Percent,40,Absolute,75\" /><Rows Styles=\"AutoSize,0,Percent,100\" />" +
            "</TableLayoutSettings>";

        [Fact]
        public void The_registered_converter_reads_the_resx_string ()
        {
            var converter = TypeDescriptor.GetConverter (typeof (TableLayoutSettings));

            Assert.IsType<TableLayoutSettingsTypeConverter> (converter);
            Assert.True (converter.CanConvertFrom (typeof (string)));

            var settings = Assert.IsType<TableLayoutSettings> (converter.ConvertFromInvariantString (ResxXml));

            Assert.Equal (2, settings.ColumnStyles.Count);
            Assert.Equal (SizeType.Percent, settings.ColumnStyles[0].SizeType);
            Assert.Equal (40f, settings.ColumnStyles[0].Width);
            Assert.Equal (SizeType.Absolute, settings.ColumnStyles[1].SizeType);
            Assert.Equal (75f, settings.ColumnStyles[1].Width);
            Assert.Equal (2, settings.RowStyles.Count);
            Assert.Equal (SizeType.AutoSize, settings.RowStyles[0].SizeType);
            // A detached settings object answers per control name until it is applied to a panel.
            Assert.Equal (1, settings.GetColumn ("ok"));
            Assert.Equal (2, settings.GetColumnSpan ("caption"));
        }

        [Fact]
        public void Assigning_converted_settings_applies_styles_and_cells_to_named_children ()
        {
            using var tlp = new TableLayoutPanel { ColumnCount = 2, RowCount = 2, Size = new System.Drawing.Size (200, 100) };
            var caption = new Label { Name = "caption" };
            var ok = new Button { Name = "ok" };
            tlp.Controls.Add (caption);
            tlp.Controls.Add (ok);

            var converter = TypeDescriptor.GetConverter (typeof (TableLayoutSettings));
            tlp.LayoutSettings = (TableLayoutSettings)converter.ConvertFromInvariantString (ResxXml)!;

            Assert.Equal (2, tlp.ColumnStyles.Count);
            Assert.Equal (SizeType.Absolute, tlp.ColumnStyles[1].SizeType);
            Assert.Equal (75f, tlp.ColumnStyles[1].Width);
            Assert.Equal (SizeType.Percent, tlp.RowStyles[1].SizeType);
            Assert.Equal (new TableLayoutPanelCellPosition (1, 1), tlp.GetCellPosition (ok));
            Assert.Equal (new TableLayoutPanelCellPosition (0, 0), tlp.GetCellPosition (caption));
            Assert.Equal (2, tlp.GetColumnSpan (caption));

            // The applied grid is the one laid out: the absolute 75px column holds "ok".
            tlp.PerformLayout ();
            Assert.Equal (tlp.GetColumnWidths ()[0], ok.Left - ok.Margin.Left);
            Assert.Equal (75, tlp.GetColumnWidths ()[1]);
        }

        [Fact]
        public void A_panel_grid_round_trips_through_the_converter ()
        {
            using var source = new TableLayoutPanel { ColumnCount = 3, RowCount = 1 };
            source.ColumnStyles.Add (new ColumnStyle (SizeType.Absolute, 30));
            source.ColumnStyles.Add (new ColumnStyle (SizeType.Percent, 100));
            source.ColumnStyles.Add (new ColumnStyle (SizeType.AutoSize));
            source.RowStyles.Add (new RowStyle (SizeType.Percent, 100));
            var a = new Button { Name = "a" };
            var b = new Button { Name = "b" };
            source.Controls.Add (a, 2, 0);
            source.Controls.Add (b, 0, 0);
            source.SetColumnSpan (b, 2);

            var converter = TypeDescriptor.GetConverter (typeof (TableLayoutSettings));
            Assert.True (converter.CanConvertTo (typeof (string)));
            var xml = converter.ConvertToInvariantString (source.LayoutSettings);

            using var target = new TableLayoutPanel { ColumnCount = 3, RowCount = 1 };
            var a2 = new Button { Name = "a" };
            var b2 = new Button { Name = "b" };
            target.Controls.Add (b2);
            target.Controls.Add (a2);
            target.LayoutSettings = (TableLayoutSettings)converter.ConvertFromInvariantString (xml!)!;

            Assert.Equal (3, target.ColumnStyles.Count);
            Assert.Equal (SizeType.Absolute, target.ColumnStyles[0].SizeType);
            Assert.Equal (30f, target.ColumnStyles[0].Width);
            Assert.Equal (SizeType.AutoSize, target.ColumnStyles[2].SizeType);
            Assert.Equal (2, target.GetColumn (a2));
            Assert.Equal (0, target.GetColumn (b2));
            Assert.Equal (2, target.GetColumnSpan (b2));
        }

        [Fact]
        public void Assigning_a_live_settings_object_is_refused ()
        {
            using var one = new TableLayoutPanel ();
            using var two = new TableLayoutPanel ();

            // Upstream accepts only the detached settings the converter produces.
            Assert.Throws<NotSupportedException> (() => one.LayoutSettings = two.LayoutSettings);
        }

        [Fact]
        public void The_settings_serialize_through_ISerializable ()
        {
            using var tlp = new TableLayoutPanel { ColumnCount = 1 };
            tlp.ColumnStyles.Add (new ColumnStyle (SizeType.Absolute, 42));

            ISerializable serializable = tlp.LayoutSettings;
#pragma warning disable SYSLIB0050 // the legacy serialization contract is exactly what a .resx reader uses
            var info = new SerializationInfo (typeof (TableLayoutSettings), new FormatterConverter ());
            serializable.GetObjectData (info, default);
#pragma warning restore SYSLIB0050

            var text = info.GetString ("SerializedString");
            Assert.Contains ("Absolute,42", text);

            // The deserialization constructor upstream's resx reader calls rebuilds a detached copy.
            var ctor = typeof (TableLayoutSettings).GetConstructor (
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, [typeof (SerializationInfo), typeof (StreamingContext)], null);
            Assert.NotNull (ctor);
            var copy = (TableLayoutSettings)ctor!.Invoke ([info, default (StreamingContext)]);
            Assert.Single (copy.ColumnStyles);
            Assert.Equal (42f, copy.ColumnStyles[0].Width);
        }
    }
}
