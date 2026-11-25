using System;
using System.Collections.Generic;
using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Synthesises every city sound at start-up, so the project needs no audio files:
    /// traffic rumble, rain, thunder, wind, horns (car, scooter, auto, truck, bus), crowd murmur,
    /// a station chime and PA voice, train horn and wheel clatter, the fleet-hub electrical hum and
    /// charger chirps, crows by day, crickets by night and the pedestrian-crossing beep.
    /// Any clip can be replaced by a real recording: put a file with the same name
    /// (e.g. "horn_car.wav") in Assets/_Project/Resources/TaxiSimAudio/.
    /// Deterministic (seeded) and loop-safe (looping clips crossfade their ends).
    /// </summary>
    public static class ProceduralAudio
    {
        public const int Rate = 44100;
        private static Pcg32 _rng = new Pcg32(1977UL);

        public static readonly string[] ClipNames =
        {
            "traffic_bed", "rain_loop", "thunder", "wind_loop", "crowd_loop", "crickets_loop", "hub_hum",
            "train_rumble", "horn_car", "horn_car_double", "horn_scooter", "horn_auto", "horn_truck", "horn_bus",
            "station_chime", "pa_announcement", "train_horn", "charge_chirp", "crow", "ped_beep",
            "dhol_loop", "firework"
        };

        public static Dictionary<string, AudioClip> BuildAll(out int overrides)
        {
            _rng = new Pcg32(1977UL);
            overrides = 0;
            var d = new Dictionary<string, AudioClip>();
            foreach (string name in ClipNames)
            {
                AudioClip real = Resources.Load<AudioClip>("TaxiSimAudio/" + name);
                if (real != null) { d[name] = real; overrides++; continue; }
                float[] data = Synth(name);
                if (data == null) continue;
                var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
                clip.SetData(data, 0);
                d[name] = clip;
            }
            return d;
        }

        private static float[] Synth(string name)
        {
            switch (name)
            {
                case "traffic_bed": return Loop(TrafficBed(8.5f), 0.5f);
                case "rain_loop": return Loop(Rain(6.5f), 0.5f);
                case "thunder": return Thunder();
                case "wind_loop": return Loop(Wind(6.5f), 0.5f);
                case "crowd_loop": return Loop(Babble(8.5f, 9, 0.18f, 3200f), 0.5f);
                case "crickets_loop": return Crickets(4f);
                case "hub_hum": return HubHum(4f);
                case "train_rumble": return Loop(TrainRumble(4.5f), 0.5f);
                case "horn_car": return Horn(new[] { 410f, 512f }, 0.45f, 1, 0f, 0.55f);
                case "horn_car_double": return Horn(new[] { 400f, 500f }, 0.17f, 2, 0.09f, 0.55f);
                case "horn_scooter": return Horn(new[] { 880f, 1100f }, 0.12f, 2, 0.07f, 0.35f);
                case "horn_auto": return BulbHorn();
                case "horn_truck": return TruckHorn();
                case "horn_bus": return Horn(new[] { 196f, 247f, 294f }, 0.9f, 1, 0f, 0.8f);
                case "station_chime": return Chime(new[] { 659.3f, 523.3f, 784f }, 0.42f, 2.6f);
                case "pa_announcement": return PaVoice(5f);
                case "train_horn": return Horn(new[] { 290f, 348f }, 1.3f, 1, 0f, 0.9f, 0.08f);
                case "charge_chirp": return Chirp();
                case "crow": return Crow();
                case "ped_beep": return Beep(2800f, 0.05f);
                case "dhol_loop": return DholLoop();
                case "firework": return Firework();
                default: return null;
            }
        }

        // ------------------------------------------------------------------ building blocks

        private static float Noise() => (float)(_rng.NextDouble() * 2.0 - 1.0);
        private static float[] Buf(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        /// <summary>One-pole low-pass coefficient for a cutoff frequency.</summary>
        private static float Lp(float hz) => 1f - Mathf.Exp(-2f * Mathf.PI * hz / Rate);

        private static float[] Normalise(float[] x, float peak)
        {
            float m = 1e-6f;
            foreach (float v in x) m = Mathf.Max(m, Mathf.Abs(v));
            float k = peak / m;
            for (int i = 0; i < x.Length; i++) x[i] *= k;
            return x;
        }

        /// <summary>Makes a clip loop seamlessly: the tail is crossfaded into the head.</summary>
        private static float[] Loop(float[] x, float fadeSeconds)
        {
            int f = Mathf.Min((int)(fadeSeconds * Rate), x.Length / 3);
            int n = x.Length - f;
            var y = new float[n];
            Array.Copy(x, y, n);
            for (int i = 0; i < f; i++)
            {
                float t = i / (float)f;
                y[i] = x[i] * t + x[n + i] * (1f - t);
            }
            return y;
        }

        private static float Env(float t, float attack, float length, float release)
        {
            if (t < 0f || t > length) return 0f;
            float a = attack > 0f ? Mathf.Clamp01(t / attack) : 1f;
            float r = release > 0f ? Mathf.Clamp01((length - t) / release) : 1f;
            return a * r;
        }

        /// <summary>Band-pass biquad (RBJ, constant peak gain) — used for formants and resonances.</summary>
        private struct BandPass
        {
            private float _b0, _b2, _a1, _a2, _x1, _x2, _y1, _y2;

            public void Set(float hz, float q)
            {
                float w = 2f * Mathf.PI * Mathf.Clamp(hz, 20f, Rate * 0.45f) / Rate;
                float alpha = Mathf.Sin(w) / (2f * q);
                float a0 = 1f + alpha;
                _b0 = alpha / a0;
                _b2 = -alpha / a0;
                _a1 = -2f * Mathf.Cos(w) / a0;
                _a2 = (1f - alpha) / a0;
            }

            public float Tick(float x)
            {
                float y = _b0 * x + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
                _x2 = _x1; _x1 = x; _y2 = _y1; _y1 = y;
                return y;
            }
        }

        // ------------------------------------------------------------------ ambiences

        private static float[] TrafficBed(float seconds)
        {
            float[] x = Buf(seconds);
            float brown = 0f, lp1 = 0f, lp2 = 0f;
            float a = Lp(260f), b = Lp(900f);
            float[] engines = { 42f, 57f, 71f, 88f };
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                brown = Mathf.Clamp(brown + Noise() * 0.02f, -1f, 1f) * 0.999f;
                lp1 += a * (brown - lp1);
                float hiss = Noise();
                lp2 += b * (hiss - lp2);
                float eng = 0f;
                for (int k = 0; k < engines.Length; k++)
                    eng += Mathf.Sin(2f * Mathf.PI * engines[k] * t + k) * (0.5f + 0.5f * Mathf.Sin(t * (0.3f + k * 0.17f) + k * 2f));
                // Passing vehicles: slow swells
                float swell = 0.6f + 0.4f * Mathf.Sin(t * 0.9f) * Mathf.Sin(t * 0.37f + 1f);
                x[i] = (lp1 * 3f + lp2 * 0.25f + eng * 0.05f) * swell;
            }
            return Normalise(x, 0.8f);
        }

        private static float[] Rain(float seconds)
        {
            float[] x = Buf(seconds);
            float lp = 0f, lp2 = 0f, hp = 0f, prev = 0f, a = Lp(3200f), a2 = Lp(1500f);
            float drop = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float n = Noise();
                lp += a * (n - lp);
                lp2 += a2 * (lp - lp2);                // softer, "pinker" body
                hp = 0.97f * (hp + lp2 - prev);        // high-pass ~200 Hz
                prev = lp2;
                if (_rng.NextDouble() < 120.0 / Rate) drop = (float)_rng.Range(0.3, 1.0); // droplet ticks
                drop *= 0.992f;
                x[i] = hp * 0.7f + (lp - lp2) * 0.35f + drop * lp * 0.6f;
            }
            return Normalise(x, 0.7f);
        }

        private static float[] Wind(float seconds)
        {
            float[] x = Buf(seconds);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                float gust = 0.55f + 0.45f * Mathf.Sin(t * 0.8f) * Mathf.Sin(t * 0.31f + 2f);
                float cutoff = Mathf.Lerp(250f, 900f, gust);
                lp += Lp(cutoff) * (Noise() - lp);
                lp2 += Lp(cutoff * 0.7f) * (lp - lp2);
                x[i] = lp2 * gust;
            }
            return Normalise(x, 0.7f);
        }

        private static float[] Thunder()
        {
            float[] x = Buf(6f);
            float brown = 0f, lp = 0f, a = Lp(140f);
            float[] swells = { 0.25f, 1.1f, 2.0f, 3.1f };
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                brown = Mathf.Clamp(brown + Noise() * 0.05f, -1f, 1f) * 0.998f;
                lp += a * (brown - lp);
                float env = 0f;
                foreach (float s in swells) env += Mathf.Exp(-Mathf.Max(0f, t - s) * 1.6f) * Mathf.Clamp01((t - s) * 8f);
                float crack = t < 0.18f ? Noise() * Mathf.Exp(-t * 25f) * 0.6f : 0f;
                x[i] = lp * env * 4f + crack;
            }
            return Normalise(x, 0.95f);
        }

        /// <summary>Speech-like murmur: glottal pulse trains through moving vowel formants.</summary>
        private static float[] Babble(float seconds, int voices, float voiceGain, float bandTop)
        {
            float[] x = Buf(seconds);
            float[,] vowels = { { 700, 1220 }, { 500, 1900 }, { 300, 2300 }, { 520, 900 }, { 350, 800 }, { 600, 1700 } };
            for (int v = 0; v < voices; v++)
            {
                float f0 = (float)_rng.Range(95.0, 230.0);
                float phase = 0f;
                var f1 = new BandPass(); var f2 = new BandPass();
                int vowel = _rng.Range(0, 6);
                float sylLen = (float)_rng.Range(0.12, 0.28), sylT = 0f, sylAmp = 1f;
                bool pause = false;
                f1.Set(vowels[vowel, 0], 6f); f2.Set(vowels[vowel, 1], 8f);
                for (int i = 0; i < x.Length; i++)
                {
                    sylT += 1f / Rate;
                    if (sylT > sylLen)
                    {
                        sylT = 0f;
                        sylLen = (float)_rng.Range(0.1, 0.3);
                        pause = _rng.Chance(0.18);
                        vowel = _rng.Range(0, 6);
                        sylAmp = (float)_rng.Range(0.5, 1.0);
                        f1.Set(vowels[vowel, 0] * (f0 > 160f ? 1.15f : 1f), 6f);
                        f2.Set(vowels[vowel, 1] * (f0 > 160f ? 1.15f : 1f), 8f);
                    }
                    float jitter = 1f + 0.03f * Mathf.Sin(i * 0.0007f + v);
                    phase += f0 * jitter / Rate;
                    if (phase > 1f) phase -= 1f;
                    float pulse = (1f - phase) * (1f - phase) - 0.33f; // glottal-ish
                    float src = pulse + Noise() * 0.08f;
                    float env = pause ? 0f : Mathf.Sin(Mathf.PI * Mathf.Clamp01(sylT / sylLen)) * sylAmp;
                    x[i] += (f1.Tick(src) + 0.6f * f2.Tick(src)) * env * voiceGain;
                }
            }
            // Gentle band limit
            float lp = 0f, a = Lp(bandTop);
            for (int i = 0; i < x.Length; i++) { lp += a * (x[i] - lp); x[i] = lp; }
            return Normalise(x, 0.6f);
        }

        private static float[] Crickets(float seconds)
        {
            float[] x = Buf(seconds);
            int crickets = 3;
            for (int c = 0; c < crickets; c++)
            {
                float freq = 4300f + c * 380f;
                float period = seconds / (8 + c * 2);       // integer number of chirp trains per loop
                float offset = (float)_rng.Range(0.0, period);
                for (int i = 0; i < x.Length; i++)
                {
                    float t = i / (float)Rate;
                    float tt = Mathf.Repeat(t + offset, period);
                    float pulses = tt < 0.12f ? Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 33f * tt)) : 0f;
                    x[i] += Mathf.Sin(2f * Mathf.PI * freq * t) * pulses * (0.5f + 0.2f * c);
                }
            }
            return Normalise(x, 0.4f);
        }

        private static float[] HubHum(float seconds)
        {
            // 4 s contains whole cycles of every partial, so the loop is seamless
            float[] x = Buf(seconds);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                float v = Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.8f
                        + Mathf.Sin(2f * Mathf.PI * 150f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 300f * t) * 0.15f
                        + Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.04f * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.5f * t));
                x[i] = v * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.25f * t));
            }
            return Normalise(x, 0.5f);
        }

        private static float[] TrainRumble(float seconds)
        {
            float[] x = Buf(seconds);
            float brown = 0f, lp = 0f, a = Lp(200f);
            float clack = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                brown = Mathf.Clamp(brown + Noise() * 0.03f, -1f, 1f) * 0.999f;
                lp += a * (brown - lp);
                // Wheel joints: "ta-dum ... ta-dum"
                float bar = Mathf.Repeat(t, 0.75f);
                if (Mathf.Abs(bar - 0f) < 1f / Rate || Mathf.Abs(bar - 0.11f) < 1f / Rate) clack = 1f;
                clack *= 0.9985f;
                x[i] = lp * 3f + clack * Noise() * 0.35f + clack * Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.3f;
            }
            return Normalise(x, 0.75f);
        }

        // ------------------------------------------------------------------ one-shots

        private static float[] Horn(float[] freqs, float length, int beeps, float gap, float peak, float attack = 0.012f)
        {
            float total = beeps * length + (beeps - 1) * gap + 0.05f;
            float[] x = Buf(total);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                float local = Mathf.Repeat(t, length + gap);
                int beep = Mathf.FloorToInt(t / (length + gap));
                if (beep >= beeps) continue;
                float env = Env(local, attack, length, 0.04f);
                if (env <= 0f) continue;
                float v = 0f;
                foreach (float f in freqs)
                {
                    float ph = 2f * Mathf.PI * f * t * (1f + 0.004f * Mathf.Sin(t * 30f));
                    v += Mathf.Sin(ph) + 0.45f * Mathf.Sin(2f * ph) + 0.25f * Mathf.Sin(3f * ph) + 0.12f * Mathf.Sin(5f * ph);
                }
                x[i] = (float)Math.Tanh(v * 0.9f) * env;
            }
            return Normalise(x, peak);
        }

        /// <summary>Auto-rickshaw rubber bulb horn: "pom-pom" with a pitch drop.</summary>
        private static float[] BulbHorn()
        {
            float[] x = Buf(0.55f);
            float phase = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                if (t >= 0.52f) break; // exactly two "pom"s
                float local = Mathf.Repeat(t, 0.26f);
                float f = Mathf.Lerp(360f, 270f, local / 0.2f);
                phase += f / Rate;
                float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
                x[i] = saw * Env(local, 0.01f, 0.2f, 0.06f);
            }
            float lp = 0f, a = Lp(1800f);
            for (int i = 0; i < x.Length; i++) { lp += a * (x[i] - lp); x[i] = lp; }
            return Normalise(x, 0.5f);
        }

        /// <summary>Decorated goods-truck "Horn OK Please" musical horn (a little rising three-note tune).</summary>
        private static float[] TruckHorn()
        {
            float[] notes = { 392f, 440f, 523.3f, 440f };
            float noteLen = 0.17f;
            float[] x = Buf(notes.Length * noteLen + 0.1f);
            float phase = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                int n = Mathf.Min(notes.Length - 1, Mathf.FloorToInt(t / noteLen));
                phase += notes[n] / Rate;
                float ph = 2f * Mathf.PI * phase;
                float v = Mathf.Sin(ph) + 0.6f * Mathf.Sin(2f * ph) + 0.4f * Mathf.Sin(3f * ph) + 0.2f * Mathf.Sin(4f * ph);
                x[i] = (float)Math.Tanh(v * 0.8f) * Env(t, 0.02f, notes.Length * noteLen, 0.06f);
            }
            return Normalise(x, 0.7f);
        }

        private static float[] Chime(float[] notes, float spacing, float length)
        {
            float[] x = Buf(length + notes.Length * spacing);
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * spacing * Rate);
                for (int i = start; i < x.Length; i++)
                {
                    float t = (i - start) / (float)Rate;
                    float env = Mathf.Exp(-t * 2.2f) * Mathf.Clamp01(t * 200f);
                    float f = notes[n];
                    x[i] += (Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * f * 2.76f * t) * Mathf.Exp(-t * 4f)
                           + 0.15f * Mathf.Sin(2f * Mathf.PI * f * 5.4f * t) * Mathf.Exp(-t * 7f)) * env;
                }
            }
            return Normalise(x, 0.6f);
        }

        /// <summary>Station public-address voice: a single talker, band-limited and with a hall echo.</summary>
        private static float[] PaVoice(float seconds)
        {
            float[] v = Babble(seconds, 1, 1f, 3400f);
            float[] x = new float[v.Length + Rate];
            // Telephone band (300–3400 Hz) and a hall echo
            float hp = 0f, prev = 0f;
            for (int i = 0; i < v.Length; i++)
            {
                hp = 0.96f * (hp + v[i] - prev);
                prev = v[i];
                x[i] += (float)Math.Tanh(hp * 2.5f);
            }
            int[] taps = { (int)(0.11f * Rate), (int)(0.23f * Rate), (int)(0.37f * Rate) };
            float[] gains = { 0.45f, 0.3f, 0.18f };
            for (int i = x.Length - 1; i >= 0; i--)
                for (int k = 0; k < taps.Length; k++)
                    if (i - taps[k] >= 0) x[i] += x[i - taps[k]] * gains[k];
            return Normalise(x, 0.6f);
        }

        private static float[] Chirp()
        {
            float[] x = Buf(0.32f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                float local = Mathf.Repeat(t, 0.16f);
                float f = Mathf.Lerp(1800f, 2500f, local / 0.1f);
                x[i] = Mathf.Sin(2f * Mathf.PI * f * local) * Env(local, 0.005f, 0.1f, 0.03f);
            }
            return Normalise(x, 0.4f);
        }

        private static float[] Crow()
        {
            float[] x = Buf(1.1f);
            float phase = 0f;
            var formant = new BandPass();
            formant.Set(1300f, 3f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                float local = Mathf.Repeat(t, 0.36f);
                int caw = Mathf.FloorToInt(t / 0.36f);
                if (caw > 2) break;
                float f = Mathf.Lerp(620f, 470f, local / 0.28f);
                phase += f / Rate;
                float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
                float src = saw + Noise() * 0.6f;
                x[i] = formant.Tick(src) * Env(local, 0.02f, 0.28f, 0.08f);
            }
            return Normalise(x, 0.55f);
        }

        /// <summary>
        /// Ganpati procession drums: a dhol (deep "dha" on the bass head, sharp "tin" on the treble head)
        /// in a driving 4-beat pattern, with tasha snare rolls on top. Exactly 4 bars at 120 BPM, so it loops cleanly.
        /// </summary>
        private static float[] DholLoop()
        {
            const float beat = 0.5f;
            float[] x = Buf(16 * beat);
            // 16th-note grid per bar: 'D' bass, 't' treble, '.' rest
            string pattern = "D..tD.t.D..tDtt." + "D..tD.t.D..tD.tt" + "D..tD.t.D..tDtt." + "D.tDD.tDDtDtDttt";
            float step = beat / 4f;
            for (int k = 0; k < pattern.Length; k++)
            {
                char c = pattern[k];
                int start = (int)(k * step * Rate);
                if (c == 'D') Hit(x, start, 0.45f, 78f, 1f, 9f, 0.15f);
                else if (c == 't') Hit(x, start, 0.12f, 340f, 0.55f, 35f, 0.6f);
                // Tasha: fast snare roll on every off-beat 16th
                if (k % 2 == 1) Snare(x, start, 0.07f, 0.25f);
            }
            return Normalise(x, 0.8f);
        }

        private static void Hit(float[] x, int start, float length, float freq, float amp, float decay, float noise)
        {
            int n = (int)(length * Rate);
            for (int i = 0; i < n && start + i < x.Length; i++)
            {
                float t = i / (float)Rate;
                float f = freq * (1f + 0.6f * Mathf.Exp(-t * 40f)); // pitch drop on attack
                x[start + i] += (Mathf.Sin(2f * Mathf.PI * f * t) + Noise() * noise * Mathf.Exp(-t * 60f)) * amp * Mathf.Exp(-t * decay);
            }
        }

        private static void Snare(float[] x, int start, float length, float amp)
        {
            int n = (int)(length * Rate);
            float hp = 0f, prev = 0f;
            for (int i = 0; i < n && start + i < x.Length; i++)
            {
                float t = i / (float)Rate;
                float v = Noise();
                hp = 0.9f * (hp + v - prev);
                prev = v;
                x[start + i] += hp * amp * Mathf.Exp(-t * 55f);
            }
        }

        /// <summary>Firework: rising whistle, a deep boom and a tail of crackles.</summary>
        private static float[] Firework()
        {
            float[] x = Buf(3.2f);
            float phase = 0f;
            for (int i = 0; i < (int)(0.7f * Rate); i++)
            {
                float t = i / (float)Rate;
                phase += Mathf.Lerp(1200f, 2600f, t / 0.7f) / Rate;
                x[i] += Mathf.Sin(2f * Mathf.PI * phase) * 0.18f * Mathf.Clamp01(t * 10f);
            }
            int boom = (int)(0.75f * Rate);
            float brown = 0f, lp = 0f, a = Lp(220f);
            for (int i = boom; i < x.Length; i++)
            {
                float t = (i - boom) / (float)Rate;
                brown = Mathf.Clamp(brown + Noise() * 0.08f, -1f, 1f) * 0.995f;
                lp += a * (brown - lp);
                x[i] += (lp * 6f + Noise() * 0.5f * Mathf.Exp(-t * 30f)) * Mathf.Exp(-t * 2.5f);
                if (t > 0.25f && _rng.NextDouble() < 260.0 / Rate * Mathf.Exp(-(t - 0.25f) * 1.2f))
                    for (int k = 0; k < 220 && i + k < x.Length; k++) x[i + k] += Noise() * 0.35f * Mathf.Exp(-k / 40f);
            }
            return Normalise(x, 0.9f);
        }

        private static float[] Beep(float hz, float length)
        {
            float[] x = Buf(length);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                x[i] = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * hz * t)) * 0.5f * Env(t, 0.002f, length, 0.01f);
            }
            return Normalise(x, 0.35f);
        }
    }
}
