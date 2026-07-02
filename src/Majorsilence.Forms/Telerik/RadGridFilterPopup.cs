using System.Drawing;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// The per-column filter popup shown when the user clicks a column's funnel glyph. Offers an
    /// Excel-style distinct-value checklist plus a single condition (operator + value), mirroring the
    /// RadGridView composite filter popup. Calls back with the resulting <see cref="FilterDescriptor"/>
    /// (or null to clear the filter).
    /// </summary>
    internal static class RadGridFilterPopup
    {
        // Operator entries shown in the condition combo (label, operator).
        private static readonly (string Label, FilterOperator Op)[] Operators = [
            ("No condition", FilterOperator.None),
            ("Contains", FilterOperator.Contains),
            ("Does not contain", FilterOperator.NotContains),
            ("Equals", FilterOperator.IsEqualTo),
            ("Not equal to", FilterOperator.IsNotEqualTo),
            ("Starts with", FilterOperator.StartsWith),
            ("Ends with", FilterOperator.EndsWith),
            ("Greater than", FilterOperator.IsGreaterThan),
            ("Less than", FilterOperator.IsLessThan),
            ("Is empty", FilterOperator.IsNull),
            ("Is not empty", FilterOperator.IsNotNull),
        ];

        private const string BlanksLabel = "(Blanks)";
        private const int MaxChecklistValues = 14;

        public static void Show (RadGridView grid, string columnName, List<string> distinctValues,
            FilterDescriptor? current, Point screenLocation, Action<FilterDescriptor?> onApply)
        {
            if (grid.FindWindow () is not WindowBase window)
                return;

            const int width = 250;
            const int margin = 8;
            const int rowH = 26;
            const int checkH = 22;

            var popup = new PopupWindow (window);
            var y = margin;

            popup.Controls.Add (new Label {
                Text = $"Filter \"{columnName}\"",
                Left = margin, Top = y, Width = width - margin * 2, Height = 18
            });
            y += 22;

            // Condition: operator + value.
            var operatorCombo = new ComboBox {
                Left = margin, Top = y, Width = width - margin * 2, Height = 24,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (var entry in Operators)
                operatorCombo.Items.Add (entry.Label, false);
            operatorCombo.SelectedIndex = current is null
                ? 0 : Math.Max (0, Array.FindIndex (Operators, o => o.Op == current.Operator));
            popup.Controls.Add (operatorCombo);
            y += 30;

            var valueBox = new TextBox {
                Left = margin, Top = y, Width = width - margin * 2, Height = 24,
                Text = current?.Value?.ToString () ?? string.Empty
            };
            popup.Controls.Add (valueBox);
            y += 30;

            // Second condition: And/Or + operator + value.
            var combineCombo = new ComboBox {
                Left = margin, Top = y, Width = 64, Height = 24, DropDownStyle = ComboBoxStyle.DropDownList
            };
            combineCombo.Items.Add ("And", false);
            combineCombo.Items.Add ("Or", false);
            combineCombo.SelectedIndex = current?.CombineWithOr == true ? 1 : 0;
            popup.Controls.Add (combineCombo);

            var secondOperatorCombo = new ComboBox {
                Left = combineCombo.Right + 4, Top = y, Width = width - margin * 2 - 68, Height = 24,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (var entry in Operators)
                secondOperatorCombo.Items.Add (entry.Label, false);
            secondOperatorCombo.SelectedIndex = current is null
                ? 0 : Math.Max (0, Array.FindIndex (Operators, o => o.Op == current.SecondOperator));
            popup.Controls.Add (secondOperatorCombo);
            y += 30;

            var secondValueBox = new TextBox {
                Left = margin, Top = y, Width = width - margin * 2, Height = 24,
                Text = current?.SecondValue?.ToString () ?? string.Empty
            };
            popup.Controls.Add (secondValueBox);
            y += 32;

            // Distinct-value checklist (skipped when there are too many distinct values).
            var valueChecks = new List<CheckBox> ();
            var showChecklist = distinctValues.Count > 0 && distinctValues.Count <= MaxChecklistValues;

            if (showChecklist) {
                var selectAll = new CheckBox {
                    Text = "(Select All)",
                    Left = margin, Top = y, Width = width - margin * 2, Height = checkH,
                    Checked = true
                };
                popup.Controls.Add (selectAll);
                y += checkH;

                foreach (var value in distinctValues) {
                    var isChecked = current?.SelectedValues is null || current.SelectedValues.Contains (value);
                    var box = new CheckBox {
                        Text = string.IsNullOrEmpty (value) ? BlanksLabel : value,
                        Tag = value,
                        Left = margin + 14, Top = y, Width = width - margin * 2 - 14, Height = checkH,
                        Checked = isChecked
                    };
                    valueChecks.Add (box);
                    popup.Controls.Add (box);
                    y += checkH;
                }

                selectAll.Checked = valueChecks.All (c => c.Checked);
                selectAll.CheckedChanged += (_, _) => {
                    foreach (var box in valueChecks)
                        box.Checked = selectAll.Checked;
                };
            } else if (distinctValues.Count > MaxChecklistValues) {
                popup.Controls.Add (new Label {
                    Text = $"{distinctValues.Count} distinct values — use a condition above.",
                    Left = margin, Top = y, Width = width - margin * 2, Height = 32
                });
                y += 34;
            }

            y += 4;

            // Apply / Clear buttons.
            var applyButton = new Button {
                Text = "Apply", Left = margin, Top = y, Width = (width - margin * 3) / 2, Height = rowH
            };
            var clearButton = new Button {
                Text = "Clear", Left = applyButton.Right + margin, Top = y, Width = (width - margin * 3) / 2, Height = rowH
            };
            popup.Controls.Add (applyButton);
            popup.Controls.Add (clearButton);
            y += rowH + margin;

            applyButton.Click += (_, _) => {
                onApply (BuildDescriptor (operatorCombo.SelectedIndex, valueBox.Text,
                    secondOperatorCombo.SelectedIndex, secondValueBox.Text, combineCombo.SelectedIndex == 1, valueChecks));
                popup.Hide ();
            };
            clearButton.Click += (_, _) => {
                onApply (null);
                popup.Hide ();
            };

            popup.Size = new Size (width, y);
            popup.Show (screenLocation);
        }

        private static FilterDescriptor? BuildDescriptor (int operatorIndex, string value,
            int secondOperatorIndex, string secondValue, bool combineWithOr, List<CheckBox> valueChecks)
        {
            HashSet<string>? selected = null;

            if (valueChecks.Count > 0 && !valueChecks.All (c => c.Checked))
                selected = new HashSet<string> (
                    valueChecks.Where (c => c.Checked).Select (c => c.Tag as string ?? string.Empty),
                    StringComparer.CurrentCultureIgnoreCase);

            var op = operatorIndex >= 0 && operatorIndex < Operators.Length ? Operators[operatorIndex].Op : FilterOperator.None;
            var op2 = secondOperatorIndex >= 0 && secondOperatorIndex < Operators.Length ? Operators[secondOperatorIndex].Op : FilterOperator.None;

            // Nothing selected to filter on → clear.
            if (selected is null && op == FilterOperator.None && op2 == FilterOperator.None)
                return null;

            return new FilterDescriptor {
                Operator = op,
                Value = string.IsNullOrEmpty (value) ? null : value,
                SecondOperator = op2,
                SecondValue = string.IsNullOrEmpty (secondValue) ? null : secondValue,
                CombineWithOr = combineWithOr,
                SelectedValues = selected
            };
        }
    }
}
