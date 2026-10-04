using System;
using System.Collections.Generic;
using System.Text;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Terminal;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Asking the terminal what it supports, and the Kitty keyboard protocol it may answer yes to: the
    // parser's decoding of the replies and of CSI-u key events, the probe's decision, and what the backend
    // does with the answers. The backend is built directly (never installed as the platform) with an
    // injected environment, so none of this depends on the terminal the tests happen to run in.
    [Collection ("Headless")]
    public class TerminalProbeAndKeyboardTests
    {
        public TerminalProbeAndKeyboardTests () => HeadlessRenderer.Use ();

        private static List<TerminalInput> Parse (string text)
        {
            var parser = new TerminalInputParser ();
            var events = new List<TerminalInput> ();
            parser.Feed (Encoding.UTF8.GetBytes (text), events);
            return events;
        }

        private static TerminalInput One (string text) => Assert.Single (Parse (text));

        // ── Reply parsing ─────────────────────────────────────────────────────

        [Fact]
        public void DeviceAttributesReplyCarriesItsAttributeList ()
        {
            var e = One ("\u001b[?62;4;22c");

            Assert.Equal (TerminalInputKind.DeviceAttributes, e.Kind);
            Assert.Equal ("62;4;22", e.Text);
        }

        [Fact]
        public void KeyboardFlagsReplyCarriesTheFlags ()
        {
            var e = One ("\u001b[?27u");

            Assert.Equal (TerminalInputKind.KeyboardFlags, e.Kind);
            Assert.Equal (27, e.Col);
        }

        [Theory]
        [InlineData ("\u001b_Gi=31;OK\u001b\\", "OK")]
        [InlineData ("\u001b_Gi=31;OK\u0007", "OK")]                                  // BEL-terminated
        [InlineData ("\u001b_Gi=31;ENOTSUPPORTED:no such thing\u001b\\", "ENOTSUPPORTED:no such thing")]
        public void GraphicsReplyCarriesItsIdAndMessage (string reply, string message)
        {
            var e = One (reply);

            Assert.Equal (TerminalInputKind.GraphicsReply, e.Kind);
            Assert.Equal (31, e.Col);
            Assert.Equal (message, e.Text);
        }

        [Fact]
        public void GraphicsReplySplitAcrossReadsWaitsForItsTerminator ()
        {
            var parser = new TerminalInputParser ();
            var events = new List<TerminalInput> ();
            parser.Feed (Encoding.ASCII.GetBytes ("\u001b_Gi=31;"), events);
            Assert.Empty (events);

            parser.Feed (Encoding.ASCII.GetBytes ("OK\u001b\\"), events);
            Assert.Equal ("OK", Assert.Single (events).Text);
        }

        [Fact]
        public void UnrelatedApplicationCommandIsSwallowedNotTyped ()
            => Assert.Empty (Parse ("\u001b_Xanything at all\u001b\\"));

        [Fact]
        public void RepliesAndKeysInOneReadComeOutInOrder ()
        {
            // What a terminal sends back for the probe, then the user's first key.
            var events = Parse ("\u001b_Gi=31;OK\u001b\\\u001b[?0u\u001b[?62;4c" + "a");

            Assert.Equal (
                new[] { TerminalInputKind.GraphicsReply, TerminalInputKind.KeyboardFlags, TerminalInputKind.DeviceAttributes, TerminalInputKind.Key },
                events.ConvertAll (e => e.Kind));
        }

        [Fact]
        public void SixelGeometryReplyCarriesTheLargestImageSize ()
        {
            var e = One ("\u001b[?2;0;1000;1000S");   // what xterm says with its default 1000x1000 limit

            Assert.Equal (TerminalInputKind.SixelLimit, e.Kind);
            Assert.Equal ((1000, 1000), (e.Col, e.Row));
        }

        [Theory]
        [InlineData ("\u001b[?2;3;0;0S")]       // status 3: the terminal refused
        [InlineData ("\u001b[?1;0;256S")]       // a different item (colour registers), not geometry
        [InlineData ("\u001b[?2;0;0;0S")]       // zero is not a limit
        [InlineData ("\u001b[?2;0S")]           // truncated
        public void OtherGraphicsAttributeRepliesAreIgnored (string reply)
            => Assert.Empty (Parse (reply));

        [Theory]
        [InlineData ("\u001bP>|XTerm(407)\u001b\\", "XTerm(407)")]
        [InlineData ("\u001bP>|WezTerm 20240203-110809-5046fc22\u001b\\", "WezTerm 20240203-110809-5046fc22")]
        [InlineData ("\u001bP>|foot(1.18.1)\u0007", "foot(1.18.1)")]   // BEL-terminated
        public void VersionReplyCarriesTheTerminalsNameAndVersion (string reply, string name)
        {
            var e = One (reply);

            Assert.Equal (TerminalInputKind.TerminalVersion, e.Kind);
            Assert.Equal (name, e.Text);
        }

        [Fact]
        public void VersionReplySplitAcrossReadsWaitsForItsTerminator ()
        {
            var parser = new TerminalInputParser ();
            var events = new List<TerminalInput> ();
            parser.Feed (Encoding.ASCII.GetBytes ("\u001bP>|XTerm("), events);
            Assert.Empty (events);

            parser.Feed (Encoding.ASCII.GetBytes ("407)\u001b\\"), events);
            Assert.Equal ("XTerm(407)", Assert.Single (events).Text);
        }

        [Fact]
        public void AltShiftPIsStillAKeyAndNotTheStartOfAVersionReply ()
        {
            // ESC P is also what Alt+Shift+P sends; only ESC P > | is a reply.
            var alone = One ("\u001bP");
            Assert.Equal (Keys.P | Keys.Shift | Keys.Alt, alone.Key);

            var followed = Parse ("\u001bPx");
            Assert.Equal (Keys.P | Keys.Shift | Keys.Alt, followed[0].Key);
            Assert.Equal (Keys.X, followed[1].Key);
        }

        // ── Kitty key events ──────────────────────────────────────────────────

        [Fact]
        public void PlainKeyCarriesItsAssociatedText ()
        {
            var e = One ("\u001b[97;1;97u");

            Assert.Equal (Keys.A, e.Key);
            Assert.Equal ("a", e.Text);
            Assert.Equal (KeyEventKind.Press, e.Event);
        }

        [Fact]
        public void ShiftedKeyTypesTheShiftedText ()
        {
            var e = One ("\u001b[97:65;2;65u");   // a, shifted A; modifiers 2 = shift

            Assert.Equal (Keys.A | Keys.Shift, e.Key);
            Assert.Equal ("A", e.Text);
        }

        [Fact]
        public void TextIsDerivedWhenTheTerminalSendsNone ()
        {
            Assert.Equal ("a", One ("\u001b[97u").Text);
            Assert.Equal ("A", One ("\u001b[97:65;2u").Text);   // shifted alternate used with shift
        }

        [Fact]
        public void ReleaseAndRepeatAreReportedAndReleaseTypesNothing ()
        {
            var repeat = One ("\u001b[97;1:2;97u");
            var release = One ("\u001b[97;1:3u");

            Assert.Equal (KeyEventKind.Repeat, repeat.Event);
            Assert.Equal ("a", repeat.Text);
            Assert.Equal (KeyEventKind.Release, release.Event);
            Assert.Null (release.Text);
        }

        [Fact]
        public void CtrlLetterIsAKeyNotText ()
        {
            var e = One ("\u001b[99;5u");   // Ctrl+C: modifiers 5 = 1 + ctrl(4)

            Assert.Equal (Keys.C | Keys.Control, e.Key);
            Assert.Null (e.Text);
        }

        [Fact]
        public void AltKeyIsAShortcutNotText ()
        {
            var e = One ("\u001b[120;3u");

            Assert.Equal (Keys.X | Keys.Alt, e.Key);
            Assert.Null (e.Text);
        }

        [Theory]
        [InlineData ("\u001b[27u", Keys.Escape)]   // no lone-ESC ambiguity under this protocol
        [InlineData ("\u001b[13u", Keys.Return)]
        [InlineData ("\u001b[9u", Keys.Tab)]
        [InlineData ("\u001b[127u", Keys.Back)]
        [InlineData ("\u001b[57414u", Keys.Return)]   // keypad Enter
        [InlineData ("\u001b[57399u", Keys.NumPad0)]
        [InlineData ("\u001b[57408u", Keys.NumPad9)]
        [InlineData ("\u001b[57413u", Keys.Add)]
        [InlineData ("\u001b[57376u", Keys.F13)]
        [InlineData ("\u001b[57387u", Keys.F24)]
        [InlineData ("\u001b[57441u", Keys.ShiftKey)]
        [InlineData ("\u001b[57442u", Keys.ControlKey)]
        [InlineData ("\u001b[57443u", Keys.Menu)]
        [InlineData ("\u001b[57358u", Keys.Capital)]
        public void FunctionalKeysMapToTheirKeys (string input, Keys expected)
        {
            var e = One (input);

            Assert.Equal (expected, e.Key);
            Assert.Null (e.Text);   // none of these type a character
        }

        [Fact]
        public void BaseLayoutKeyIsTheKeysIdentityAndTheTextIsWhatItTypes ()
        {
            // A Russian layout: the key labelled "ф" is the physical A key (base layout 97).
            var e = One ("\u001b[1092::97;1;1092u");

            Assert.Equal (Keys.A, e.Key);
            Assert.Equal ("ф", e.Text);
        }

        [Fact]
        public void PunctuationMapsToOemKeysAndTypesItself ()
        {
            var e = One ("\u001b[44;1;44u");

            Assert.Equal (Keys.Oemcomma, e.Key);
            Assert.Equal (",", e.Text);
        }

        [Fact]
        public void MultiCodepointTextIsAssembled ()
        {
            var e = One ("\u001b[97;1;228:229u");

            Assert.Equal ("äå", e.Text);
        }

        [Fact]
        public void ControlCodepointsInTextAreNeverTyped ()
        {
            var e = One ("\u001b[97;1;7:97u");   // BEL then a

            Assert.Equal ("a", e.Text);
        }

        [Fact]
        public void UnknownKeyCodeProducesNothing ()
            => Assert.Empty (Parse ("\u001b[57999u"));

        [Fact]
        public void LegacyKeysCarryEventTypesAndModifiersToo ()
        {
            var release = One ("\u001b[1;1:3A");   // Up, released
            var ctrlRepeat = One ("\u001b[1;5:2C");   // Ctrl+Right, repeating
            var del = One ("\u001b[3;2:1~");

            Assert.Equal ((Keys.Up, KeyEventKind.Release), (release.Key, release.Event));
            Assert.Equal ((Keys.Right | Keys.Control, KeyEventKind.Repeat), (ctrlRepeat.Key, ctrlRepeat.Event));
            Assert.Equal ((Keys.Delete | Keys.Shift, KeyEventKind.Press), (del.Key, del.Event));
        }

        [Fact]
        public void SubParametersDoNotBreakTheModifierParameter ()
        {
            // Before sub-parameters were parsed, "5:3" read as 0 and the modifier was lost.
            Assert.Equal (Keys.Left | Keys.Control, One ("\u001b[1;5:1D").Key);
        }

        // ── The probe ─────────────────────────────────────────────────────────

        private static TerminalProbe Probe (params TerminalInput[] replies)
        {
            var probe = new TerminalProbe ();
            foreach (var r in replies)
                probe.Observe (r);
            return probe;
        }

        private static TerminalInput Graphics (string message, int id = TerminalProbe.GraphicsQueryId) => new (TerminalInputKind.GraphicsReply, Text: message, Col: id);
        private static TerminalInput Keyboard (int flags = 0) => new (TerminalInputKind.KeyboardFlags, Col: flags);
        private static TerminalInput Attributes (string list) => new (TerminalInputKind.DeviceAttributes, Text: list);

        [Fact]
        public void QueryAsksAllThreeQuestionsWithDeviceAttributesLast ()
        {
            var q = TerminalProbe.Query;

            Assert.Contains ("a=q", q);          // can you display Kitty graphics?
            Assert.Contains ("\u001b[?u", q);    // do you speak the Kitty keyboard protocol?
            Assert.True (q.EndsWith ("\u001b[c", StringComparison.Ordinal), "DA1 is the sentinel: it must be asked last");
            Assert.True (q.IndexOf ("a=q", StringComparison.Ordinal) < q.IndexOf ("\u001b[?u", StringComparison.Ordinal));
        }

        [Fact]
        public void ProbeCompletesOnlyWhenDeviceAttributesArrive ()
        {
            Assert.False (Probe (Graphics ("OK"), Keyboard ()).Complete);
            Assert.True (Probe (Graphics ("OK"), Keyboard (), Attributes ("62")).Complete);
        }

        [Fact]
        public void ProbeDecidesKittyThenSixelThenHalfBlocks ()
        {
            Assert.Equal (TerminalGraphicsMode.Kitty, Probe (Graphics ("OK"), Attributes ("62;4")).Decide ());   // Kitty wins over Sixel
            Assert.Equal (TerminalGraphicsMode.Sixel, Probe (Attributes ("62;4;22")).Decide ());
            Assert.Equal (TerminalGraphicsMode.HalfBlock, Probe (Attributes ("62;22")).Decide ());
        }

        [Fact]
        public void SixelIsTheWholeAttributeNotASubstring ()
        {
            // 14 and 41 contain a 4 but are not Sixel.
            Assert.False (Probe (Attributes ("62;14;41")).Sixel);
        }

        [Fact]
        public void AGraphicsErrorOrAnotherIdIsNotSupport ()
        {
            Assert.False (Probe (Graphics ("ENOTSUPPORTED:x"), Attributes ("62")).KittyGraphics);
            Assert.False (Probe (Graphics ("OK", id: 7), Attributes ("62")).KittyGraphics);
        }

        [Fact]
        public void AnyKeyboardFlagsReplyMeansTheProtocolIsImplemented ()
            => Assert.True (Probe (Keyboard (0), Attributes ("62")).KittyKeyboard);

        [Fact]
        public void QueryAsksForTheSixelGeometryBeforeTheSentinel ()
        {
            var q = TerminalProbe.Query;

            Assert.Contains ("\u001b[?2;1S", q);
            Assert.True (q.IndexOf ("\u001b[?2;1S", StringComparison.Ordinal) < q.LastIndexOf ("\u001b[c", StringComparison.Ordinal));
        }

        [Fact]
        public void ProbeRecordsTheSixelLimitOnlyWhenTheTerminalSaidOne ()
        {
            Assert.Null (Probe (Attributes ("62;4")).SixelLimit);
            Assert.Equal ((1000, 800), Probe (new TerminalInput (TerminalInputKind.SixelLimit, Col: 1000, Row: 800), Attributes ("62;4")).SixelLimit);
        }

        [Fact]
        public void ProbeRecordsTheTerminalVersion ()
            => Assert.Equal ("XTerm(407)", Probe (new TerminalInput (TerminalInputKind.TerminalVersion, Text: "XTerm(407)"), Attributes ("62;4")).Version);

        [Fact]
        public void ProbeIgnoresEventsThatAreNotRepliesAndSaysSo ()
        {
            var probe = new TerminalProbe ();

            Assert.False (probe.Observe (new TerminalInput (TerminalInputKind.Key, Keys.A)));
            Assert.True (probe.Observe (Attributes ("62")));
        }

        // ── The backend applying the answers ─────────────────────────────────

        private static Func<string, string?> Env (params (string, string)[] vars)
        {
            var map = new Dictionary<string, string> ();
            foreach (var (k, v) in vars)
                map[k] = v;
            return n => map.TryGetValue (n, out var v) ? v : null;
        }

        [Fact]
        public void TheTerminalsAnswerReplacesTheEnvironmentGuess ()
        {
            // The environment says "foot" (Sixel); the terminal says it does Kitty graphics.
            var backend = new TerminalPlatformBackend (new TerminalOptions (), Env (("TERM", "foot")));
            Assert.Equal (TerminalGraphicsMode.Sixel, backend.GraphicsMode);

            backend.ApplyProbe (Probe (Graphics ("OK"), Attributes ("62;4")));

            Assert.Equal (TerminalGraphicsMode.Kitty, backend.GraphicsMode);
        }

        [Fact]
        public void AnUnlistedSixelTerminalIsFoundByAskingIt ()
        {
            var backend = new TerminalPlatformBackend (new TerminalOptions (), Env ());
            Assert.Equal (TerminalGraphicsMode.HalfBlock, backend.GraphicsMode);

            backend.ApplyProbe (Probe (Attributes ("62;4")));

            Assert.Equal (TerminalGraphicsMode.Sixel, backend.GraphicsMode);
            Assert.Equal ((8, 16), backend.CellPixels);   // the starting guess until the cell-size reply
        }

        [Fact]
        public void ACellSizeLearnedBeforeTheSwitchIsUsedNotTheGuess ()
        {
            // The cell-size reply arrives during the probe, while the guess is still half-blocks; the first
            // frame in pixel mode must already be at the real size (one full frame, not two).
            var backend = new TerminalPlatformBackend (new TerminalOptions (), Env ());
            backend.Dispatch (new TerminalInput (TerminalInputKind.CellSize, Col: 9, Row: 18));
            Assert.Equal ((1, 2), backend.CellPixels);   // still half-blocks: unchanged until the probe decides

            backend.ApplyProbe (Probe (Attributes ("62;4")));

            Assert.Equal ((9, 18), backend.CellPixels);
        }

        private static TerminalPlatformBackend SixelBackend (int cols, int rows)
            => new (new TerminalOptions { GraphicsMode = TerminalGraphicsMode.Sixel }, Env (), cells: (cols, rows));

        [Fact]
        public void SixelAreaIsTheWholeTerminalWhenTheTerminalSetsNoLimit ()
        {
            using var backend = SixelBackend (150, 45);
            backend.Dispatch (new TerminalInput (TerminalInputKind.CellSize, Col: 8, Row: 17));

            Assert.Equal (new System.Drawing.Size (150 * 8, 44 * 17), backend.PixelSize);   // the last row is left unused
        }

        [Fact]
        public void SixelAreaIsCutToWhatTheTerminalWillDrawInWholeCells ()
        {
            // xterm draws at most 1000x1000 by default and clips the rest: a 150x45 terminal of 8x17 cells
            // would otherwise get a 1200 px wide form of which only 1000 px show.
            using var backend = SixelBackend (150, 45);
            backend.Dispatch (new TerminalInput (TerminalInputKind.CellSize, Col: 8, Row: 17));
            var probe = Probe (new TerminalInput (TerminalInputKind.SixelLimit, Col: 1000, Row: 1000), Attributes ("62;4"));

            backend.ApplyProbe (probe);

            Assert.Equal (new System.Drawing.Size (125 * 8, 44 * 17), backend.PixelSize);   // 1000 / 8 = 125 columns; 44 rows * 17 = 748 fits
        }

        private static TerminalProbe XTermProbe (params TerminalInput[] more)
        {
            var probe = new TerminalProbe ();
            probe.Observe (new TerminalInput (TerminalInputKind.TerminalVersion, Text: "XTerm(407)"));
            foreach (var m in more)
                probe.Observe (m);
            probe.Observe (Attributes ("63;4"));
            return probe;
        }

        [Fact]
        public void AnXTermThatDoesNotSayItsLimitIsAssumedToHaveItsDocumentedDefault ()
        {
            // Real xterm 407 never answers the geometry query yet cuts Sixel images off at 1000x1000.
            using var backend = SixelBackend (150, 45);
            backend.Dispatch (new TerminalInput (TerminalInputKind.CellSize, Col: 8, Row: 17));

            backend.ApplyProbe (XTermProbe ());

            Assert.Equal (new System.Drawing.Size (125 * 8, 44 * 17), backend.PixelSize);
        }

        [Fact]
        public void AnotherTerminalWithoutALimitIsNotCut ()
        {
            using var backend = SixelBackend (150, 45);
            backend.Dispatch (new TerminalInput (TerminalInputKind.CellSize, Col: 8, Row: 17));
            var probe = new TerminalProbe ();
            probe.Observe (new TerminalInput (TerminalInputKind.TerminalVersion, Text: "foot(1.18.1)"));
            probe.Observe (Attributes ("62;4"));

            backend.ApplyProbe (probe);

            Assert.Equal (new System.Drawing.Size (150 * 8, 44 * 17), backend.PixelSize);
        }

        [Fact]
        public void WhatAnXTermSaysBeatsTheAssumedDefault ()
        {
            using var backend = SixelBackend (150, 45);
            backend.Dispatch (new TerminalInput (TerminalInputKind.CellSize, Col: 8, Row: 17));

            backend.ApplyProbe (XTermProbe (new TerminalInput (TerminalInputKind.SixelLimit, Col: 4096, Row: 4096)));

            Assert.Equal (new System.Drawing.Size (150 * 8, 44 * 17), backend.PixelSize);   // 4096 allows the whole terminal
        }

        [Fact]
        public void AnExplicitLimitBeatsEverythingForAnXTermWithARaisedResource ()
        {
            // The user raised xterm's maxGraphicsSize; xterm still reports nothing, so they say so.
            using var byOption = new TerminalPlatformBackend (
                new TerminalOptions { GraphicsMode = TerminalGraphicsMode.Sixel, MaxSixelSize = new System.Drawing.Size (2000, 2000) }, Env (), cells: (150, 45));
            byOption.Dispatch (new TerminalInput (TerminalInputKind.CellSize, Col: 8, Row: 17));
            byOption.ApplyProbe (XTermProbe ());
            Assert.Equal (new System.Drawing.Size (150 * 8, 44 * 17), byOption.PixelSize);

            using var byVariable = new TerminalPlatformBackend (
                new TerminalOptions { GraphicsMode = TerminalGraphicsMode.Sixel }, Env (("MF_TERMINAL_SIXEL_MAX", "600x400")), cells: (150, 45));
            byVariable.Dispatch (new TerminalInput (TerminalInputKind.CellSize, Col: 8, Row: 17));
            byVariable.ApplyProbe (XTermProbe ());
            Assert.Equal (new System.Drawing.Size (75 * 8, 23 * 17), byVariable.PixelSize);   // 600/8 columns, 400/17 rows
        }

        [Theory]
        [InlineData ("1000x1000", 1000, 1000)]
        [InlineData ("2048X1024", 2048, 1024)]
        [InlineData (" 800x600 ", 800, 600)]
        public void SixelLimitVariableParsesWidthByHeight (string value, int w, int h)
            => Assert.Equal ((w, h), TerminalCapabilities.ExplicitSixelLimit (Env (("MF_TERMINAL_SIXEL_MAX", value))));

        [Theory]
        [InlineData (null)]
        [InlineData ("")]
        [InlineData ("1000")]
        [InlineData ("0x600")]
        [InlineData ("axb")]
        public void MalformedSixelLimitVariableIsIgnored (string? value)
            => Assert.Null (TerminalCapabilities.ExplicitSixelLimit (Env (("MF_TERMINAL_SIXEL_MAX", value ?? string.Empty))));

        [Fact]
        public void ATallTerminalIsAlsoCutToTheHeightLimit ()
        {
            using var backend = SixelBackend (100, 100);
            backend.Dispatch (new TerminalInput (TerminalInputKind.CellSize, Col: 8, Row: 17));

            backend.ApplyProbe (Probe (new TerminalInput (TerminalInputKind.SixelLimit, Col: 1000, Row: 1000), Attributes ("62;4")));

            Assert.Equal (new System.Drawing.Size (100 * 8, 58 * 17), backend.PixelSize);   // 1000 / 17 = 58 rows
        }

        [Fact]
        public void TheSixelLimitDoesNotShrinkOtherModes ()
        {
            using var backend = new TerminalPlatformBackend (new TerminalOptions { GraphicsMode = TerminalGraphicsMode.Kitty }, Env (), cells: (150, 45));
            backend.Dispatch (new TerminalInput (TerminalInputKind.CellSize, Col: 8, Row: 17));

            backend.ApplyProbe (Probe (new TerminalInput (TerminalInputKind.SixelLimit, Col: 1000, Row: 1000), Attributes ("62;4")));

            Assert.Equal (new System.Drawing.Size (150 * 8, 45 * 17), backend.PixelSize);
        }

        [Fact]
        public void ATerminalThatDeniesGraphicsDropsAnOptimisticGuessToHalfBlocks ()
        {
            var backend = new TerminalPlatformBackend (new TerminalOptions (), Env (("TERM", "foot")));

            backend.ApplyProbe (Probe (Attributes ("62;22")));

            Assert.Equal (TerminalGraphicsMode.HalfBlock, backend.GraphicsMode);
            Assert.Equal ((1, 2), backend.CellPixels);
        }

        [Fact]
        public void APinnedModeIsNeverOverriddenByTheTerminal ()
        {
            var byOption = new TerminalPlatformBackend (new TerminalOptions { GraphicsMode = TerminalGraphicsMode.Sixel }, Env ());
            byOption.ApplyProbe (Probe (Graphics ("OK"), Attributes ("62")));
            Assert.Equal (TerminalGraphicsMode.Sixel, byOption.GraphicsMode);

            var byVariable = new TerminalPlatformBackend (new TerminalOptions (), Env (("MF_TERMINAL_GRAPHICS", "halfblock")));
            byVariable.ApplyProbe (Probe (Graphics ("OK"), Attributes ("62")));
            Assert.Equal (TerminalGraphicsMode.HalfBlock, byVariable.GraphicsMode);
        }

        [Fact]
        public void AProbeThatNeverCompletedKeepsTheGuess ()
        {
            var backend = new TerminalPlatformBackend (new TerminalOptions (), Env (("TERM", "foot")));

            backend.ApplyProbe (new TerminalProbe ());   // no reply at all

            Assert.Equal (TerminalGraphicsMode.Sixel, backend.GraphicsMode);
        }

        [Fact]
        public void KeyboardProtocolIsOnOnlyWhenTheTerminalSaidSo ()
        {
            var no = new TerminalPlatformBackend (new TerminalOptions (), Env ());
            no.ApplyProbe (Probe (Attributes ("62")));
            Assert.False (no.KittyKeyboard);

            var yes = new TerminalPlatformBackend (new TerminalOptions (), Env ());
            yes.ApplyProbe (Probe (Keyboard (), Attributes ("62")));
            Assert.True (yes.KittyKeyboard);
        }

        // ── Key dispatch ──────────────────────────────────────────────────────

        // A Form showing through a backend window host, recording what reaches it.
        private sealed class Harness : IDisposable
        {
            public readonly TerminalPlatformBackend Backend;
            public readonly List<string> Seen = new ();
            private readonly Form _form;

            public Harness (bool kittyKeyboard)
            {
                Backend = new TerminalPlatformBackend (new TerminalOptions { GraphicsMode = TerminalGraphicsMode.HalfBlock }, Env ());
                if (kittyKeyboard)
                    Backend.ApplyProbe (Probe (Keyboard (), Attributes ("62")));

                var form = _form = new Form { KeyPreview = true };
                form.KeyDown += (_, e) => Seen.Add ("down:" + e.KeyCode);
                form.KeyUp += (_, e) => Seen.Add ("up:" + e.KeyCode);
                form.KeyPress += (_, e) => Seen.Add ("char:" + e.KeyChar);
                form.Show ();

                Backend.CreateWindow (form, isPopup: false).Show ();
            }

            // A shown Form stays in Application.OpenForms, which is process-wide: leaving it open breaks unrelated tests.
            public void Dispose ()
            {
                _form.Close ();
                _form.Dispose ();
                Backend.Dispose ();
            }

            public void Send (Keys key, string? text = null, KeyEventKind kind = KeyEventKind.Press)
                => Backend.Dispatch (new TerminalInput (TerminalInputKind.Key, key, text, Event: kind));
        }

        [Fact]
        public void LegacyTerminalGetsAReleaseSynthesisedAfterEachPress ()
        {
            using var h = new Harness (kittyKeyboard: false);

            h.Send (Keys.A, "a");

            Assert.Equal (new[] { "down:A", "char:a", "up:A" }, h.Seen);
        }

        [Fact]
        public void KittyKeyboardReportsRealReleasesSoNoneIsSynthesised ()
        {
            using var h = new Harness (kittyKeyboard: true);

            h.Send (Keys.A, "a");
            Assert.Equal (new[] { "down:A", "char:a" }, h.Seen);   // still held: no up yet

            h.Send (Keys.A, kind: KeyEventKind.Release);
            Assert.Equal (new[] { "down:A", "char:a", "up:A" }, h.Seen);
        }

        [Fact]
        public void ARepeatIsAnotherKeyDownAndTypesAgain ()
        {
            using var h = new Harness (kittyKeyboard: true);

            h.Send (Keys.A, "a");
            h.Send (Keys.A, "a", KeyEventKind.Repeat);

            Assert.Equal (new[] { "down:A", "char:a", "down:A", "char:a" }, h.Seen);
        }

        [Fact]
        public void AReleaseNeverTypes ()
        {
            using var h = new Harness (kittyKeyboard: true);

            h.Send (Keys.A, "a", KeyEventKind.Release);

            Assert.Equal (new[] { "up:A" }, h.Seen);
        }

        [Fact]
        public void AShortcutDoesNotAlsoType ()
        {
            using var h = new Harness (kittyKeyboard: false);

            h.Send (Keys.S | Keys.Control, text: "s");

            Assert.Equal (new[] { "down:S", "up:S" }, h.Seen);
        }

        [Theory]
        [InlineData (false)]
        [InlineData (true)]
        public void CtrlCAlwaysEndsTheAppAndIsNeverDeliveredToIt (bool kittyKeyboard)
        {
            using var h = new Harness (kittyKeyboard);

            h.Send (Keys.C | Keys.Control);

            Assert.True (h.Backend.ExitRequested);
            Assert.Empty (h.Seen);
        }

        [Fact]
        public void CtrlCReleaseDoesNotEndTheAppAgainOrReachIt ()
        {
            using var h = new Harness (kittyKeyboard: true);

            h.Send (Keys.C | Keys.Control, kind: KeyEventKind.Release);

            Assert.False (h.Backend.ExitRequested);
            Assert.Empty (h.Seen);
        }

        [Fact]
        public void CtrlShiftCIsAnOrdinaryShortcut ()
        {
            using var h = new Harness (kittyKeyboard: false);

            h.Send (Keys.C | Keys.Control | Keys.Shift);

            Assert.False (h.Backend.ExitRequested);
            Assert.Equal (new[] { "down:C", "up:C" }, h.Seen);
        }
    }
}
