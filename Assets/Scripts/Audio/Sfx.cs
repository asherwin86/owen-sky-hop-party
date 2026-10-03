using System.Collections.Generic;
using UnityEngine;

namespace SkyHop
{
    /// <summary>All sound is synthesised at runtime, so there are no audio files to import.</summary>
    public static class Sfx
    {
        private const int SR = 22050;
        private static AudioSource _src, _music;
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        private static AudioClip _theme;
        private static bool _wantMusic;

        public static bool Muted
        {
            get { return PlayerPrefs.GetInt("hop_mute", 0) == 1; }
            set { PlayerPrefs.SetInt("hop_mute", value ? 1 : 0); Apply(); }
        }

        private static void Init()
        {
            if (_src != null) return;
            var go = new GameObject("Sfx"); Object.DontDestroyOnLoad(go);
            _src = go.AddComponent<AudioSource>();
            _music = go.AddComponent<AudioSource>(); _music.loop = true; _music.volume = 0.28f;
            Apply();
        }

        private static void Apply()
        {
            if (_src == null) return;
            _src.mute = Muted; _music.mute = Muted;
            if (_wantMusic && !Muted && !_music.isPlaying) { _music.clip = Theme(); _music.Play(); }
        }

        public static void Play(string name, float vol = 1f)
        {
            Init();
            if (Muted) return;
            _src.PlayOneShot(Get(name), vol);
        }

        public static void PlayAt(string name, Vector3 pos, float vol = 1f)
        {
            var cam = Camera.main;
            float d = cam != null ? Vector3.Distance(cam.transform.position, pos) : 0f;
            float v = vol * Mathf.Clamp01(1.4f - d / 40f);
            if (v > 0.02f) Play(name, v);
        }

        public static void Music(bool on)
        {
            Init();
            _wantMusic = on;
            if (on) { if (!Muted && !_music.isPlaying) { _music.clip = Theme(); _music.Play(); } }
            else _music.Stop();
        }

        private static AudioClip Get(string name)
        {
            if (Clips.TryGetValue(name, out var c) && c != null) return c;
            c = Make(name); Clips[name] = c; return c;
        }

        private delegate float Gen(float t, float p);

        private static AudioClip Build(string name, float dur, Gen g)
        {
            int n = (int)(dur * SR);
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(g(i / (float)SR, i / (float)n), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, SR, false);
            clip.SetData(d, 0);
            return clip;
        }

        private static float Sine(float f, float t) { return Mathf.Sin(2f * Mathf.PI * f * t); }
        private static float Square(float f, float t) { return Sine(f, t) >= 0f ? 1f : -1f; }
        private static readonly System.Random Rnd = new System.Random(3);
        private static float Noise() { return (float)(Rnd.NextDouble() * 2.0 - 1.0); }

        private static AudioClip Make(string n)
        {
            switch (n)
            {
                case "jump": return Build(n, 0.2f, (t, p) => Sine(Mathf.Lerp(320f, 720f, p), t) * 0.45f * (1f - p));
                case "djump": return Build(n, 0.22f, (t, p) => Sine(Mathf.Lerp(480f, 1000f, p), t) * 0.4f * (1f - p));
                case "land": return Build(n, 0.12f, (t, p) => (Noise() * 0.25f + Sine(110f, t) * 0.4f) * (1f - p) * (1f - p));
                case "pad": return Build(n, 0.45f, (t, p) => Sine(Mathf.Lerp(220f, 1300f, p) + Sine(18f, t) * 40f, t) * 0.4f * (1f - p * 0.8f));
                case "knock": return Build(n, 0.25f, (t, p) => (Noise() * 0.4f + Sine(Mathf.Lerp(200f, 60f, p), t) * 0.6f) * (1f - p));
                case "click": return Build(n, 0.06f, (t, p) => Sine(900f, t) * 0.3f * (1f - p));
                case "beep": return Build(n, 0.18f, (t, p) => Square(440f, t) * 0.18f * (1f - p * 0.3f));
                case "go": return Build(n, 0.45f, (t, p) => (Square(880f, t) * 0.14f + Sine(1320f, t) * 0.18f) * (1f - p));
                case "fall": return Build(n, 0.55f, (t, p) => Sine(Mathf.Lerp(700f, 90f, p), t) * 0.4f * (1f - p));
                case "checkpoint": return Build(n, 0.3f, (t, p) => (t < 0.12f ? Sine(660f, t) : Sine(990f, t)) * 0.35f * (1f - p));
                case "finish":
                    return Build(n, 1.1f, (t, p) =>
                    {
                        float[] notes = { 523f, 659f, 784f, 1047f };
                        int i = Mathf.Min(3, (int)(t / 0.16f));
                        float local = t - i * 0.16f;
                        float env = i == 3 ? (1f - p) : Mathf.Clamp01(1f - local / 0.2f);
                        return (Square(notes[i], t) * 0.12f + Sine(notes[i] * 2f, t) * 0.12f) * env;
                    });
                case "win":
                    return Build(n, 1.4f, (t, p) =>
                    {
                        float[] notes = { 392f, 523f, 659f, 784f, 1047f };
                        int i = Mathf.Min(4, (int)(t / 0.14f));
                        float local = t - i * 0.14f;
                        float env = i == 4 ? (1f - p) : Mathf.Clamp01(1f - local / 0.16f);
                        return (Square(notes[i], t) * 0.12f + Sine(notes[i], t) * 0.2f) * env;
                    });
                default: return Build(n, 0.1f, (t, p) => 0f);
            }
        }

        private static AudioClip Theme()
        {
            if (_theme != null) return _theme;
            const float step = 0.19f;            // 16th-ish, ~79 bpm eighths => bouncy
            const int steps = 64;
            int n = (int)(step * steps * SR);
            var d = new float[n];
            int[] pent = { 0, 2, 4, 7, 9 };
            int[] roots = { 0, 0, 5, 7, 0, 0, 5, 7 };      // semitones above C
            var rnd = new System.Random(11);
            for (int s = 0; s < steps; s += 2)
            {
                int bar = s / 8;
                int root = roots[bar % roots.Length];
                int deg = pent[rnd.Next(pent.Length)] + 12 * rnd.Next(1, 3);
                float f = 261.63f * Mathf.Pow(2f, (root + deg) / 12f);
                int start = (int)(s * step * SR), len = (int)(step * 1.8f * SR);
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float t = i / (float)SR;
                    float env = Mathf.Exp(-t * 5f);
                    d[start + i] += (Square(f, t) * 0.5f + Sine(f * 2f, t) * 0.25f) * env * 0.35f;
                }
            }
            for (int s = 0; s < steps; s += 4)
            {
                int bar = s / 8;
                float f = 130.81f * Mathf.Pow(2f, roots[bar % roots.Length] / 12f);
                if ((s / 4) % 2 == 1) f *= 1.5f;
                int start = (int)(s * step * SR), len = (int)(step * 3.6f * SR);
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float t = i / (float)SR;
                    d[start + i] += Sine(f, t) * Mathf.Exp(-t * 2.2f) * 0.5f;
                }
            }
            for (int s = 0; s < steps; s += 2)
            {
                int start = (int)(s * step * SR), len = (int)(0.05f * SR);
                float amp = s % 4 == 0 ? 0.35f : 0.15f;
                for (int i = 0; i < len && start + i < n; i++) d[start + i] += Noise() * amp * (1f - i / (float)len);
            }
            float peak = 0.01f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(d[i]));
            for (int i = 0; i < n; i++) d[i] = d[i] / peak * 0.8f;
            _theme = AudioClip.Create("HopTheme", n, 1, SR, false);
            _theme.SetData(d, 0);
            return _theme;
        }
    }
}
