using System.ComponentModel;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// Designer compatibility: real Telerik's RadControl base implements
    /// <see cref="ISupportInitialize"/>, so designer-generated InitializeComponent code brackets
    /// every Rad control with an unconditional ((ISupportInitialize)control).BeginInit()/EndInit()
    /// cast. Implementing this interface gives a compat control those members as no-ops via default
    /// interface methods -- add it to the class's base list and nothing else is required. A control
    /// that genuinely needs batched initialization overrides by implementing the members directly
    /// (class members take precedence over these defaults).
    /// </summary>
    public interface ISupportInitializeCompat : ISupportInitialize
    {
        void ISupportInitialize.BeginInit () { }
        void ISupportInitialize.EndInit () { }
    }
}
