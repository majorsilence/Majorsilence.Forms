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
#if !NETSTANDARD2_0
        void ISupportInitialize.BeginInit () { }
        void ISupportInitialize.EndInit () { }
#endif
        // netstandard2.0: no default interface members. Every implementer derives from
        // Majorsilence.Forms.Control, which supplies the no-op ISupportInitialize implementation for
        // that TFM (see Control.Compat.cs).
    }
}
