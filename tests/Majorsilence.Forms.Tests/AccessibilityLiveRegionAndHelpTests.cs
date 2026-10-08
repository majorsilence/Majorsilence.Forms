using System;
using System.Collections.Generic;
using System.Linq;
using Majorsilence.Forms.Automation;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// SMP-16 (Label.LiveSetting announces through a live region) and EVT-33 (Control.QueryAccessibilityHelp is
// raised from the accessible object's Help), tested through the automation tree, the observer and the
// browser accessibility DOM, which all run headless. The browser live region itself (#417) is covered by
// AriaDomTests.Live; these pin that a label's change takes one path to every consumer. The Windows UI Automation bridge that turns the same
// observer event into UIA's LiveRegionChanged, and reads HelpText through the same Help, only runs on Windows.
[Collection ("Headless")]
public class AccessibilityLiveRegionAndHelpTests
{
    private static Form BuildForm (out Label status, out Button button)
    {
        HeadlessRenderer.Use ();
        var form = new Form { Width = 300, Height = 200 };
        status = new Label { Name = "status", Text = "Ready", Left = 10, Top = 10, Width = 200 };
        button = new Button { Name = "save", Text = "Save", Left = 10, Top = 50, Width = 80 };
        form.Controls.Add (status);
        form.Controls.Add (button);
        HeadlessRenderer.CapturePng (form, 300, 200);
        return form;
    }

    private static AriaNode Node (Form form, string automationId)
    {
        HeadlessRenderer.CapturePng (form, form.Width, form.Height);
        return Assert.Single (AriaDom.Build (new[] { form }), n => n["data-mf-automation-id"] == automationId);
    }

    // ── SMP-16: Label.LiveSetting ───────────────────────────────────────────────────────────────────

    [Fact]
    public void A_live_labels_text_change_reaches_the_observer_as_a_live_region_change ()
    {
        using var form = BuildForm (out var status, out _);
        using var observer = new AutomationObserver (form);
        var changes = new List<AutomationElement?> ();
        observer.LiveRegionChanged += (_, e) => changes.Add (e);

        status.LiveSetting = AutomationLiveSetting.Polite;
        status.Text = "Saved";

        var change = Assert.Single (changes);
        Assert.Equal ("status", change?.AutomationId);
        Assert.Equal ("Saved", change?.Name);
        Assert.Equal (AutomationLiveSetting.Polite, change?.LiveSetting);
    }

    [Fact]
    public void A_label_whose_LiveSetting_is_Off_raises_no_live_region_change ()
    {
        using var form = BuildForm (out var status, out _);
        using var observer = new AutomationObserver (form);
        var changes = 0;
        observer.LiveRegionChanged += (_, _) => changes++;

        status.Text = "Saved";

        Assert.Equal (0, changes);
    }

    [Fact]
    public void RaiseLiveRegionChanged_is_true_only_when_something_takes_it ()
    {
        using var form = BuildForm (out var status, out _);
        status.LiveSetting = AutomationLiveSetting.Assertive;

        // Upstream returns false when no UI Automation client is listening; nothing listening here either.
        Assert.False (status.AccessibilityObject.RaiseLiveRegionChanged ());

        using (var observer = new AutomationObserver (form)) {
            // An observer that is not listening for live regions has not taken it.
            Assert.False (status.AccessibilityObject.RaiseLiveRegionChanged ());

            observer.LiveRegionChanged += (_, _) => { };
            Assert.True (status.AccessibilityObject.RaiseLiveRegionChanged ());
        }

        Assert.False (status.AccessibilityObject.RaiseLiveRegionChanged ());
    }

    [Fact]
    public void An_observer_of_another_window_does_not_take_a_live_region_change ()
    {
        using var form = BuildForm (out var status, out _);
        using var other = new Form { Width = 100, Height = 100 };
        using var observer = new AutomationObserver (other);
        var strays = 0;
        observer.LiveRegionChanged += (_, _) => strays++;
        status.LiveSetting = AutomationLiveSetting.Polite;

        status.Text = "Saved";

        Assert.Equal (0, strays);
        Assert.False (status.AccessibilityObject.RaiseLiveRegionChanged ());
    }

    [Fact]
    public void One_text_change_reaches_both_the_observer_and_the_browser_live_region_once ()
    {
        // One mechanism: the label's own raise (Label.OnTextChanged) feeds the observer -- what the UIA
        // bridge listens to -- and the browser mirror's live region, and the mirror noticing the same text
        // change itself does not make it a second announcement.
        using var form = BuildForm (out var status, out _);
        form.Show ();
        status.LiveSetting = AutomationLiveSetting.Assertive;
        var sink = new RecordingSink ();
        using var mirror = new AriaDomMirror (sink, () => new[] { form });
        using var observer = new AutomationObserver (form);
        var observed = new List<string?> ();
        observer.LiveRegionChanged += (_, e) => observed.Add (e?.Name);
        mirror.SyncNow ();

        status.Text = "Saved";
        HeadlessRenderer.CapturePng (form, form.Width, form.Height);
        mirror.SyncNow ();

        Assert.Equal (new[] { "Saved" }, observed);
        var said = Assert.Single (sink.Announced);
        Assert.Equal ("Saved", said.Text);
        Assert.True (said.Assertive);

        form.Close ();
    }

    [Fact]
    public void The_automation_tree_reports_a_labels_LiveSetting ()
    {
        using var form = BuildForm (out var status, out _);
        status.LiveSetting = AutomationLiveSetting.Assertive;

        var root = AutomationProvider.BuildTree (form);

        Assert.Equal (AutomationLiveSetting.Assertive, root.Self ().Single (e => e.AutomationId == "status").LiveSetting);
        Assert.Equal (AutomationLiveSetting.Off, root.Self ().Single (e => e.AutomationId == "save").LiveSetting);
    }

    [Fact]
    public void A_live_tool_strip_status_label_reports_its_LiveSetting_in_the_tree ()
    {
        using var form = BuildForm (out _, out _);
        var strip = new StatusStrip ();
        var label = new ToolStripStatusLabel { Name = "progress", Text = "Idle", LiveSetting = AutomationLiveSetting.Polite };
        strip.Items.Add (label);
        form.Controls.Add (strip);
        HeadlessRenderer.CapturePng (form, form.Width, form.Height);

        var element = AutomationProvider.BuildTree (form).Self ().Single (e => e.AutomationId == "progress");

        Assert.Equal (AutomationLiveSetting.Polite, element.LiveSetting);
    }

    [Fact]
    public void A_control_that_is_not_a_live_region_refuses_RaiseLiveRegionChanged ()
    {
        using var form = BuildForm (out _, out var button);

        // Upstream's ControlAccessibleObject throws for an owner that is not an IAutomationLiveRegion.
        Assert.Throws<InvalidOperationException> (() => button.AccessibilityObject.RaiseLiveRegionChanged ());
    }

    [Fact]
    public void A_controls_accessible_object_is_a_ControlAccessibleObject_for_it ()
    {
        using var form = BuildForm (out _, out var button);

        // Upstream Control.CreateAccessibilityInstance; the owner is what Help and live regions read.
        var accessible = Assert.IsType<Control.ControlAccessibleObject> (button.AccessibilityObject);
        Assert.Same (button, accessible.Owner);

        accessible.Name = "Save the order";
        Assert.Equal ("Save the order", button.AccessibleName);
    }

    // ── EVT-33: Control.QueryAccessibilityHelp ──────────────────────────────────────────────────────

    [Fact]
    public void Help_asks_the_QueryAccessibilityHelp_handlers ()
    {
        using var form = BuildForm (out _, out var button);
        object? sender = null;
        button.QueryAccessibilityHelp += (s, e) => {
            sender = s;
            e.HelpString = "Saves the order and closes the form";
            e.HelpNamespace = "orders.chm";
            e.HelpKeyword = "42";
        };

        Assert.Equal ("Saves the order and closes the form", button.AccessibilityObject.Help);
        Assert.Same (button, sender);

        Assert.Equal (42, button.AccessibilityObject.GetHelpTopic (out var file));
        Assert.Equal ("orders.chm", file);
    }

    [Fact]
    public void Without_a_handler_Help_is_the_accessible_objects_own ()
    {
        // Guard: the no-handler fallback was already what a plain AccessibleObject answered, so no
        // neutralization of the fix turns this red; it pins that the event is not invented when absent.
        using var form = BuildForm (out _, out var button);

        Assert.Null (button.AccessibilityObject.Help);
        Assert.Equal (-1, button.AccessibilityObject.GetHelpTopic (out var file));
        Assert.Null (file);
    }

    [Fact]
    public void A_help_keyword_that_is_not_a_number_is_topic_zero ()
    {
        using var form = BuildForm (out _, out var button);
        button.QueryAccessibilityHelp += (_, e) => e.HelpKeyword = "orders";

        // Upstream int.TryParse leaves 0 behind.
        Assert.Equal (0, button.AccessibilityObject.GetHelpTopic (out var file));
        Assert.Null (file);
    }

    [Fact]
    public void The_automation_tree_reads_help_through_Help_when_asked_not_when_built ()
    {
        using var form = BuildForm (out _, out var button);
        var raised = 0;
        button.QueryAccessibilityHelp += (_, e) => {
            raised++;
            e.HelpString = "Saves the order";
        };

        var save = AutomationProvider.BuildTree (form).Self ().Single (e => e.AutomationId == "save");
        Assert.Equal (0, raised);

        Assert.Equal ("Saves the order", save.HelpText);
        Assert.Equal ("Saves the order", save.HelpText);
        Assert.Equal (1, raised);
    }

    [Fact]
    public void The_browser_accessibility_DOM_describes_a_control_with_its_help ()
    {
        using var form = BuildForm (out _, out var button);

        Assert.Null (Node (form, "save")["aria-description"]);

        button.QueryAccessibilityHelp += (_, e) => e.HelpString = "Saves the order";
        Assert.Equal ("Saves the order", Node (form, "save")["aria-description"]);
    }

    private sealed class RecordingSink : IAriaDomSink
    {
        public readonly List<AriaAnnouncement> Announced = new ();

        public void Apply (string opsJson) { }

        public void SetActiveDescendant (string? elementId) { }

        public void Announce (string text, bool assertive, string? key) => Announced.Add (new AriaAnnouncement (text, assertive, key));
    }
}
