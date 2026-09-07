using Majorsilence.Forms.Gtk4;
using CF = Majorsilence.Forms;

namespace EmbeddingGtk4;

// A host-owned GTK 4 (gir.core) app that embeds Majorsilence.Forms content via
// MajorsilenceFormsPresenter / control.ToGtkWidget (). The GTK host owns the Gtk.Application and its
// main loop; the Gtk4 backend runs inside it.
//
// Run on a desktop session (X11/Wayland): `dotnet run --project samples/EmbeddingGtk4`.
public static class Program
{
    [System.STAThread]
    public static int Main (string[] args)
    {
        var app = global::Gtk.Application.New ("com.majorsilence.forms.embeddinggtk4", global::Gio.ApplicationFlags.DefaultFlags);
        app.OnActivate += (sender, _) => BuildWindow ((global::Gtk.Application) sender);
        return app.RunWithSynchronizationContext (args);
    }

    private static void BuildWindow (global::Gtk.Application app)
    {
        var window = global::Gtk.ApplicationWindow.New (app);
        window.SetTitle ("Majorsilence.Forms embedded in GTK 4");
        window.SetDefaultSize (820, 560);

        var root = global::Gtk.Box.New (global::Gtk.Orientation.Vertical, 8);
        root.SetMarginTop (12);
        root.SetMarginBottom (12);
        root.SetMarginStart (12);
        root.SetMarginEnd (12);

        // ── Native GTK row ──────────────────────────────────────────────────────
        root.Append (Heading ("Native GTK 4 widgets"));

        var nativeRow = global::Gtk.Box.New (global::Gtk.Orientation.Horizontal, 8);
        var nativeEntry = global::Gtk.Entry.New ();
        nativeEntry.SetPlaceholderText ("A native GTK Entry");
        nativeEntry.SetHexpand (true);

        var status = global::Gtk.Label.New ("Clicks: 0");
        status.SetXalign (0);

        var dialogButton = global::Gtk.Button.NewWithLabel ("Open a Majorsilence Form as a GTK window");
        dialogButton.OnClicked += (_, _) => {
            // Gtk4HostInterop.ToGtkWindow (): a Majorsilence.Forms Form's backend window is a real
            // Gtk.Window, created eagerly in the Form's own constructor. Hand it back and let the host
            // present it.
            var form = BuildDialogForm ();
            var gtkWindow = form.ToGtkWindow ();
            gtkWindow.SetTransientFor (window);
            gtkWindow.SetModal (true);
            gtkWindow.Present ();
        };

        nativeRow.Append (nativeEntry);
        nativeRow.Append (dialogButton);
        root.Append (nativeRow);

        root.Append (global::Gtk.Separator.New (global::Gtk.Orientation.Horizontal));

        // ── Embedded Majorsilence.Forms scene ──────────────────────────────────
        root.Append (Heading ("Embedded Majorsilence.Forms (MajorsilenceFormsPresenter)"));

        var presenter = new MajorsilenceFormsPresenter { Content = BuildMajorsilenceScene (status) };
        presenter.Widget.SetVexpand (true);
        presenter.Widget.SetHexpand (true);
        root.Append (presenter.Widget);

        root.Append (status);

        window.SetChild (root);
        window.Present ();

        if (System.Environment.GetEnvironmentVariable ("EMBED_SELFTEST") == "1") {
            var paints = 0;
            presenter.Surface.Paint += (_, _) => System.Console.Error.WriteLine ($"[selftest] embedded scene paint #{++paints}");
            var ticks = 0;
            global::GLib.Functions.TimeoutAdd (0, 600, () => {
                ticks++;
                System.Console.Error.WriteLine ($"[selftest] tick {ticks}; presenter widget size = {presenter.Widget.GetWidth ()}x{presenter.Widget.GetHeight ()}");
                if (ticks == 2) {
                    var w = BuildDialogForm ().ToGtkWindow ();
                    w.SetTransientFor (window);
                    w.Present ();
                    System.Console.Error.WriteLine ($"[selftest] ToGtkWindow () -> {w.GetType ().Name}, presented");
                }
                if (ticks >= 4) { app.Quit (); return false; }
                return true;
            });
        }
    }

    private static global::Gtk.Label Heading (string text)
    {
        var label = global::Gtk.Label.New (null);
        label.SetMarkup ($"<b>{text}</b>");
        label.SetXalign (0);
        return label;
    }

    // A small Majorsilence.Forms Form shown as a real GTK window via ToGtkWindow ().
    private static CF.Form BuildDialogForm ()
    {
        var form = new CF.Form {
            Text = "Majorsilence.Forms dialog",
            ClientSize = new System.Drawing.Size (320, 140)
        };

        var label = new CF.Label {
            Text = "This Form is hosted by a real GTK 4 window.",
            Left = 12, Top = 12, Width = 288, Height = 40
        };
        var closeButton = new CF.Button { Text = "Close", Left = 12, Top = 60, Width = 100, Height = 32 };
        closeButton.Click += (_, _) => form.Close ();

        form.Controls.Add (label);
        form.Controls.Add (closeButton);
        return form;
    }

    // A Majorsilence.Forms control tree exercising render + input + a popup (combo dropdown).
    private static CF.Panel BuildMajorsilenceScene (global::Gtk.Label status)
    {
        var panel = new CF.Panel ();

        var label = new CF.Label { Text = "Majorsilence.Forms controls:", Left = 12, Top = 12, Width = 420, Height = 24 };
        var textbox = new CF.TextBox { Text = "Edit me", Left = 12, Top = 44, Width = 240, Height = 30 };

        var combo = new CF.ComboBox { Left = 12, Top = 84, Width = 240, Height = 30 };
        combo.Items.Add ("First");
        combo.Items.Add ("Second");
        combo.Items.Add ("Third");

        var button = new CF.Button { Text = "Majorsilence Button", Left = 12, Top = 124, Width = 160, Height = 34 };

        var clicks = 0;
        button.Click += (_, _) => {
            clicks++;
            status.SetText ($"Clicks: {clicks} — text: \"{textbox.Text}\"");
        };

        panel.Controls.Add (label);
        panel.Controls.Add (textbox);
        panel.Controls.Add (combo);
        panel.Controls.Add (button);
        return panel;
    }
}
