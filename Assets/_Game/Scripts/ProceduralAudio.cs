using System.Collections.Generic;
using UnityEngine;

namespace GunMan
{
    /// <summary>
    /// Generates simple synthesized sound effects at runtime so the prototype has audio feedback
    /// without needing any audio assets. Clips are cached per key.
    /// </summary>
    public static class ProceduralAudio
    {
        const int SampleRate = 44100;
        static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        public static AudioClip Gunshot(string key, float duration = 0.25f, float lowpass = 0.35f, float punch = 1f, float tail = 0.6f)
        {
            if (Cache.TryGetValue(key, out var clip)) return clip;
            int n = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[n];
            var rng = new System.Random(key.GetHashCode());
            float lp = 0f;
            float freq = 90f * punch;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * (14f / Mathf.Max(0.05f, duration)) * tail);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * lowpass;
                float thump = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 40f) * 0.9f * punch;
                float crack = (t < 0.004f) ? noise * 1.2f : 0f;
                data[i] = Mathf.Clamp((lp * 1.6f + thump + crack) * env, -1f, 1f);
            }
            clip = AudioClip.Create(key, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            Cache[key] = clip;
            return clip;
        }

        public static AudioClip Explosion()
        {
            const string key = "explosion";
            if (Cache.TryGetValue(key, out var clip)) return clip;
            float duration = 1.6f;
            int n = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[n];
            var rng = new System.Random(1234);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 3.2f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.08f;
                float boom = Mathf.Sin(2f * Mathf.PI * 45f * t) * Mathf.Exp(-t * 6f);
                data[i] = Mathf.Clamp((lp * 2.5f + boom * 0.8f) * env, -1f, 1f);
            }
            clip = AudioClip.Create(key, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            Cache[key] = clip;
            return clip;
        }

        public static AudioClip Click(string key, float pitch = 1800f, float duration = 0.06f)
        {
            if (Cache.TryGetValue(key, out var clip)) return clip;
            int n = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 90f);
                data[i] = Mathf.Sin(2f * Mathf.PI * pitch * t) * env * 0.6f;
            }
            clip = AudioClip.Create(key, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            Cache[key] = clip;
            return clip;
        }

        public static AudioClip Impact()
        {
            const string key = "impact";
            if (Cache.TryGetValue(key, out var clip)) return clip;
            float duration = 0.12f;
            int n = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[n];
            var rng = new System.Random(99);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 60f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.5f;
                data[i] = Mathf.Clamp(lp * env * 0.8f, -1f, 1f);
            }
            clip = AudioClip.Create(key, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            Cache[key] = clip;
            return clip;
        }
    }
}
