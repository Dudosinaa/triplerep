using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed partial class FirstMindInteriorBuilder
{
    private static AudioClip[] DoorSounds()
    {
        return new[]
        {
            WriteDoorSound("Door - opening", 0.89f, 17, 0.30f, true),
            WriteDoorSound("Door - closing", 0.87f, 42, 0.54f, false),
            WriteDoorSound("Door - wood and latch", 0.28f, 73, 0f, false)
        };
    }

    private static AudioClip WriteDoorSound(string name, float duration, int seed, float secondCreak, bool opening)
    {
        const int rate = 44100;
        string path = Data + "/" + name + ".wav";
        int frames = Mathf.CeilToInt(duration * rate);
        var rng = new System.Random(seed);
        float phase = 0f;
        float low = 0f;
        float soft = 0f;
        using (var wav = new BinaryWriter(File.Create(path)))
        {
            wav.Write(0x46464952);
            wav.Write(36 + frames * 2);
            wav.Write(0x45564157);
            wav.Write(0x20746D66);
            wav.Write(16);
            wav.Write((short)1);
            wav.Write((short)1);
            wav.Write(rate);
            wav.Write(rate * 2);
            wav.Write((short)2);
            wav.Write((short)16);
            wav.Write(0x61746164);
            wav.Write(frames * 2);
            for (int i = 0; i < frames; i++)
            {
                float time = i / (float)rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                low += (noise - low) * 0.118f;
                soft += (low - soft) * 0.035f;
                float sample;
                if (secondCreak == 0f)
                {
                    sample = Knock(time - 0.013f, soft, 1f) + Knock(time - 0.041f, low * 0.38f, 0.27f);
                }
                else
                {
                    float body = Pulse(time, opening ? 0.20f : 0.24f, 0.084f) * 0.77f +
                        Pulse(time, secondCreak, 0.116f) * 0.62f;
                    float stickSlip = 0.68f + 0.32f * Mathf.Sin(time * 2f * Mathf.PI * (19f + time * 9f));
                    phase += 2f * Mathf.PI / rate * (145f + 27f * Mathf.Sin(time * 7f) + soft * 38f + time * 92f);
                    sample = body * stickSlip * (0.068f * Mathf.Sin(phase) +
                        0.029f * Mathf.Sin(phase * 2f + Mathf.Sin(time * 35f)) +
                        0.018f * Mathf.Sin(phase * 3.02f) + low * 0.105f);
                    sample += Knock(time - (opening ? 0.029f : 0.26f), soft, opening ? 0.49f : 0.11f);
                    sample += Knock(time - 0.078f, low * 0.37f, opening ? 0.13f : 0.055f);
                }
                float edge = Mathf.Clamp01(time / 0.006f) * Mathf.Clamp01((duration - time) / 0.037f);
                wav.Write((short)(Mathf.Clamp(sample * edge, -1f, 1f) * short.MaxValue));
            }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return Load<AudioClip>(path);
    }

    private static float Pulse(float time, float center, float width)
    {
        float x = (time - center) / width;
        return Mathf.Exp(-x * x * 1.6f);
    }

    private static float Knock(float time, float noise, float strength)
    {
        if (time < 0f) return 0f;
        return Mathf.Clamp01(time * 500f) * Mathf.Exp(-time * 39f) * strength *
            (Mathf.Sin(time * 405f) * 0.36f + Mathf.Sin(time * 761f) * 0.13f +
                Mathf.Sin(time * 1833f) * 0.11f + noise * 0.43f);
    }
}
