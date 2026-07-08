using System.ComponentModel;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Real WinForms implements ISupportInitialize on exactly these controls, so the designer brackets
    // their InitializeComponent property assignments with an unconditional
    // ((ISupportInitialize)ctrl).BeginInit()/EndInit() cast. Every one must satisfy that cast or a
    // migrated form throws InvalidCastException the moment its designer code runs (TrackBar was the
    // gap, found opening frmMaintainCustomer in a real migrated app; PictureBox/SplitContainer were
    // earlier gaps). Pin the whole set so a newly-added control can't silently regress.
    public class CoreSupportInitializeTests
    {
        [Theory]
        [InlineData (typeof (TrackBar))]
        [InlineData (typeof (NumericUpDown))]
        [InlineData (typeof (PictureBox))]
        [InlineData (typeof (SplitContainer))]
        [InlineData (typeof (DataGridView))]
        public void Designer_bracketed_control_supports_initialize (System.Type type)
        {
            Assert.True (typeof (ISupportInitialize).IsAssignableFrom (type),
                $"{type.Name} must implement ISupportInitialize (designer code casts to it unconditionally).");

            var instance = (ISupportInitialize) System.Activator.CreateInstance (type)!;
            instance.BeginInit ();
            instance.EndInit ();
            (instance as System.IDisposable)?.Dispose ();
        }
    }
}
