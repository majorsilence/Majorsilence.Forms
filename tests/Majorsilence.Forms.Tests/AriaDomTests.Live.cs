using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Majorsilence.Forms.Automation;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// The accessibility DOM's live region: what AriaDom.Announcements says for a change between two
// snapshots, and the explicit announcements (RaiseAutomationNotification, RaiseLiveRegionChanged) the
// mirror delivers. The JavaScript side only speaks and debounces what these produce.
public partial class AriaDomTests
{
    private static Dictionary<string, AriaNode> Snapshot (Form form) => Build (form).ToDictionary (n => n.Id);

    private static List<AriaAnnouncement> Said (Dictionary<string, AriaNode> before, Form form) =>
        AriaDom.Announcements (before, Build (form));

    [Fact]
    public void A_live_labels_new_text_is_announced_with_its_politeness ()
    {
        using var form = new Form { Text = "Live", Width = 400, Height = 300 };
        var polite = new Label { Name = "saved", Text = "Not saved", LiveSetting = AutomationLiveSetting.Polite, Top = 10, Width = 200 };
        var urgent = new Label { Name = "error", Text = "", LiveSetting = AutomationLiveSetting.Assertive, Top = 40, Width = 200 };
        var quiet = new Label { Name = "clock", Text = "10:00", Top = 70, Width = 200 };
        form.Controls.AddRange (new Control[] { polite, urgent, quiet });

        var before = Snapshot (form);
        polite.Text = "Saved";
        urgent.Text = "Disk full";
        quiet.Text = "10:01";
        var said = Said (before, form);

        // LiveSetting Off -- upstream's default -- is not announced: a clock ticking would never stop talking.
        Assert.Equal (2, said.Count);
        Assert.Contains (said, a => a.Text == "Saved" && !a.Assertive);
        Assert.Contains (said, a => a.Text == "Disk full" && a.Assertive);

        // Only on change: the same text again says nothing.
        Assert.Empty (Said (Snapshot (form), form));
    }

    [Fact]
    public void A_status_bars_text_changes_are_announced_politely ()
    {
        using var form = new Form { Text = "Status", Width = 400, Height = 300 };
        var status = new StatusStrip ();
        var label = new ToolStripStatusLabel { Text = "Ready" };
        status.Items.Add (label);
        form.Controls.Add (status);

        var before = Snapshot (form);
        label.Text = "3 records loaded";

        var said = Assert.Single (Said (before, form));
        Assert.Equal ("3 records loaded", said.Text);
        Assert.False (said.Assertive);
        Assert.Equal (AriaDom.KeyOf (label), said.Key);
    }

    [Fact]
    public void The_first_snapshot_announces_nothing ()
    {
        using var owner = new Form { Text = "Load", Width = 400, Height = 300 };
        owner.Controls.Add (new Label { Text = "Welcome", LiveSetting = AutomationLiveSetting.Assertive });
        owner.Show ();
        using var dialog = new Form { Text = "Sign in", Width = 200, Height = 100 };
        _ = dialog.ShowDialogAsync (owner);

        // A page that just loaded has not changed -- even with a dialog already up when the mirror
        // starts; announcing all of it would talk over the reader reading the page.
        Assert.Empty (AriaDom.Announcements (new Dictionary<string, AriaNode> (), AriaDom.Build (new Form[] { owner, dialog })));

        dialog.Close ();
    }

    [Fact]
    public void A_message_box_opening_is_announced_and_an_error_interrupts ()
    {
        using var owner = new Form { Text = "Main", Width = 400, Height = 300 };
        owner.Show ();
        var before = AriaDom.Build (new Form[] { owner }).ToDictionary (n => n.Id);

        using var box = new MessageBoxForm ("Save failed", "The disk is full.", MessageBoxButtons.OK, MessageBoxIcon.Error,
            MessageBoxDefaultButton.Button1, 0);
        _ = box.ShowDialogAsync (owner);
        var nodes = AriaDom.Build (new Form[] { owner, box });

        var dialog = nodes.Single (n => n.ParentId is null && n.Index == 1);
        Assert.Equal ("alertdialog", dialog.Role);
        Assert.Equal ("true", dialog["aria-modal"]);
        Assert.Equal (NodeOf (nodes, box.MessageLabel).ElementId, dialog["aria-describedby"]);

        var said = Assert.Single (AriaDom.Announcements (before, nodes));
        Assert.Equal ("Save failed. The disk is full.", said.Text);
        Assert.True (said.Assertive);

        // Said once, when it opens -- not again while it stays open.
        Assert.Empty (AriaDom.Announcements (nodes.ToDictionary (n => n.Id), AriaDom.Build (new Form[] { owner, box })));

        box.Close ();
    }

    [Fact]
    public void A_modal_dialog_opening_is_announced_politely_by_its_title ()
    {
        using var owner = new Form { Text = "Main", Width = 400, Height = 300 };
        owner.Show ();
        var before = AriaDom.Build (new Form[] { owner }).ToDictionary (n => n.Id);

        using var dialog = new Form { Text = "Options", Width = 200, Height = 100 };
        _ = dialog.ShowDialogAsync (owner);

        var said = Assert.Single (AriaDom.Announcements (before, AriaDom.Build (new Form[] { owner, dialog })));
        Assert.Equal ("Options", said.Text);
        Assert.False (said.Assertive);

        dialog.Close ();
    }

    [Fact]
    public void The_focused_combo_boxs_new_value_is_announced_only_while_focus_stays ()
    {
        using var form = ComboForm (out var combo);
        var other = new Button { Name = "go", Text = "Go", Left = 10, Top = 100 };
        form.Controls.Add (other);

        // Focus arriving: the reader announces the combo box, value and all, itself.
        other.Focus ();
        var before = Snapshot (form);
        combo.Focus ();
        combo.SelectedIndex = 2;
        Assert.DoesNotContain (Said (before, form), a => a.Text == "Cherry");

        // Focus staying, value changing (an arrow key on a closed list): said, politely.
        before = Snapshot (form);
        combo.SelectedIndex = 0;
        var said = Assert.Single (Said (before, form));
        Assert.Equal ("Apple", said.Text);
        Assert.False (said.Assertive);

        // A value changed in code on a control the user is not on: not said.
        other.Focus ();
        before = Snapshot (form);
        combo.SelectedIndex = 1;
        Assert.Empty (Said (before, form));

        form.Close ();
    }

    [Fact]
    public void RaiseAutomationNotification_is_delivered_through_the_mirror ()
    {
        using var form = BuildForm ();
        form.Show ();
        var sink = new RecordingSink ();
        var button = form.Controls.Find ("saveButton", true).Single ();

        using (var mirror = new AriaDomMirror (sink, () => new[] { form })) {
            mirror.SyncNow ();

            Assert.True (button.AccessibilityObject.RaiseAutomationNotification (
                AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.ImportantAll, "Saved"));
            Assert.True (button.AccessibilityObject.RaiseAutomationNotification (
                AutomationNotificationKind.Other, AutomationNotificationProcessing.MostRecent, "3 of 10"));
            mirror.SyncNow ();

            Assert.Equal (2, sink.Announced.Count);
            Assert.True (sink.Announced[0].Assertive);
            Assert.Equal ("Saved", sink.Announced[0].Text);
            Assert.Null (sink.Announced[0].Key);   // "all": each one is said
            Assert.False (sink.Announced[1].Assertive);
            Assert.NotNull (sink.Announced[1].Key);   // "most recent": a newer one replaces it
        }

        // With nothing listening it is not delivered, and says so, as upstream does with no client.
        Assert.False (button.AccessibilityObject.RaiseAutomationNotification (
            AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.All, "Saved"));

        form.Close ();
    }

    [Fact]
    public void RaiseLiveRegionChanged_after_setting_the_text_announces_once ()
    {
        using var form = new Form { Text = "Live", Width = 400, Height = 300 };
        var label = new Label { Name = "saved", Text = "Not saved", LiveSetting = AutomationLiveSetting.Polite, Width = 200 };
        var plain = new Label { Name = "plain", Text = "x", Top = 40 };
        form.Controls.Add (label);
        form.Controls.Add (plain);
        form.Show ();
        HeadlessRenderer.CapturePng (form, form.Width, form.Height);
        var sink = new RecordingSink ();

        using (var mirror = new AriaDomMirror (sink, () => new[] { form })) {
            mirror.SyncNow ();

            // The pattern written for .NET Framework, where setting the text alone announced nothing.
            label.Text = "Saved";
            Assert.True (label.AccessibilityObject.RaiseLiveRegionChanged ());
            mirror.SyncNow ();

            var said = Assert.Single (sink.Announced);
            Assert.Equal ("Saved", said.Text);

            // Not a live region: not delivered.
            Assert.False (plain.AccessibilityObject.RaiseLiveRegionChanged ());
        }

        form.Close ();
    }

    [Fact]
    public void A_mirror_without_announcements_says_nothing_and_delivers_nothing ()
    {
        using var form = new Form { Text = "Quiet", Width = 400, Height = 300 };
        var label = new Label { Text = "a", LiveSetting = AutomationLiveSetting.Assertive };
        form.Controls.Add (label);
        form.Show ();
        var sink = new RecordingSink ();

        using var mirror = new AriaDomMirror (sink, () => new[] { form }, announce: false);
        mirror.SyncNow ();
        label.Text = "b";
        Assert.False (label.AccessibilityObject.RaiseLiveRegionChanged ());
        mirror.SyncNow ();

        Assert.Empty (sink.Announced);

        form.Close ();
    }

    [Fact]
    public async Task Closing_a_popup_schedules_a_sync_without_a_frame ()
    {
        using var form = ComboForm (out var combo);
        HeadlessRenderer.CapturePng (form, form.Width, form.Height);
        var sink = new RecordingSink ();
        using var mirror = new AriaDomMirror (sink, () => WindowsOf (form));

        combo.DroppedDown = true;
        Assert.Contains (PopupWindow.ShownPopups, p => ReferenceEquals (p.ParentWindow, form));
        mirror.SyncNow ();
        var popup = mirror.Current.Values.Single (n => n["data-mf-popup"] == "listbox");
        var batches = sink.Batches.Count;

        combo.DroppedDown = false;
        Assert.DoesNotContain (PopupWindow.ShownPopups, p => ReferenceEquals (p.ParentWindow, form));

        // Hiding a popup paints nothing; ShownPopupsChanged is what tells the mirror. Wait on the UI
        // queue a little longer than the mirror's interval, as Rendering_a_frame_schedules_a_sync does.
        var done = new TaskCompletionSource<bool> ();
        var timer = Backends.Platform.Backend.CreateTimer ();
        timer.IntervalMilliseconds = AriaDomMirror.IntervalMilliseconds * 3;
        timer.Tick += () => { timer.Stop (); done.TrySetResult (true); };
        timer.Start ();
        Backends.Platform.Backend.RunModalLoop (done.Task);
        timer.Dispose ();
        await done.Task;

        Assert.True (sink.Batches.Count > batches);
        Assert.Contains (popup.ElementId, sink.Batches.Last ());
        Assert.DoesNotContain (mirror.Current.Values, n => n["data-mf-popup"] is not null);

        form.Close ();
    }

    [Fact]
    public async Task Closing_a_dialog_schedules_a_sync_without_a_frame ()
    {
        using var owner = new Form { Text = "Main", Width = 400, Height = 300 };
        owner.Show ();
        var dialog = new Form { Text = "Options", Width = 200, Height = 100 };
        _ = dialog.ShowDialogAsync (owner);
        var sink = new RecordingSink ();
        using var mirror = new AriaDomMirror (sink, () => new Form[] { owner, dialog }.Where (f => Application.OpenForms.Contains (f)));
        mirror.SyncNow ();
        Assert.Contains (mirror.Current.Values, n => n.Role == "dialog");

        // The browser draws no frame when a dialog closes over a form that does not repaint; the
        // collection of open forms changing is what tells the mirror.
        dialog.Close ();

        var done = new TaskCompletionSource<bool> ();
        var timer = Backends.Platform.Backend.CreateTimer ();
        timer.IntervalMilliseconds = AriaDomMirror.IntervalMilliseconds * 3;
        timer.Tick += () => { timer.Stop (); done.TrySetResult (true); };
        timer.Start ();
        Backends.Platform.Backend.RunModalLoop (done.Task);
        timer.Dispose ();
        await done.Task;

        Assert.DoesNotContain (mirror.Current.Values, n => n.Role == "dialog");
        dialog.Dispose ();
    }
}
