using System.Linq;
using System.Reflection;
using Xunit;

using Point = System.Drawing.Point;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Covers the event-data classes, handler delegates and enums generated for
    /// docs/winforms-gap-plan.md item 2.
    ///
    /// The thing that was broken is that migrated code did not <em>compile</em>: a control declaring
    /// <c>public event DataGridViewCellEventHandler CellClick</c> needs the delegate to exist, and
    /// designer files wire handlers by delegate type. So these are mostly usage tests — they subscribe,
    /// raise, and read the payload the way generated code does, which is the behaviour that matters.
    /// </summary>
    public class WinFormsEventTypeTests
    {
        private event ColumnWidthChangedEventHandler? ColumnWidthChanged;

        [Fact]
        public void A_generated_delegate_can_be_declared_subscribed_and_raised ()
        {
            ColumnWidthChangedEventArgs? received = null;
            ColumnWidthChanged += (_, e) => received = e;

            ColumnWidthChanged?.Invoke (this, new ColumnWidthChangedEventArgs (3));

            Assert.NotNull (received);
            Assert.Equal (3, received!.ColumnIndex);
        }

        [Fact]
        public void Generated_event_args_carry_their_constructor_values ()
        {
            var reordered = new ColumnReorderedEventArgs (1, 4, null!);
            Assert.Equal (1, reordered.OldDisplayIndex);
            Assert.Equal (4, reordered.NewDisplayIndex);

            var cacheVirtual = new CacheVirtualItemsEventArgs (10, 20);
            Assert.Equal (10, cacheVirtual.StartIndex);
            Assert.Equal (20, cacheVirtual.EndIndex);
        }

        [Fact]
        public void Cancelable_event_args_derive_from_CancelEventArgs ()
        {
            // Handlers written against System.Drawing set e.Cancel; that only works if the base is right.
            var e = new ColumnWidthChangingEventArgs (2, 50, false);
            Assert.IsAssignableFrom<System.ComponentModel.CancelEventArgs> (e);

            e.Cancel = true;
            Assert.True (e.Cancel);
        }

        [Fact]
        public void Generated_enums_carry_upstream_values ()
        {
            // Generated from the reference assembly, so these are the real numbers rather than a
            // sequence invented here -- the defect item 1 of the plan existed to fix. These are the
            // Win32 arrow-key virtual key codes, not 0..3; this assertion was written as 0..3 from
            // memory first and the generated values turned out to be the correct ones.
            Assert.Equal (37, (int)SearchDirectionHint.Left);
            Assert.Equal (38, (int)SearchDirectionHint.Up);
            Assert.Equal (39, (int)SearchDirectionHint.Right);
            Assert.Equal (40, (int)SearchDirectionHint.Down);
        }

        [Fact]
        public void Every_generated_delegate_has_the_conventional_event_shape ()
        {
            // A delegate that does not take (object, EventArgs-derived) cannot be wired by a designer
            // file, which is the whole reason these exist.
            var assembly = typeof (Control).Assembly;
            var handlers = assembly.GetExportedTypes()
                .Where (t => t.Name.EndsWith ("EventHandler", System.StringComparison.Ordinal) && typeof (System.Delegate).IsAssignableFrom (t))
                .ToArray ();

            Assert.True (handlers.Length > 80, $"expected the generated handlers to be present, found {handlers.Length}");

            foreach (var handler in handlers)
            {
                var invoke = handler.GetMethod ("Invoke")!;
                var parameters = invoke.GetParameters ();
                Assert.Equal (2, parameters.Length);
                Assert.Equal (typeof (object), parameters[0].ParameterType);
                Assert.True (typeof (System.EventArgs).IsAssignableFrom (parameters[1].ParameterType),
                    $"{handler.Name}'s second parameter should be EventArgs-derived, was {parameters[1].ParameterType.Name}");
            }
        }

        [Fact]
        public void Generated_event_args_all_derive_from_EventArgs ()
        {
            var assembly = typeof (Control).Assembly;
            var argTypes = assembly.GetExportedTypes ()
                .Where (t => t.Name.EndsWith ("EventArgs", System.StringComparison.Ordinal) && t.IsClass)
                .ToArray ();

            Assert.True (argTypes.Length > 50, $"expected the generated event args to be present, found {argTypes.Length}");
            Assert.All (argTypes, t => Assert.True (typeof (System.EventArgs).IsAssignableFrom (t), $"{t.Name} is not EventArgs-derived"));
        }
    }
}
