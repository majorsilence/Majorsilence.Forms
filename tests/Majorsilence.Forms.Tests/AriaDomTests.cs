using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using Majorsilence.Forms.Automation;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// The accessibility DOM the browser target keeps next to its canvas (docs/backends.md, "Accessibility
// DOM (browser)"). Avalonia.Browser has no such layer of its own, so the browser backend mirrors the
// framework's automation tree into ARIA elements; everything but the final JavaScript DOM writes is
// host-neutral and tested here: the tree -> ARIA mapping, the diff between snapshots, the wire format,
// and the mirror that drives a sink.
public class AriaDomTests
{
    private static Form BuildForm ()
    {
        var form = new Form { Text = "Customer", Width = 500, Height = 400 };

        var panel = new Panel { Name = "panel1", Left = 20, Top = 30, Width = 300, Height = 200 };
        panel.Controls.Add (new Button { Name = "saveButton", Text = "&Save", Left = 5, Top = 7, Width = 80, Height = 25 });
        form.Controls.Add (panel);

        form.Controls.Add (new CheckBox { Name = "agree", Text = "I agree", Checked = true, Left = 20, Top = 240, Width = 120 });
        form.Controls.Add (new TextBox { Name = "nameBox", AccessibleName = "Customer name", Text = "Ada", Left = 20, Top = 270, Width = 150 });
        form.Controls.Add (new TextBox { Name = "pin", AccessibleName = "PIN", Text = "secret-1234", PasswordChar = '*', Left = 200, Top = 270, Width = 100 });
        form.Controls.Add (new Label { Name = "hint", Text = "All fields are required", Left = 20, Top = 300, Width = 200 });
        form.Controls.Add (new Button { Name = "deleteButton", Text = "Delete", Enabled = false, Left = 340, Top = 30, Width = 80 });

        var list = new ListBox { Name = "colours", Left = 340, Top = 70, Width = 120, Height = 80 };
        list.Items.AddRange (new object[] { "Red", "Green", "Blue" });
        list.SelectedIndex = 1;
        form.Controls.Add (list);

        return form;
    }

    private static List<AriaNode> Build (Form form)
    {
        HeadlessRenderer.CapturePng (form, form.Width, form.Height);   // lay it out
        return AriaDom.Build (new[] { form });
    }

    private static AriaNode Node (IEnumerable<AriaNode> nodes, string automationId) =>
        Assert.Single (nodes, n => n["data-mf-automation-id"] == automationId);

    [Fact]
    public void Controls_map_to_ARIA_roles_names_and_states ()
    {
        using var form = BuildForm ();
        var nodes = Build (form);

        var window = nodes[0];
        Assert.Null (window.ParentId);
        Assert.Equal ("region", window.Role);
        Assert.Equal ("Customer", window["aria-label"]);

        // Named from content (not aria-label), so find-in-page and text locators match it; the mnemonic
        // marker is not part of the name.
        var save = Node (nodes, "saveButton");
        Assert.Equal ("button", save.Role);
        Assert.Equal ("Save", save.Text);
        Assert.Null (save["aria-label"]);

        var agree = Node (nodes, "agree");
        Assert.Equal ("checkbox", agree.Role);
        Assert.Equal ("true", agree["aria-checked"]);

        var name = Node (nodes, "nameBox");
        Assert.Equal ("textbox", name.Role);
        Assert.Equal ("Customer name", name["aria-label"]);
        Assert.Equal ("Ada", name.Text);

        Assert.Equal ("true", Node (nodes, "deleteButton")["aria-disabled"]);
        Assert.Null (Node (nodes, "saveButton")["aria-disabled"]);

        // Static text is plain content with no role.
        var hint = Node (nodes, "hint");
        Assert.Null (hint.Role);
        Assert.Equal ("All fields are required", hint.Text);

        var list = Node (nodes, "colours");
        Assert.Equal ("listbox", list.Role);
        var options = nodes.Where (n => n.ParentId == list.Id).ToList ();
        Assert.Equal (new[] { "Red", "Green", "Blue" }, options.Select (o => o.Text));
        Assert.All (options, o => Assert.Equal ("option", o.Role));
        Assert.Equal (new[] { "false", "true", "false" }, options.Select (o => o["aria-selected"]));
    }

    [Fact]
    public void A_password_never_reaches_the_page ()
    {
        using var form = BuildForm ();
        var nodes = Build (form);

        var pin = Node (nodes, "pin");
        Assert.Equal ("textbox", pin.Role);
        Assert.Equal ("PIN", pin["aria-label"]);
        Assert.Null (pin.Text);

        // Nowhere -- not in any attribute, not in the serialized operations the browser receives.
        Assert.DoesNotContain (nodes, n => (n.Text ?? "").Contains ("secret") || n.Attributes.Any (a => a.Value.Contains ("secret")));
        Assert.DoesNotContain ("secret", AriaDom.ToJson (AriaDom.Diff (new Dictionary<string, AriaNode> (), nodes)));
    }

    [Fact]
    public void A_designer_identifier_is_an_automation_id_not_a_name ()
    {
        using var form = BuildForm ();
        var nodes = Build (form);

        // The automation tree names a nameless panel by its Name ("panel1"); a reader should not
        // announce that, and an unnamed layout panel is not a group worth announcing at all.
        var panel = Node (nodes, "panel1");
        Assert.Null (panel.Role);
        Assert.Null (panel["aria-label"]);
        Assert.Null (panel.Text);
    }

    [Fact]
    public void Bounds_are_relative_to_the_parent_element ()
    {
        using var form = BuildForm ();
        var nodes = Build (form);

        var panel = Node (nodes, "panel1");
        var save = Node (nodes, "saveButton");

        Assert.Equal (panel.Id, save.ParentId);
        // The button's own Location inside the panel: the absolute positioning in the DOM nests.
        Assert.Equal (new Rectangle (5, 7, 80, 25), save.Bounds);

        // The window element sits where the window is; its children are offset past the frame, by the
        // same amount for every child.
        var window = nodes[0];
        Assert.Equal (form.Bounds, window.Bounds);
        var frame = new Size (panel.Bounds.X - 20, panel.Bounds.Y - 30);
        Assert.Equal (new Point (20 + frame.Width, 240 + frame.Height), Node (nodes, "agree").Bounds.Location);
    }

    [Fact]
    public void A_modal_dialog_is_an_ARIA_modal_dialog ()
    {
        using var owner = new Form { Text = "Main" };
        owner.Show ();
        using var dialog = new Form { Text = "Confirm" };
        _ = dialog.ShowDialogAsync (owner);

        var nodes = AriaDom.Build (new Form[] { owner, dialog });
        var windows = nodes.Where (n => n.ParentId is null).ToList ();

        Assert.Equal (2, windows.Count);
        Assert.Equal ("region", windows[0].Role);
        Assert.Equal ("dialog", windows[1].Role);
        Assert.Equal ("true", windows[1]["aria-modal"]);
        Assert.Equal ("Confirm", windows[1]["aria-label"]);
        Assert.Equal (1, windows[1].Index);

        dialog.Close ();
    }

    [Fact]
    public void An_unchanged_UI_needs_no_DOM_changes ()
    {
        using var form = BuildForm ();
        var first = Build (form);
        var second = Build (form);

        // Same controls, same ids: the mirror is keyed by control, not by position or snapshot.
        Assert.Equal (first.Select (n => n.Id), second.Select (n => n.Id));
        Assert.Empty (AriaDom.Diff (first.ToDictionary (n => n.Id), second));
    }

    [Fact]
    public void Renaming_a_control_updates_only_its_element ()
    {
        using var form = BuildForm ();
        var before = Build (form).ToDictionary (n => n.Id);
        var save = form.Controls.Find ("saveButton", true).Single ();

        save.Text = "Save &all";
        var ops = AriaDom.Diff (before, Build (form));

        var op = Assert.Single (ops);
        Assert.False (op.IsRemove);
        Assert.Equal ("Save all", op.Node!.Text);
        Assert.Equal (before.Values.Single (n => n.Source == save).Id, op.Id);
    }

    [Fact]
    public void Removing_a_container_removes_only_its_top_element ()
    {
        using var form = BuildForm ();
        var before = Build (form).ToDictionary (n => n.Id);
        var panel = form.Controls.Find ("panel1", false).Single ();
        var panelId = before.Values.Single (n => n.Source == panel).Id;

        form.Controls.Remove (panel);
        var ops = AriaDom.Diff (before, Build (form));

        // The button inside goes with its parent element; a removal of its own would be redundant. The
        // siblings after the panel move up one place, which is an update each, not a rebuild.
        Assert.Equal (panelId, Assert.Single (ops, o => o.IsRemove).Id);
        Assert.All (ops.Where (o => !o.IsRemove), o => Assert.Equal (before[o.Id].Index - 1, o.Node!.Index));
    }

    [Fact]
    public void Adding_a_control_creates_its_element_in_z_order ()
    {
        using var form = BuildForm ();
        var before = Build (form).ToDictionary (n => n.Id);
        var added = new Button { Name = "addedButton", Text = "New", Left = 20, Top = 330 };

        form.Controls.Add (added);
        var after = Build (form);
        var ops = AriaDom.Diff (before, after);

        var created = Assert.Single (ops, o => !before.ContainsKey (o.Id));
        Assert.Same (added, created.Node!.Source);
        Assert.Equal ("button", created.Node.Role);
        Assert.Equal (after[0].Id, created.Node.ParentId);
    }

    [Fact]
    public void The_wire_format_is_the_operation_list_the_script_applies ()
    {
        using var form = BuildForm ();
        var nodes = Build (form);
        var save = Node (nodes, "saveButton");
        var ops = new List<AriaDomOp> { AriaDomOp.Remove ("gone"), AriaDomOp.Upsert (save) };

        using var json = JsonDocument.Parse (AriaDom.ToJson (ops));
        var array = json.RootElement.EnumerateArray ().ToArray ();

        Assert.Equal ("mf-a11y-gone", array[0].GetProperty ("remove").GetString ());

        var upsert = array[1];
        Assert.Equal (save.ElementId, upsert.GetProperty ("id").GetString ());
        Assert.Equal (AriaDom.ElementIdPrefix + save.ParentId, upsert.GetProperty ("parent").GetString ());
        Assert.Equal ("button", upsert.GetProperty ("role").GetString ());
        Assert.Equal ("Save", upsert.GetProperty ("text").GetString ());
        Assert.Equal ("saveButton", upsert.GetProperty ("attrs").GetProperty ("data-mf-automation-id").GetString ());
        Assert.Equal (save.Bounds, new Rectangle (upsert.GetProperty ("x").GetInt32 (), upsert.GetProperty ("y").GetInt32 (),
            upsert.GetProperty ("w").GetInt32 (), upsert.GetProperty ("h").GetInt32 ()));

        var window = AriaDom.ToJson (new List<AriaDomOp> { AriaDomOp.Upsert (nodes[0]) });
        Assert.Equal (JsonValueKind.Null, JsonDocument.Parse (window).RootElement[0].GetProperty ("parent").ValueKind);
    }

    private sealed class RecordingSink : IAriaDomSink
    {
        public readonly List<string> Batches = new ();
        public readonly List<string?> Active = new ();

        public void Apply (string opsJson) => Batches.Add (opsJson);

        public void SetActiveDescendant (string? elementId) => Active.Add (elementId);
    }

    [Fact]
    public void The_mirror_sends_the_tree_once_then_only_what_changed ()
    {
        using var form = BuildForm ();
        form.Show ();
        HeadlessRenderer.CapturePng (form, form.Width, form.Height);
        var sink = new RecordingSink ();
        using var mirror = new AriaDomMirror (sink, () => new[] { form });

        mirror.SyncNow ();
        Assert.Single (sink.Batches);
        Assert.Equal (mirror.Current.Count, JsonDocument.Parse (sink.Batches[0]).RootElement.GetArrayLength ());

        mirror.SyncNow ();
        Assert.Single (sink.Batches);   // nothing changed, nothing sent

        form.Controls.Find ("hint", false).Single ().Text = "Name is required";
        mirror.SyncNow ();

        Assert.Equal (2, sink.Batches.Count);
        var op = Assert.Single (JsonDocument.Parse (sink.Batches[1]).RootElement.EnumerateArray ());
        Assert.Equal ("Name is required", op.GetProperty ("text").GetString ());

        form.Close ();
    }

    [Fact]
    public void The_mirror_points_aria_activedescendant_at_the_focused_control ()
    {
        using var form = BuildForm ();
        form.Show ();
        var sink = new RecordingSink ();
        using var mirror = new AriaDomMirror (sink, () => new[] { form });
        var nameBox = form.Controls.Find ("nameBox", false).Single ();
        var agree = form.Controls.Find ("agree", false).Single ();

        nameBox.Focus ();
        mirror.SyncNow ();
        Assert.Equal (mirror.Current.Values.Single (n => n.Source == nameBox).ElementId, sink.Active.Last ());

        agree.Focus ();
        mirror.SyncNow ();
        Assert.Equal (mirror.Current.Values.Single (n => n.Source == agree).ElementId, sink.Active.Last ());

        // Focus moving is not an element change: no batch is sent just for it.
        var batches = sink.Batches.Count;
        nameBox.Focus ();
        mirror.SyncNow ();
        Assert.Equal (batches, sink.Batches.Count);

        form.Close ();
    }

    [Fact]
    public void Rendering_a_frame_schedules_a_sync ()
    {
        using var form = BuildForm ();
        form.Show ();
        var sink = new RecordingSink ();
        using var mirror = new AriaDomMirror (sink, () => new[] { form });

        HeadlessRenderer.CapturePng (form, form.Width, form.Height);

        // The headless timer delivers its tick through the UI queue, which a nested modal loop drains;
        // wait on one for a little longer than the mirror's interval.
        var done = new System.Threading.Tasks.TaskCompletionSource<bool> ();
        var timer = Backends.Platform.Backend.CreateTimer ();
        timer.IntervalMilliseconds = AriaDomMirror.IntervalMilliseconds * 3;
        timer.Tick += () => { timer.Stop (); done.TrySetResult (true); };
        timer.Start ();
        Backends.Platform.Backend.RunModalLoop (done.Task);
        timer.Dispose ();

        Assert.NotEmpty (sink.Batches);

        form.Close ();
    }
}
