using Majorsilence.Forms.Backends;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    public class TouchKeyboardGateTests
    {
        [Fact]
        public void RequestWhileATouchIsPending_IsHeldUntilTheTap ()
        {
            var gate = new TouchKeyboardGate ();

            Assert.False (gate.Request (true, TextInputKind.Normal, touchPending: true));
            Assert.Equal (TextInputKind.Normal, gate.Tap ());
            Assert.Null (gate.Tap ());
        }

        [Fact]
        public void ADragDropsTheHeldRequest ()
        {
            var gate = new TouchKeyboardGate ();
            gate.Request (true, TextInputKind.Email, touchPending: true);

            gate.Drag ();

            Assert.Null (gate.Tap ());
        }

        [Fact]
        public void RequestWithNoTouchPending_AppliesAtOnce ()
        {
            var gate = new TouchKeyboardGate ();

            Assert.True (gate.Request (true, TextInputKind.Normal, touchPending: false));
            Assert.False (gate.IsHolding);
        }

        [Fact]
        public void HidingTheKeyboard_IsNeverHeldAndClearsAHeldRequest ()
        {
            var gate = new TouchKeyboardGate ();
            gate.Request (true, TextInputKind.Normal, touchPending: true);

            Assert.True (gate.Request (false, TextInputKind.Normal, touchPending: true));
            Assert.Null (gate.Tap ());
        }
    }
}
