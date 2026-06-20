using System.ComponentModel;
using Continuum.Forms;
using Continuum.Forms.Telerik;

namespace ControlGallery.Panels
{
    // Showcases RadPropertyGrid (Continuum.Forms.Telerik) — backed by the real Continuum.Forms.PropertyGrid.
    // Setting SelectedObject displays the object's public properties grouped by [Category].
    public class TelerikPropertyGridPanel : BasePanel
    {
        public TelerikPropertyGridPanel ()
        {
            Controls.Add (new Label {
                Text = "RadPropertyGrid — set SelectedObject to inspect an object's properties (grouped by [Category]).",
                Left = 10, Top = 10, Width = 760
            });

            var pg = new RadPropertyGrid { Left = 10, Top = 40, Width = 360, Height = 320 };
            pg.SelectedObject = new Employee ();
            Controls.Add (pg);
        }

        private sealed class Employee
        {
            [Category ("Identity")] public string Name { get; set; } = "Alice Johnson";
            [Category ("Identity")] public int Age { get; set; } = 32;
            [Category ("Work")] public string Department { get; set; } = "Engineering";
            [Category ("Work")] public decimal Salary { get; set; } = 105000m;
            [Category ("Work")] public bool Active { get; set; } = true;
        }
    }
}
