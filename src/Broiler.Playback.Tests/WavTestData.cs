namespace Broiler.Playback.Tests;

internal static class WavTestData
{
    /// <summary>Builds a minimal valid 16-bit PCM mono RIFF/WAVE with the given frame count.</summary>
    public static byte[] Pcm16Mono(int sampleRate, int frameCount)
    {
        const int channels = 1;
        const int bitsPerSample = 16;
        int blockAlign = channels * bitsPerSample / 8;
        int byteRate = sampleRate * blockAlign;
        int dataSize = frameCount * blockAlign;

        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write("RIFF"u8.ToArray());
            w.Write(36 + dataSize);
            w.Write("WAVE"u8.ToArray());
            w.Write("fmt "u8.ToArray());
            w.Write(16);                 // PCM fmt chunk size
            w.Write((short)1);           // audio format: PCM
            w.Write((short)channels);
            w.Write(sampleRate);
            w.Write(byteRate);
            w.Write((short)blockAlign);
            w.Write((short)bitsPerSample);
            w.Write("data"u8.ToArray());
            w.Write(dataSize);
            for (int i = 0; i < frameCount; i++)
                w.Write((short)((i % 200) - 100));
        }

        return ms.ToArray();
    }
}
