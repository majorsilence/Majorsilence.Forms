using Xunit;

namespace Majorsilence.Forms.Tests;

// Exercises ComputerInfo — the backing type for classic VB's My.Computer.Name, confirmed against the
// one real usage found in Financial (MainLoginWinformsView.vb: My.Computer.Name.ToUpper()).
public class ComputerInfoTests
{
    [Fact]
    public void Name_forwards_to_Environment_MachineName ()
    {
        var info = new ComputerInfo ();
        Assert.Equal (Environment.MachineName, info.Name);
    }

    [Fact]
    public void Name_supports_ToUpper_call_shape ()
    {
        // My.Computer.Name.ToUpper() is the real-world call shape (MainLoginWinformsView.vb) — this only
        // compiles/behaves correctly if Name returns a plain String (VB property access, not a method).
        var info = new ComputerInfo ();
        var upper = info.Name.ToUpperInvariant ();
        Assert.Equal (info.Name.Length, upper.Length);
    }
}
