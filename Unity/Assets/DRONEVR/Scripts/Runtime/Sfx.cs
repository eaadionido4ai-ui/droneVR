// Small sounds made when the game starts, as TANGHALAN VR makes them: a click, a pop, a cheer of notes, an
// alarm, a camera's shutter, a radio's blip, a splash, a crash; and the sounds that go on: the drone's motors
// (higher as it works harder), the wind and the rain. No sound files, so the app stays light.
using System.Collections.Generic;
using UnityEngine;

namespace DroneVR.Vr
{
    public static class Sfx
    {
        const int Rate = 44100;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static AudioSource oneShots, motor, wind, rain;
        static readonly System.Random rnd = new System.Random(7);

        public static void Init(Transform listener)
        {
            oneShots = listener.gameObject.AddComponent<AudioSource>();
            oneShots.playOnAwake = false; oneShots.spatialBlend = 0;
            Make("pop", Tones(new[] { 520f, 880f }, new[] { 0f, 0.07f }, 0.14f, 1, 0.5f));
            Make("drop", Tones(new[] { 330f }, new[] { 0f }, 0.14f, 1, 0.45f));
            Make("click", Tones(new[] { 1400f }, new[] { 0f }, 0.05f, 2, 0.18f));
            Make("cheer", Tones(new[] { 523f, 659f, 784f, 1047f }, new[] { 0f, 0.11f, 0.22f, 0.33f }, 0.3f, 1, 0.45f));
            Make("sad", Tones(new[] { 330f, 262f }, new[] { 0f, 0.25f }, 0.45f, 0, 0.4f));
            Make("radio", Tones(new[] { 1200f, 900f }, new[] { 0f, 0.06f }, 0.06f, 2, 0.12f));
            var alarm = new List<float>(); var at = new List<float>();
            for (int i = 0; i < 3; i++) { alarm.Add(960); at.Add(i * 0.32f); alarm.Add(760); at.Add(i * 0.32f + 0.16f); }
            Make("alarm", Tones(alarm.ToArray(), at.ToArray(), 0.16f, 2, 0.2f));
            Make("shutter", Shutter());
            Make("splash", Noise(0.6f, 0.5f, false, 0.25f, true));
            Make("crash", Noise(0.5f, 0.8f, false, 0.6f, true));
            motor = Loop(listener, Motor(), "motor");
            wind = Loop(listener, Noise(3f, 0.4f, false, 0.03f, false), "wind");
            rain = Loop(listener, Noise(2f, 0.3f, false, 0.35f, false), "rain");
        }

        static void Make(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            clips[name] = c;
        }

        static AudioSource Loop(Transform listener, float[] data, string name)
        {
            Make(name, data);
            var s = listener.gameObject.AddComponent<AudioSource>();
            s.clip = clips[name]; s.loop = true; s.playOnAwake = false; s.volume = 0; s.spatialBlend = 0;
            return s;
        }

        // notes: 0 sine, 1 triangle, 2 square
        static float[] Tones(float[] freq, float[] start, float len, int wave, float vol)
        {
            float end = 0; foreach (var s in start) end = Mathf.Max(end, s + len);
            var d = new float[(int)(Rate * (end + 0.05f))];
            for (int k = 0; k < freq.Length; k++)
            {
                int s0 = (int)(start[k] * Rate), n = (int)(len * Rate);
                for (int i = 0; i < n && s0 + i < d.Length; i++)
                {
                    float t = (float)i / Rate, ph = freq[k] * t;
                    float w = wave == 0 ? Mathf.Sin(ph * 2 * Mathf.PI) : wave == 1 ? 1 - 4 * Mathf.Abs(ph - Mathf.Floor(ph + 0.5f)) : (ph - Mathf.Floor(ph) < 0.5f ? 1 : -1);
                    float env = Mathf.Min(1, t / 0.02f) * Mathf.Exp(-4f * t / len);
                    d[s0 + i] += w * env * vol;
                }
            }
            return d;
        }

        // filtered noise: k is how much high sound it keeps; fading, it dies away; otherwise it swells slowly (a loop)
        static float[] Noise(float len, float vol, bool rising, float k, bool fading)
        {
            var d = new float[(int)(Rate * len)];
            float lp = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / d.Length;
                lp += k * ((float)rnd.NextDouble() * 2 - 1 - lp);
                float env = fading ? Mathf.Exp(-5 * t) : rising ? Mathf.Sin(t * Mathf.PI) : 0.75f + 0.25f * Mathf.Sin(t * 2 * Mathf.PI);
                d[i] = lp * vol * env / Mathf.Sqrt(k);
            }
            return d;
        }

        static float[] Shutter()
        {
            var d = new float[Rate / 5];
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float click = (t < 0.012f || (t > 0.09f && t < 0.1f)) ? ((float)rnd.NextDouble() * 2 - 1) * 0.6f : 0;
                d[i] = click;
            }
            return d;
        }

        // four motors: a buzz with its overtones and a little air, a whole number of waves in the loop
        static float[] Motor()
        {
            var d = new float[Rate * 2];
            float lp = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                lp += 0.2f * ((float)rnd.NextDouble() * 2 - 1 - lp);
                d[i] = 0.32f * Mathf.Sin(2 * Mathf.PI * 160 * t) + 0.18f * Mathf.Sin(2 * Mathf.PI * 320 * t) + 0.1f * Mathf.Sin(2 * Mathf.PI * 482 * t + 0.4f)
                    + 0.06f * Mathf.Sin(2 * Mathf.PI * 167 * t) + 0.12f * lp;
            }
            return d;
        }

        public static void Play(string name, float volume = 1f)
        {
            AudioClip c;
            if (oneShots != null && clips.TryGetValue(name, out c)) oneShots.PlayOneShot(c, volume);
        }

        /// <summary>The motors: how loud (0 to 1), and how hard they work (0 hovering, 1 flat out).</summary>
        public static void Motor(float loud, float effort)
        {
            SetLoop(motor, loud * 0.5f);
            if (motor != null) motor.pitch = Mathf.Lerp(motor.pitch, 0.85f + 0.45f * Mathf.Clamp01(effort), Time.deltaTime * 4);
        }

        /// <summary>The wind and the rain, as strong as they are (0 to 1).</summary>
        public static void Weather(float windStrength, float rainStrength)
        {
            SetLoop(wind, Mathf.Clamp01(windStrength) * 0.5f);
            SetLoop(rain, Mathf.Clamp01(rainStrength) * 0.45f);
        }

        static void SetLoop(AudioSource s, float v)
        {
            if (s == null) return;
            s.volume = Mathf.MoveTowards(s.volume, v, Time.deltaTime * 2f);
            if (s.volume > 0.001f && !s.isPlaying) s.Play();
            else if (s.volume <= 0.001f && s.isPlaying) s.Stop();
        }
    }
}
