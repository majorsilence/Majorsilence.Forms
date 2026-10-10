using System;
using Majorsilence.Forms.Essentials;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Android does not say whether a voice is a man's or a woman's. The framework speaks a sample into a file and estimates its pitch: a man's
// speaking voice sits near 85 to 155 Hz and a woman's near 165 to 255 Hz. These test the estimate on tones with a voice's harmonic shape.
public class VoicePitchTests
{
    // A voice-like tone: the fundamental and its first few harmonics, falling off, with a little noise, at a given sample rate.
    private static short[] Tone (double hz, double seconds = 1.0, int rate = 22050, double noise = 0.02)
    {
        var random = new Random (7);
        var samples = new short[(int)(seconds * rate)];
        for (var i = 0; i < samples.Length; i++) {
            var t = i / (double)rate;
            var value = 0.0;
            for (var h = 1; h <= 6; h++)
                value += Math.Sin (2 * Math.PI * hz * h * t) / h;

            value = value / 2.4 + (random.NextDouble () - 0.5) * noise;
            samples[i] = (short)(value * 12000);
        }

        return samples;
    }

    [Theory]
    [InlineData (95.0)]
    [InlineData (120.0)]
    [InlineData (150.0)]
    [InlineData (190.0)]
    [InlineData (220.0)]
    [InlineData (250.0)]
    public void The_pitch_of_a_voice_like_tone_is_found_within_five_percent (double hz)
    {
        var estimate = VoicePitch.Estimate (Tone (hz), 22050);

        Assert.NotNull (estimate);
        Assert.InRange (estimate.Value, hz * 0.95, hz * 1.05);
    }

    [Fact]
    public void The_pitch_is_found_at_other_sample_rates ()
    {
        Assert.InRange (VoicePitch.Estimate (Tone (130, rate: 16000), 16000)!.Value, 123, 137);
        Assert.InRange (VoicePitch.Estimate (Tone (210, rate: 24000), 24000)!.Value, 200, 220);
    }

    [Fact]
    public void Silence_has_no_pitch ()
    {
        Assert.Null (VoicePitch.Estimate (new short[22050], 22050));
    }

    [Fact]
    public void Noise_has_no_pitch ()
    {
        var random = new Random (3);
        var noise = new short[22050];
        for (var i = 0; i < noise.Length; i++)
            noise[i] = (short)((random.NextDouble () - 0.5) * 20000);

        Assert.Null (VoicePitch.Estimate (noise, 22050));
    }

    [Theory]
    [InlineData (100.0, VoiceGender.Male)]
    [InlineData (139.0, VoiceGender.Male)]
    [InlineData (160.0, VoiceGender.Female)]
    [InlineData (190.0, VoiceGender.Female)]
    [InlineData (230.0, VoiceGender.Female)]
    public void A_pitch_is_called_a_mans_or_a_womans_voice (double hz, VoiceGender expected)
    {
        Assert.Equal (expected, VoicePitch.GenderOf (hz));
    }

    [Fact]
    public void No_pitch_is_no_opinion ()
    {
        Assert.Equal (VoiceGender.Unknown, VoicePitch.GenderOf (null));
    }

    [Fact]
    public void A_wav_file_is_read_to_its_samples_and_rate ()
    {
        var samples = Tone (180, 0.5, 22050);
        using var stream = new System.IO.MemoryStream ();
        using (var writer = new System.IO.BinaryWriter (stream, System.Text.Encoding.ASCII, leaveOpen: true)) {
            writer.Write ("RIFF"u8.ToArray ());
            writer.Write (36 + samples.Length * 2);
            writer.Write ("WAVEfmt "u8.ToArray ());
            writer.Write (16); writer.Write ((short)1); writer.Write ((short)1);
            writer.Write (22050); writer.Write (22050 * 2); writer.Write ((short)2); writer.Write ((short)16);
            writer.Write ("data"u8.ToArray ());
            writer.Write (samples.Length * 2);
            foreach (var s in samples)
                writer.Write (s);
        }

        var wav = VoicePitch.ReadWav (stream.ToArray ());

        Assert.NotNull (wav);
        Assert.Equal (22050, wav.Value.Rate);
        Assert.Equal (samples.Length, wav.Value.Samples.Length);
        Assert.Equal (samples[100], wav.Value.Samples[100]);
    }

    [Fact]
    public void Something_that_is_not_a_wav_file_is_refused ()
    {
        Assert.Null (VoicePitch.ReadWav ([1, 2, 3, 4]));
        Assert.Null (VoicePitch.ReadWav (new byte[100]));
    }
}
