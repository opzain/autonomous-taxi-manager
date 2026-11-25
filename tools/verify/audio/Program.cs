using System;
using System.IO;
using System.Reflection;

internal static class Program
{
    private static int Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : "wav";
        Directory.CreateDirectory(outDir);
        MethodInfo synth = typeof(TaxiSim.World.ProceduralAudio).GetMethod("Synth", BindingFlags.NonPublic | BindingFlags.Static);
        int bad = 0;
        foreach (string name in TaxiSim.World.ProceduralAudio.ClipNames)
        {
            float[] x = (float[])synth.Invoke(null, new object[] { name });
            double peak = 0, rms = 0;
            int nan = 0;
            foreach (float v in x)
            {
                if (float.IsNaN(v) || float.IsInfinity(v)) { nan++; continue; }
                peak = Math.Max(peak, Math.Abs(v));
                rms += v * v;
            }
            rms = Math.Sqrt(rms / Math.Max(1, x.Length));
            bool ok = nan == 0 && peak > 0.05 && peak <= 1.0 && rms > 0.01;
            if (!ok) bad++;
            Console.WriteLine($"   {(ok ? "ok " : "BAD")} {name,-18} {x.Length / 44100.0,5:0.00}s  peak {peak:0.00}  rms {rms:0.000}");
            WriteWav(Path.Combine(outDir, name + ".wav"), x);
        }
        Console.WriteLine(bad == 0 ? $"{TaxiSim.World.ProceduralAudio.ClipNames.Length} sounds OK" : $"{bad} sounds FAILED");
        return bad == 0 ? 0 : 1;
    }

    private static void WriteWav(string path, float[] x)
    {
        using var f = new BinaryWriter(File.Create(path));
        int n = x.Length;
        f.Write("RIFF"u8.ToArray()); f.Write(36 + n * 2); f.Write("WAVEfmt "u8.ToArray());
        f.Write(16); f.Write((short)1); f.Write((short)1); f.Write(44100); f.Write(88200); f.Write((short)2); f.Write((short)16);
        f.Write("data"u8.ToArray()); f.Write(n * 2);
        foreach (float v in x) f.Write((short)Math.Clamp(v * 32767f, -32768f, 32767f));
    }
}
