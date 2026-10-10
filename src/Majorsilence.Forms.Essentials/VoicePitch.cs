using System;
using System.Collections.Generic;

namespace Majorsilence.Forms.Essentials
{
    /// <summary>
    /// Estimates the pitch of a spoken sample, to say whether a synthesised voice is a man's or a woman's where the platform will not
    /// (Android does not). A speaking voice is steady enough for this: a man's sits near 85 to 155 Hz and a woman's near 165 to 255 Hz (the synthesised ones sit a little closer together), and a
    /// synthesiser keeps to its voice, so the median pitch of a sentence separates them cleanly. It is an estimate, and is labelled one.
    /// </summary>
    internal static class VoicePitch
    {
        // Where the synthesised men's voices (about 130 to 140 Hz on Android's Google voices) end and the women's (160 and up) begin, in Hz.
        private const double MenAndWomenMeet = 150;

        private const double LowestHz = 70;
        private const double HighestHz = 400;

        /// <summary>A man's, a woman's, or no opinion when no pitch was found.</summary>
        internal static VoiceGender GenderOf (double? hz)
            => hz is not { } pitch ? VoiceGender.Unknown : pitch < MenAndWomenMeet ? VoiceGender.Male : VoiceGender.Female;

        /// <summary>
        /// The median pitch, in Hz, of the voiced parts of <paramref name="samples"/> (16-bit mono at <paramref name="rate"/>), or null when too
        /// little of it is voiced to say (silence, noise).
        /// </summary>
        internal static double? Estimate (short[] samples, int rate)
        {
            if (rate <= 0 || samples.Length < rate / 4)
                return null;

            var frame = rate * 40 / 1000;
            var hop = rate * 20 / 1000;
            var minLag = (int)(rate / HighestHz);
            var maxLag = (int)(rate / LowestHz);
            var pitches = new List<double> ();

            for (var start = 0; start + frame + maxLag <= samples.Length; start += hop) {
                // Silence between words has no pitch; so does a frame too quiet to trust.
                double energy = 0;
                for (var i = 0; i < frame; i++)
                    energy += (double)samples[start + i] * samples[start + i];

                if (Math.Sqrt (energy / frame) < 300)
                    continue;

                var scores = new double[maxLag + 1];
                var best = 0.0;
                for (var lag = minLag; lag <= maxLag; lag++) {
                    double correlation = 0, energyLag = 0;
                    for (var i = 0; i < frame; i++) {
                        correlation += (double)samples[start + i] * samples[start + i + lag];
                        energyLag += (double)samples[start + i + lag] * samples[start + i + lag];
                    }

                    scores[lag] = correlation / Math.Sqrt (energy * energyLag + 1e-9);
                    best = Math.Max (best, scores[lag]);
                }

                // A voiced frame repeats itself closely; noise and unvoiced consonants do not.
                if (best <= 0.6)
                    continue;

                // The first peak nearly as good as the best, not the best itself: a whole number of periods repeats as well as one, and taking
                // the best would sometimes answer an octave low.
                for (var lag = minLag + 1; lag < maxLag; lag++) {
                    if (scores[lag] >= best * 0.9 && scores[lag] >= scores[lag - 1] && scores[lag] >= scores[lag + 1]) {
                        pitches.Add ((double)rate / lag);
                        break;
                    }
                }
            }

            if (pitches.Count < 5)
                return null;

            pitches.Sort ();
            return pitches[pitches.Count / 2];
        }

        /// <summary>The 16-bit mono samples and sample rate of a PCM .wav file, or null when it is not one.</summary>
        internal static (short[] Samples, int Rate)? ReadWav (byte[] bytes)
        {
            if (bytes.Length < 44 || bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F')
                return null;

            var rate = BitConverter.ToInt32 (bytes, 24);
            var channels = BitConverter.ToInt16 (bytes, 22);
            var bits = BitConverter.ToInt16 (bytes, 34);
            if (bits != 16 || channels < 1 || rate <= 0)
                return null;

            // Chunks follow the 12-byte header; the samples are in the "data" one.
            var position = 12;
            while (position + 8 <= bytes.Length) {
                var size = BitConverter.ToInt32 (bytes, position + 4);
                if (bytes[position] == 'd' && bytes[position + 1] == 'a' && bytes[position + 2] == 't' && bytes[position + 3] == 'a') {
                    var available = Math.Min (size < 0 ? int.MaxValue : size, bytes.Length - position - 8);
                    var frames = available / (2 * channels);
                    var samples = new short[frames];
                    for (var i = 0; i < frames; i++)
                        samples[i] = BitConverter.ToInt16 (bytes, position + 8 + i * 2 * channels);     // the first channel

                    return (samples, rate);
                }

                if (size < 0)
                    return null;

                position += 8 + size + (size & 1);
            }

            return null;
        }
    }
}
