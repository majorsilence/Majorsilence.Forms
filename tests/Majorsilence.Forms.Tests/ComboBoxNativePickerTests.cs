using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // On a platform with its own item picker (Android's list dialog) a ComboBox hands the choice to it instead of opening its
    // small popup (#438). Headless stands in for that platform through HeadlessPlatformBackend.ItemPicker.
    [Collection ("Headless")]
    public class ComboBoxNativePickerTests : IDisposable
    {
        private readonly HeadlessPlatformBackend backend;

        public ComboBoxNativePickerTests ()
        {
            HeadlessRenderer.Use ();
            backend = (HeadlessPlatformBackend) Platform.Backend;
            backend.ItemPicker = null;
        }

        public void Dispose ()
        {
            backend.ItemPicker = null;
            GC.SuppressFinalize (this);
        }

        private sealed record Request (string? Title, IReadOnlyList<string> Items, int Selected, Action<int> Completed);

        private static (Form Form, ComboBox Combo) Build (ComboBoxStyle style = ComboBoxStyle.DropDownList)
        {
            var form = new Form { ClientSize = new Size (300, 200), FormBorderStyle = FormBorderStyle.None };
            var combo = new ComboBox { Location = new Point (10, 10), Size = new Size (200, 28), DropDownStyle = style };
            combo.Items.AddRange (["Sky", "Coral", "Mint"]);
            combo.SelectedIndex = 0;
            form.Controls.Add (combo);
            form.Show ();
            return (form, combo);
        }

        private List<Request> Answer (bool shows = true)
        {
            var requests = new List<Request> ();
            backend.ItemPicker = (title, items, selected, completed) => {
                requests.Add (new Request (title, items, selected, completed));
                return shows;
            };
            return requests;
        }

        [Fact]
        public void ATapAsksTheBackendForItsPickerWithTheItemsAndTheCurrentChoice ()
        {
            var requests = Answer ();
            var (form, combo) = Build ();
            using var _ = form;

            var popups = PopupWindow.ShownPopups.Length;
            HeadlessRenderer.Click (form, 100, 24);

            var request = Assert.Single (requests);
            Assert.Equal (["Sky", "Coral", "Mint"], request.Items);
            Assert.Equal (0, request.Selected);
            Assert.Equal (popups, PopupWindow.ShownPopups.Length);      // the small drop-down is not also opened
            Assert.True (combo.DroppedDown);             // the list is showing, so a second tap must not ask again
        }

        [Fact]
        public void AChoiceSetsTheSelectionAndRaisesTheUserCommitEvents ()
        {
            var requests = Answer ();
            var (form, combo) = Build ();
            using var _ = form;
            var changed = 0;
            var committed = 0;
            var closed = 0;
            combo.SelectedIndexChanged += (_, _) => changed++;
            combo.SelectionChangeCommitted += (_, _) => committed++;
            combo.DropDownClosed += (_, _) => closed++;

            combo.DroppedDown = true;
            requests[0].Completed (2);

            Assert.Equal (2, combo.SelectedIndex);
            Assert.Equal ("Mint", combo.Text);
            Assert.Equal ((1, 1, 1), (changed, committed, closed));
            Assert.False (combo.DroppedDown);
        }

        [Fact]
        public void DismissingThePickerLeavesTheSelectionAndStillClosesTheDropDown ()
        {
            var requests = Answer ();
            var (form, combo) = Build ();
            using var _ = form;
            var changed = 0;
            var committed = 0;
            var closed = 0;
            combo.SelectedIndexChanged += (_, _) => changed++;
            combo.SelectionChangeCommitted += (_, _) => committed++;
            combo.DropDownClosed += (_, _) => closed++;

            combo.DroppedDown = true;
            requests[0].Completed (-1);

            Assert.Equal (0, combo.SelectedIndex);
            Assert.Equal ((0, 0, 1), (changed, committed, closed));
            Assert.False (combo.DroppedDown);
        }

        [Fact]
        public void LosingFocusWhilePickerShowsDoesNotCloseItOrReportAChoice ()
        {
            var requests = Answer ();
            var (form, combo) = Build ();
            using var _ = form;
            var closed = 0;
            combo.DropDownClosed += (_, _) => closed++;

            combo.DroppedDown = true;
            combo.DroppedDown = false;       // what OnDeselected does when the platform dialog takes the focus

            Assert.True (combo.DroppedDown);
            Assert.Equal (0, closed);

            requests[0].Completed (1);

            Assert.Equal (1, combo.SelectedIndex);
            Assert.Equal (1, closed);
        }

        [Fact]
        public void WithoutANativePickerTheComboOpensItsOwnPopup ()
        {
            var (form, combo) = Build ();
            using var _ = form;

            var popups = PopupWindow.ShownPopups.Length;

            combo.DroppedDown = true;

            Assert.True (combo.DroppedDown);
            Assert.Equal (popups + 1, PopupWindow.ShownPopups.Length);
        }

        [Fact]
        public void APickerThatCannotBeShownFallsBackToThePopup ()
        {
            var requests = Answer (shows: false);
            var (form, combo) = Build ();
            using var _ = form;

            var popups = PopupWindow.ShownPopups.Length;

            combo.DroppedDown = true;

            Assert.Single (requests);
            Assert.Equal (popups + 1, PopupWindow.ShownPopups.Length);
        }

        [Fact]
        public void AnEditableComboKeepsItsPopupBecauseItsTextBoxIsPartOfTheControl ()
        {
            var requests = Answer ();
            var (form, combo) = Build (ComboBoxStyle.DropDown);
            using var _ = form;

            var popups = PopupWindow.ShownPopups.Length;

            combo.DroppedDown = true;

            Assert.Empty (requests);
            Assert.Equal (popups + 1, PopupWindow.ShownPopups.Length);
        }

        [Fact]
        public void AnEmptyComboDoesNotShowAnEmptyPicker ()
        {
            var requests = Answer ();
            var form = new Form { ClientSize = new Size (300, 200), FormBorderStyle = FormBorderStyle.None };
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            form.Controls.Add (combo);
            form.Show ();
            using var _ = form;

            combo.DroppedDown = true;

            Assert.Empty (requests);
        }
    }
}
