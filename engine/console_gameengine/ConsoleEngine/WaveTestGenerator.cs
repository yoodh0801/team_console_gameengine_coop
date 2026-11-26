using ConsoleEngine;
using System.Text;

public static class WaveTestGenerator
{
    public static void CreateBeepWav(string filePath, double frequencyHz = 523.25, double durationSeconds = 0.25, double amplitude = 0.35)
    {
        const int sampleRate = 44100;
        const short bitsPerSample = 16;
        const short channels = 1;
        int sampleCount = (int)(sampleRate * durationSeconds);
        int byteRate = sampleRate * channels * (bitsPerSample / 8);
        short blockAlign = (short)(channels * (bitsPerSample / 8));
        int dataSize = sampleCount * blockAlign;

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // RIFF 헤더
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataSize);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));

        // fmt 청크
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1); // PCM
        bw.Write(channels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write(blockAlign);
        bw.Write(bitsPerSample);

        // data 청크
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataSize);

        for (int i = 0; i < sampleCount; i++)
        {
            double t = (double)i / sampleRate;
            // 끝부분 클릭 노이즈 방지용 짧은 페이드아웃
            double envelope = Math.Min(1.0, (sampleCount - i) / (sampleRate * 0.02));
            short sample = (short)(Math.Sin(2.0 * Math.PI * frequencyHz * t) * amplitude * envelope * short.MaxValue);
            bw.Write(sample);
        }
    }
}
