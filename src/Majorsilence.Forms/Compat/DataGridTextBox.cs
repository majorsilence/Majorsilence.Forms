using System;
using System.Drawing;
using Majorsilence.Forms;

namespace Majorsilence.Forms
{
    public class DataGridTextBox : DataGridViewTextBoxCell
    {
        public string Text { get => this.Value?.ToString() ; set => this.Value = value; }

        public Control Parent { get; set; }
        public Point Location { get; set; }
        public new bool Visible { get; set; }
        public int MaxLength { get; set; }

        public event EventHandler TextChanged;
        public event EventHandler KeyPress;
    }
}
