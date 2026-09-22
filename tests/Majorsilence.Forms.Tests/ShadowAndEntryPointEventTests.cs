using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// W6.1: events that were declared and raised by nothing, in two shapes. Shadows -- a subclass redeclaring
/// an event the base already raises, so a subscription on the subclass hears nothing (LST-28's shape,
/// on PrintPreviewDialog and Form) -- and stored properties whose <c>*Changed</c> event was never
/// wired. Each test subscribes at the public surface a caller would use and mutates the public
/// property, so it fails if the forward or the raise is removed.
/// </summary>
public class ShadowAndEntryPointEventTests
{
    private static int Count (Action<EventHandler> subscribe, Action mutate)
    {
        var fired = 0;
        subscribe ((_, _) => fired++);
        mutate ();
        mutate (); // the same value again: a notifying setter raises once, not per assignment
        return fired;
    }

    // ── PrintPreviewDialog: nine `new` redeclarations forward to the base, one seam raises ─────────

    [Fact]
    public void Dialog_BackColorChanged_reaches_a_subscriber_on_the_dialog ()
    {
        using var dialog = new PrintPreviewDialog ();
        Assert.Equal (1, Count (h => dialog.BackColorChanged += h, () => dialog.BackColor = Color.Red));
    }

    [Fact]
    public void Dialog_ForeColorChanged_reaches_a_subscriber_on_the_dialog ()
    {
        using var dialog = new PrintPreviewDialog ();
        Assert.Equal (1, Count (h => dialog.ForeColorChanged += h, () => dialog.ForeColor = Color.Red));
    }

    [Fact]
    public void Dialog_CursorChanged_reaches_a_subscriber_on_the_dialog ()
    {
        using var dialog = new PrintPreviewDialog ();
        Assert.Equal (1, Count (h => dialog.CursorChanged += h, () => dialog.Cursor = Cursors.Hand));
    }

    [Fact]
    public void Dialog_PaddingChanged_reaches_a_subscriber_on_the_dialog ()
    {
        using var dialog = new PrintPreviewDialog ();
        Assert.Equal (1, Count (h => dialog.PaddingChanged += h, () => dialog.Padding = new Padding (7)));
    }

    [Fact]
    public void Dialog_RightToLeftChanged_reaches_a_subscriber_on_the_dialog ()
    {
        using var dialog = new PrintPreviewDialog ();
        Assert.Equal (1, Count (h => dialog.RightToLeftChanged += h, () => dialog.RightToLeft = RightToLeft.Yes));
    }

    [Fact]
    public void Dialog_CausesValidationChanged_reaches_a_subscriber_on_the_dialog ()
    {
        using var dialog = new PrintPreviewDialog ();
        Assert.True (dialog.CausesValidation);
        Assert.Equal (1, Count (h => dialog.CausesValidationChanged += h, () => dialog.CausesValidation = false));
    }

    [Fact]
    public void Dialog_BackgroundImageChanged_reaches_a_subscriber_on_the_dialog ()
    {
        using var dialog = new PrintPreviewDialog ();
        using var image = new Majorsilence.Forms.Drawing.Bitmap (2, 2);
        Assert.Equal (1, Count (h => dialog.BackgroundImageChanged += h, () => dialog.BackgroundImage = image));
    }

    [Fact]
    public void Dialog_BackgroundImageLayoutChanged_reaches_a_subscriber_on_the_dialog ()
    {
        using var dialog = new PrintPreviewDialog ();
        Assert.Equal (1, Count (h => dialog.BackgroundImageLayoutChanged += h, () => dialog.BackgroundImageLayout = ImageLayout.Stretch));
    }

    [Fact]
    public void Dialog_TextChanged_reaches_a_subscriber_on_the_dialog ()
    {
        using var dialog = new PrintPreviewDialog ();
        Assert.Equal (1, Count (h => dialog.TextChanged += h, () => dialog.Text = "Preview"));
    }

    [Fact]
    public void Dialog_DockChanged_fires_when_Dock_changes ()
    {
        using var dialog = new PrintPreviewDialog ();
        Assert.Equal (1, Count (h => dialog.DockChanged += h, () => dialog.Dock = DockStyle.Fill));
    }

    // ── Form itself: four events whose property never reached them ──────────────────────────────────
    // Found under the dialog forwards above: WindowBase's BackColor/ForeColor/Cursor write the window's
    // own style and cursor while the events forwarded to the root adapter, and Form.Text never called
    // OnTextChanged. A forward to a base that does not fire is the same silence one level up.

    [Fact]
    public void Form_BackColorChanged_fires_when_the_forms_BackColor_changes ()
    {
        using var form = new Form ();
        Assert.Equal (1, Count (h => form.BackColorChanged += h, () => form.BackColor = Color.Red));
    }

    [Fact]
    public void Form_BackColorChanged_still_relays_the_content_roots_change ()
    {
        using var form = new Form ();
        Assert.Equal (1, Count (h => form.BackColorChanged += h, () => form.Controls.Owner.BackColor = Color.Firebrick));
    }

    [Fact]
    public void Form_ForeColorChanged_fires_when_the_forms_ForeColor_changes ()
    {
        using var form = new Form ();
        Assert.Equal (1, Count (h => form.ForeColorChanged += h, () => form.ForeColor = Color.Red));
    }

    [Fact]
    public void Form_CursorChanged_fires_when_the_forms_Cursor_changes ()
    {
        using var form = new Form ();
        Assert.Equal (1, Count (h => form.CursorChanged += h, () => form.Cursor = Cursors.Hand));
    }

    [Fact]
    public void Form_TextChanged_fires_when_Text_changes ()
    {
        using var form = new Form ();
        Assert.Equal (1, Count (h => form.TextChanged += h, () => form.Text = "Title"));
    }

    // ── Form: three events whose property lives on WindowBase ───────────────────────────────────────

    [Fact]
    public void Form_AutoSizeChanged_fires_when_AutoSize_changes ()
    {
        using var form = new Form ();
        Assert.Equal (1, Count (h => form.AutoSizeChanged += h, () => form.AutoSize = true));
    }

    [Fact]
    public void Form_MarginChanged_fires_when_Margin_changes ()
    {
        using var form = new Form ();
        Assert.Equal (1, Count (h => form.MarginChanged += h, () => form.Margin = new Padding (9)));
    }

    [Fact]
    public void Form_TabIndexChanged_fires_when_TabIndex_changes ()
    {
        using var form = new Form ();
        Assert.Equal (1, Count (h => form.TabIndexChanged += h, () => form.TabIndex = 3));
    }

    // ── Stored properties that now notify ───────────────────────────────────────────────────────────

    [Fact]
    public void Form_AutoValidateChanged_fires_when_AutoValidate_changes ()
    {
        using var form = new Form ();
        Assert.Equal (1, Count (h => form.AutoValidateChanged += h, () => form.AutoValidate = AutoValidate.Disable));
    }

    [Fact]
    public void Form_MaximizedBoundsChanged_fires_when_MaximizedBounds_changes ()
    {
        using var form = new Form ();
        Assert.Equal (1, Count (h => form.MaximizedBoundsChanged += h, () => form.MaximizedBounds = new Rectangle (1, 2, 300, 200)));
    }

    [Fact]
    public void Form_FormCornerPreferenceChanged_fires_when_FormCornerPreference_changes ()
    {
        using var form = new Form ();
        Assert.Equal (1, Count (h => form.FormCornerPreferenceChanged += h, () => form.FormCornerPreference = FormCornerPreference.DoNotRound));
    }

    [Fact]
    public void UserControl_AutoValidateChanged_fires_when_AutoValidate_changes ()
    {
        using var control = new UserControl ();
        Assert.Equal (1, Count (h => control.AutoValidateChanged += h, () => control.AutoValidate = AutoValidate.Disable));
    }

    [Fact]
    public void ContainerControl_AutoValidateChanged_fires_when_AutoValidate_changes ()
    {
        using var control = new ContainerControl ();
        Assert.Equal (1, Count (h => control.AutoValidateChanged += h, () => control.AutoValidate = AutoValidate.Disable));
    }

    [Fact]
    public void Control_RegionChanged_fires_when_Region_changes ()
    {
        using var control = new Panel ();
        using var region = new Majorsilence.Forms.Drawing.Region (new Rectangle (0, 0, 10, 10));
        Assert.Equal (1, Count (h => control.RegionChanged += h, () => control.Region = region));
    }

    [Fact]
    public void ToolStrip_LayoutStyleChanged_fires_when_LayoutStyle_changes ()
    {
        using var strip = new ToolStrip ();
        Assert.Equal (1, Count (h => strip.LayoutStyleChanged += h, () => strip.LayoutStyle = ToolStripLayoutStyle.VerticalStackWithOverflow));
    }

    [Fact]
    public void ToolStrip_RendererChanged_fires_when_Renderer_changes ()
    {
        using var strip = new ToolStrip ();
        var renderer = new ToolStripProfessionalRenderer ();
        Assert.Equal (1, Count (h => strip.RendererChanged += h, () => strip.Renderer = renderer));
    }

    [Fact]
    public void ToolStrip_OnRendererChanged_runs_when_Renderer_changes ()
    {
        using var strip = new RendererProbe ();
        strip.Renderer = new ToolStripProfessionalRenderer ();
        Assert.Equal (1, strip.RendererChanges);
    }

    private sealed class RendererProbe : ToolStrip
    {
        public int RendererChanges;
        protected override void OnRendererChanged (EventArgs e) { RendererChanges++; base.OnRendererChanged (e); }
    }

    [Fact]
    public void ToolStripManager_RendererChanged_fires_when_the_static_Renderer_changes ()
    {
        var before = ToolStripManager.Renderer;
        try {
            var renderer = new ToolStripProfessionalRenderer ();
            Assert.Equal (1, Count (h => ToolStripManager.RendererChanged += h, () => ToolStripManager.Renderer = renderer));
        } finally {
            ToolStripManager.Renderer = before;
        }
    }

    [Fact]
    public void ToolStripSplitButton_DefaultItemChanged_fires_when_DefaultItem_changes ()
    {
        var button = new ToolStripSplitButton ();
        var item = new ToolStripMenuItem ("Go");
        Assert.Equal (1, Count (h => button.DefaultItemChanged += h, () => button.DefaultItem = item));
    }

    [Fact]
    public void ToolStripItem_CommandParameterChanged_fires_when_CommandParameter_changes ()
    {
        var item = new ToolStripButton ();
        var parameter = new object ();
        Assert.Equal (1, Count (h => item.CommandParameterChanged += h, () => item.CommandParameter = parameter));
    }

    [Fact]
    public void ButtonBase_CommandChanged_fires_when_Command_changes ()
    {
        using var button = new Button ();
        var command = new NullCommand ();
        Assert.Equal (1, Count (h => button.CommandChanged += h, () => button.Command = command));
    }

    [Fact]
    public void ButtonBase_CommandParameterChanged_fires_when_CommandParameter_changes ()
    {
        using var button = new Button ();
        var parameter = new object ();
        Assert.Equal (1, Count (h => button.CommandParameterChanged += h, () => button.CommandParameter = parameter));
    }

    private sealed class NullCommand : ICommandExecutor
    {
        public event EventHandler? CommandCanExecuteChanged { add { } remove { } }
        public void Execute () { }
        public bool CanExecute () => true;
    }

    [Fact]
    public void PropertyGrid_PropertySortChanged_fires_when_PropertySort_changes ()
    {
        using var grid = new PropertyGrid ();
        Assert.Equal (1, Count (h => grid.PropertySortChanged += h, () => grid.PropertySort = PropertySort.Alphabetical));
    }

    [Fact]
    public void TabControl_RightToLeftLayoutChanged_fires_when_RightToLeftLayout_changes ()
    {
        using var tabs = new TabControl ();
        Assert.Equal (1, Count (h => tabs.RightToLeftLayoutChanged += h, () => tabs.RightToLeftLayout = true));
    }
}
