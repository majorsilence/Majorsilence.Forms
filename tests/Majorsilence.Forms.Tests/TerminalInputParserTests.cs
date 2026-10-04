using System.Collections.Generic;
using System.Linq;
using System.Text;
using Majorsilence.Forms.Terminal;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The terminal host's input decoder: bytes a terminal sends for keys, mouse and paste. Pure, so each
    // test feeds literal byte strings and asserts on the decoded events.
    public class TerminalInputParserTests
    {
        private static List<TerminalInput> Parse (string text)
        {
            var parser = new TerminalInputParser ();
            var events = new List<TerminalInput> ();
            parser.Feed (Encoding.UTF8.GetBytes (text), events);
            return events;
        }

        private static TerminalInput One (string text)
        {
            var events = Parse (text);
            return Assert.Single (events);
        }

        [Fact]
        public void LowerCaseLetterIsItsKeyAndItsText ()
        {
            var e = One ("a");

            Assert.Equal (TerminalInputKind.Key, e.Kind);
            Assert.Equal (Keys.A, e.Key);
            Assert.Equal ("a", e.Text);
        }

        [Fact]
        public void UpperCaseLetterAddsShift ()
        {
            var e = One ("Q");

            Assert.Equal (Keys.Q | Keys.Shift, e.Key);
            Assert.Equal ("Q", e.Text);
        }

        [Fact]
        public void DigitsAndSpaceMapToTheirKeys ()
        {
            Assert.Equal (Keys.D7, One ("7").Key);
            Assert.Equal (Keys.Space, One (" ").Key);
        }

        [Fact]
        public void SymbolHasTextButNoKey ()
        {
            var e = One ("!");

            Assert.Equal (Keys.None, e.Key);
            Assert.Equal ("!", e.Text);
        }

        [Fact]
        public void MultiByteCharacterIsOneEventWithTheWholeCharacter ()
        {
            Assert.Equal ("é", One ("é").Text);
            Assert.Equal ("😀", One ("😀").Text);
        }

        [Fact]
        public void CharacterSplitAcrossReadsWaitsForItsRemainingBytes ()
        {
            var bytes = Encoding.UTF8.GetBytes ("é");
            var parser = new TerminalInputParser ();
            var events = new List<TerminalInput> ();

            parser.Feed (bytes.AsSpan (0, 1), events);
            Assert.Empty (events);
            Assert.True (parser.HasPending);

            parser.Feed (bytes.AsSpan (1), events);
            Assert.Equal ("é", Assert.Single (events).Text);
            Assert.False (parser.HasPending);
        }

        [Theory]
        [InlineData ("\r", Keys.Return)]
        [InlineData ("\n", Keys.Return)]
        [InlineData ("\t", Keys.Tab)]
        [InlineData ("\u007f", Keys.Back)]
        [InlineData ("\b", Keys.Back)]
        public void ControlCharactersMapToEditingKeys (string input, Keys expected)
        {
            var e = One (input);

            Assert.Equal (expected, e.Key);
            Assert.Null (e.Text);   // Enter/Tab/Backspace do not also type a character
        }

        [Fact]
        public void CtrlLetterIsControlPlusTheLetter ()
        {
            Assert.Equal (Keys.Control | Keys.C, One ("\u0003").Key);
            Assert.Equal (Keys.Control | Keys.A, One ("\u0001").Key);
            Assert.Equal (Keys.Control | Keys.Space, One ("\u0000").Key);
        }

        [Theory]
        [InlineData ("\u001b[A", Keys.Up)]
        [InlineData ("\u001b[B", Keys.Down)]
        [InlineData ("\u001b[C", Keys.Right)]
        [InlineData ("\u001b[D", Keys.Left)]
        [InlineData ("\u001b[H", Keys.Home)]
        [InlineData ("\u001b[F", Keys.End)]
        [InlineData ("\u001bOA", Keys.Up)]   // application cursor mode
        [InlineData ("\u001bOP", Keys.F1)]
        [InlineData ("\u001b[2~", Keys.Insert)]
        [InlineData ("\u001b[3~", Keys.Delete)]
        [InlineData ("\u001b[5~", Keys.PageUp)]
        [InlineData ("\u001b[6~", Keys.PageDown)]
        [InlineData ("\u001b[15~", Keys.F5)]
        [InlineData ("\u001b[24~", Keys.F12)]
        public void NavigationAndFunctionKeys (string input, Keys expected)
            => Assert.Equal (expected, One (input).Key);

        [Fact]
        public void ModifierParameterAddsShiftAltControl ()
        {
            Assert.Equal (Keys.Up | Keys.Shift, One ("\u001b[1;2A").Key);
            Assert.Equal (Keys.Left | Keys.Alt, One ("\u001b[1;3D").Key);
            Assert.Equal (Keys.Right | Keys.Control, One ("\u001b[1;5C").Key);
            Assert.Equal (Keys.Delete | Keys.Control | Keys.Shift, One ("\u001b[3;6~").Key);
        }

        [Fact]
        public void BackTabIsShiftTab ()
            => Assert.Equal (Keys.Tab | Keys.Shift, One ("\u001b[Z").Key);

        [Fact]
        public void EscapeThenCharacterIsAltCharacterAndTypesNothing ()
        {
            var e = One ("\u001bx");

            Assert.Equal (Keys.X | Keys.Alt, e.Key);
            Assert.Null (e.Text);
        }

        [Fact]
        public void UnknownSequenceIsSwallowedNotTyped ()
            => Assert.Empty (Parse ("\u001b[99;99;99X"));

        [Fact]
        public void LoneEscapeWaitsThenFlushesAsTheEscapeKey ()
        {
            var parser = new TerminalInputParser ();
            var events = new List<TerminalInput> ();
            parser.Feed (new byte[] { 0x1B }, events);

            Assert.Empty (events);
            Assert.True (parser.HasPending);

            parser.Flush (events);
            Assert.Equal (Keys.Escape, Assert.Single (events).Key);
            Assert.False (parser.HasPending);
        }

        [Fact]
        public void SequenceSplitAcrossReadsIsNotMistakenForEscape ()
        {
            var parser = new TerminalInputParser ();
            var events = new List<TerminalInput> ();
            parser.Feed (new byte[] { 0x1B }, events);
            parser.Feed (new byte[] { (byte) '[' }, events);
            parser.Feed (new byte[] { (byte) 'A' }, events);

            Assert.Equal (Keys.Up, Assert.Single (events).Key);
        }

        [Fact]
        public void TruncatedSequenceIsDroppedOnFlush ()
        {
            var parser = new TerminalInputParser ();
            var events = new List<TerminalInput> ();
            parser.Feed (Encoding.ASCII.GetBytes ("\u001b[1;"), events);
            parser.Flush (events);

            Assert.Empty (events);
            Assert.False (parser.HasPending);
        }

        [Fact]
        public void SeveralEventsInOneReadComeOutInOrder ()
        {
            var events = Parse ("hi\u001b[A\r");

            Assert.Equal (new[] { Keys.H, Keys.I, Keys.Up, Keys.Return }, events.Select (e => e.Key));
        }

        [Fact]
        public void MousePressIsOneBasedOnTheWireAndZeroBasedHere ()
        {
            var e = One ("\u001b[<0;10;5M");

            Assert.Equal (TerminalInputKind.MouseDown, e.Kind);
            Assert.Equal (MouseButtons.Left, e.Button);
            Assert.Equal (9, e.Col);
            Assert.Equal (4, e.Row);
        }

        [Fact]
        public void MouseReleaseUsesLowerCaseM ()
        {
            var e = One ("\u001b[<2;3;3m");

            Assert.Equal (TerminalInputKind.MouseUp, e.Kind);
            Assert.Equal (MouseButtons.Right, e.Button);
        }

        [Fact]
        public void DragReportsMoveWithTheButtonHeld ()
        {
            var e = One ("\u001b[<32;7;8M");   // 32 = motion, 0 = left

            Assert.Equal (TerminalInputKind.MouseMove, e.Kind);
            Assert.Equal (MouseButtons.Left, e.Button);
        }

        [Fact]
        public void HoverReportsMoveWithNoButton ()
        {
            var e = One ("\u001b[<35;7;8M");   // 32 + 3 = motion, no button

            Assert.Equal (TerminalInputKind.MouseMove, e.Kind);
            Assert.Equal (MouseButtons.None, e.Button);
        }

        [Fact]
        public void WheelIsOneNotchPerReportAndSignedLikeWinForms ()
        {
            var up = One ("\u001b[<64;1;1M");
            var down = One ("\u001b[<65;1;1M");

            Assert.Equal (120, up.WheelY);
            Assert.Equal (-120, down.WheelY);
            Assert.Equal (TerminalInputKind.Wheel, up.Kind);
        }

        [Fact]
        public void MouseModifiersAreCarriedOn ()
        {
            var e = One ("\u001b[<20;1;1M");   // 16 ctrl + 4 shift + left

            Assert.Equal (Keys.Control | Keys.Shift, e.Key);
        }

        [Fact]
        public void BracketedPasteIsOneTextEventEvenWithControlCharacters ()
        {
            var e = One ("\u001b[200~line1\nline2\u001b[A\u001b[201~");

            Assert.Equal (TerminalInputKind.Text, e.Kind);
            Assert.Equal ("line1\nline2\u001b[A", e.Text);   // nothing inside a paste is interpreted
        }

        [Fact]
        public void PasteArrivingInPiecesWaitsForItsEnd ()
        {
            var parser = new TerminalInputParser ();
            var events = new List<TerminalInput> ();
            parser.Feed (Encoding.UTF8.GetBytes ("\u001b[200~hel"), events);
            Assert.Empty (events);

            parser.Feed (Encoding.UTF8.GetBytes ("lo\u001b[201~x"), events);
            Assert.Equal ("hello", events[0].Text);
            Assert.Equal (Keys.X, events[1].Key);
        }
    }
}
