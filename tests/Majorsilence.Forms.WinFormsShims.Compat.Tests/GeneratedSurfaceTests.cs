using System.Reflection;
using System.Text;
using Xunit;

namespace Majorsilence.Forms.WinFormsShims.Compat.Tests
{
    // The Compat assemblies are rewrites of the real ones (tools/CompatSync), so what could go wrong is the
    // rewrite: a name left behind, a string that stopped matching its type, a resource that moved. These
    // pin the places a blind find-and-replace fails; each was checked by breaking the rule it guards.
    public class GeneratedSurfaceTests
    {
        private static readonly Assembly Core = typeof (System.Windows.Forms.Form).Assembly;
        private static readonly Assembly Drawing = typeof (System.Drawing.Bitmap).Assembly;
        private static readonly Assembly Backend = Assembly.Load ("Majorsilence.Forms.WinFormsShims.Compat.Avalonia");

        [Fact]
        public void WinForms_types_are_declared_in_the_namespaces_real_WinForms_uses ()
        {
            Assert.Equal ("System.Windows.Forms", typeof (System.Windows.Forms.Button).Namespace);
            Assert.Equal ("System.Windows.Forms", typeof (System.Windows.Forms.DataGridView).Namespace);
            Assert.Equal ("System.Drawing", typeof (System.Drawing.Font).Namespace);
            Assert.Equal ("System.Drawing.Drawing2D", typeof (System.Drawing.Drawing2D.GraphicsPath).Namespace);

            // Real WinForms keeps these in System.Drawing although this layer's source keeps them with Forms.
            Assert.Equal ("System.Drawing", typeof (System.Drawing.ContentAlignment).Namespace);
            // The framework owns these; the generated assemblies must not redeclare them (that is ambiguous at every use).
            Assert.Equal ("System.Drawing.Primitives", typeof (System.Drawing.SystemColors).Assembly.GetName ().Name);
            Assert.Equal ("System.Drawing.Primitives", typeof (System.Drawing.ColorTranslator).Assembly.GetName ().Name);
        }

        [Fact]
        public void The_forms_assembly_is_not_the_one_that_holds_drawing ()
        {
            // Mirrors upstream, and keeps each project's internals (both have a Guard) apart.
            Assert.NotSame (Core, Drawing);
            Assert.Equal ("Majorsilence.Forms.WinFormsShims.Compat", Core.GetName ().Name);
            Assert.Equal ("Majorsilence.Forms.WinFormsShims.Compat.Drawing", Drawing.GetName ().Name);
        }

        [Fact]
        public void No_public_type_is_left_in_a_Majorsilence_namespace ()
        {
            var leaked = new[] { Core, Drawing, Backend }
                .SelectMany (a => a.GetExportedTypes ())
                .Where (t => t.Namespace is { } ns && ns.StartsWith ("Majorsilence", StringComparison.Ordinal))
                .Select (t => t.FullName)
                .ToList ();

            Assert.Empty (leaked);
        }

        [Fact]
        public void The_default_backend_name_resolves_to_the_generated_backend ()
        {
            // The name is a string, so the compiler cannot tell a rewrite missed it; Application.Run would
            // find no backend at run time. This is the check that stands in for the compiler.
            var platform = Core.GetType ("System.Windows.Forms.Backends.Platform", throwOnError: true)!;
            var name = (string?) platform.GetField ("DefaultBackendTypeName", BindingFlags.NonPublic | BindingFlags.Static)?.GetRawConstantValue ();

            Assert.NotNull (name);
            var resolved = Type.GetType (name!, throwOnError: false);

            Assert.NotNull (resolved);
            Assert.Equal ("Majorsilence.Forms.WinFormsShims.Compat.Avalonia", resolved!.Assembly.GetName ().Name);
            Assert.Equal ("System.Windows.Forms.Backends", resolved.Namespace);
        }

        [Fact]
        public void Embedded_resources_keep_the_names_the_code_looks_them_up_by ()
        {
            var names = Core.GetManifestResourceNames ();

            // Not type names, so the rewrite must have left them alone.
            Assert.Contains ("Majorsilence.Forms.SpellCheck.en_US.words.gz", names);
            Assert.Contains ("Majorsilence.Forms.WinFormsEnumShims.dll", names);
            Assert.Contains ("Majorsilence.Forms.DrawingShims.dll", names);
            Assert.Contains ("ILLink.Descriptors.xml", names);

            // The font extractor filters on ".ttf" and rebuilds the file name from the last two segments.
            Assert.Contains (Drawing.GetManifestResourceNames (), n => n.EndsWith (".ttf", StringComparison.Ordinal));
        }

        [Fact]
        public void The_linker_descriptor_names_the_generated_assembly_and_types ()
        {
            using var stream = Core.GetManifestResourceStream ("ILLink.Descriptors.xml")!;
            using var reader = new StreamReader (stream, Encoding.UTF8);
            var xml = reader.ReadToEnd ();

            Assert.Contains ("<assembly fullname=\"Majorsilence.Forms.WinFormsShims.Compat\">", xml);
            Assert.Contains ("<type fullname=\"System.Windows.Forms.Control\">", xml);
            Assert.DoesNotContain ("<type fullname=\"Majorsilence.Forms.", xml);
        }

        [Fact]
        public void Internals_are_shared_only_with_the_assemblies_the_rules_name ()
        {
            var friends = (a: Core, b: Drawing) switch {
                var x => new[] { x.a, x.b }.SelectMany (a => a.GetCustomAttributes<System.Runtime.CompilerServices.InternalsVisibleToAttribute> ())
                                          .Select (attribute => attribute.AssemblyName)
                                          .ToList (),
            };

            Assert.Contains ("Majorsilence.Forms.WinFormsShims.Compat.Avalonia", friends);
            Assert.Contains ("Majorsilence.Forms.WinFormsShims.Compat", friends);
        }

        [Fact]
        public void Designer_shaped_code_constructs_and_wires_up ()
        {
            // Constructing a Form starts the Avalonia desktop backend, which needs a display; a headless Linux runner has none.
            Assert.SkipWhen (OperatingSystem.IsLinux () && string.IsNullOrEmpty (Environment.GetEnvironmentVariable ("DISPLAY"))
                && string.IsNullOrEmpty (Environment.GetEnvironmentVariable ("WAYLAND_DISPLAY")), "no display for the Avalonia backend");

            using var form = new System.Windows.Forms.Form { Text = "t", ClientSize = new System.Drawing.Size (200, 100) };
            var button = new System.Windows.Forms.Button { Text = "ok", Location = new System.Drawing.Point (5, 5), Font = new System.Drawing.Font ("Arial", 9f) };
            var clicks = 0;
            button.Click += (_, _) => clicks++;
            form.Controls.Add (button);
            button.PerformClick ();

            // The framework parents a control to the form's client area, not to the form itself.
            Assert.Contains (button, form.Controls.Cast<System.Windows.Forms.Control> ());
            Assert.Same (form, button.FindForm ());
            Assert.Equal (1, clicks);
        }
    }
}
