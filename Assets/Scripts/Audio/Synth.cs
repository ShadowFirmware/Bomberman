using System;
using System.Collections.Generic;
using UnityEngine;

namespace BombaBlast
{
    public enum Wave { Square, Square25, Square12, Triangle, Saw, Sine }

    /// <summary>Sintetizador chiptune: genera efectos y música como AudioClips en tiempo de ejecución.</summary>
    public static class Synth
    {
        public const int Rate = 22050;
        static System.Random rng = new System.Random(1234);

        static float Osc(Wave w, double phase)
        {
            double p = phase - Math.Floor(phase);
            switch (w)
            {
                case Wave.Square: return p < 0.5 ? 1f : -1f;
                case Wave.Square25: return p < 0.25 ? 1f : -1f;
                case Wave.Square12: return p < 0.125 ? 1f : -1f;
                case Wave.Triangle: return (float)(p < 0.5 ? (4 * p - 1) : (3 - 4 * p));
                case Wave.Saw: return (float)(2 * p - 1);
                default: return (float)Math.Sin(p * Math.PI * 2);
            }
        }

        /// <summary>Añade un tono con barrido de frecuencia y envolvente a un buffer.</summary>
        public static void AddTone(float[] buf, float start, float dur, float f0, float f1, Wave w, float vol,
            float attack = 0.005f, float release = 0.05f, float vibrato = 0f, float sustain = 1f)
        {
            int s0 = (int)(start * Rate);
            int n = (int)(dur * Rate);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx < 0) continue;
                if (idx >= buf.Length) break;
                float t = (float)i / Rate;
                float k = (float)i / n;
                float f = Mathf.Lerp(f0, f1, k);
                if (vibrato > 0) f *= 1f + vibrato * Mathf.Sin(t * 2f * Mathf.PI * 6f);
                phase += f / Rate;
                float env = 1f;
                if (t < attack) env = t / attack;
                float rem = dur - t;
                if (rem < release) env *= Mathf.Max(0, rem / release);
                env *= Mathf.Lerp(1f, sustain, k);
                buf[idx] += Osc(w, phase) * vol * env;
            }
        }

        /// <summary>Ruido con decaimiento y filtro paso-bajo (explosiones, percusión).</summary>
        public static void AddNoise(float[] buf, float start, float dur, float vol, float decayPow = 2f,
            float lowpassStart = 1f, float lowpassEnd = 1f, bool highpass = false)
        {
            int s0 = (int)(start * Rate);
            int n = (int)(dur * Rate);
            float last = 0f, lastIn = 0f;
            float held = 0f; int holdCount = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx < 0) continue;
                if (idx >= buf.Length) break;
                float k = (float)i / n;
                if (holdCount-- <= 0) { held = (float)(rng.NextDouble() * 2 - 1); holdCount = 1; }
                float a = Mathf.Lerp(lowpassStart, lowpassEnd, k);
                float v = last + a * (held - last);
                float o = v;
                if (highpass) { o = held - lastIn; lastIn = held; }
                last = v;
                float env = Mathf.Pow(1f - k, decayPow);
                buf[idx] += o * vol * env;
            }
        }

        public static AudioClip ToClip(string name, float[] data, bool normalize = false)
        {
            if (normalize)
            {
                float max = 0.0001f;
                foreach (var v in data) max = Mathf.Max(max, Mathf.Abs(v));
                float g = 0.9f / max;
                for (int i = 0; i < data.Length; i++) data[i] *= g;
            }
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static float[] Buffer(float seconds) => new float[(int)(seconds * Rate)];

        // ------------------------------------------------------------------ Notas
        static readonly Dictionary<string, int> NoteIdx = new Dictionary<string, int>
        {
            {"C",0},{"C#",1},{"D",2},{"D#",3},{"E",4},{"F",5},{"F#",6},{"G",7},{"G#",8},{"A",9},{"A#",10},{"B",11}
        };

        public static float Freq(string note)
        {
            int octPos = note.Length - 1;
            while (octPos > 0 && char.IsDigit(note[octPos - 1])) octPos--;
            string name = note.Substring(0, octPos);
            int oct = int.Parse(note.Substring(octPos));
            int midi = (oct + 1) * 12 + NoteIdx[name];
            return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        }

        static List<string> Tokens(string seq)
        {
            var list = new List<string>();
            foreach (var t in seq.Split(new[] { ' ', '|', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)) list.Add(t);
            return list;
        }

        /// <summary>Renderiza una línea melódica: notas ("C5"), silencios ("."), prolongaciones ("-").</summary>
        public static void AddLine(float[] buf, string seq, float stepDur, Wave w, float vol, float offset = 0f,
            float transpose = 1f, float release = 0.04f, float vibrato = 0f, float sustain = 0.7f)
        {
            var tk = Tokens(seq);
            for (int i = 0; i < tk.Count; i++)
            {
                string t = tk[i];
                if (t == "." || t == "-") continue;
                int len = 1;
                while (i + len < tk.Count && tk[i + len] == "-") len++;
                float f = Freq(t) * transpose;
                AddTone(buf, offset + i * stepDur, len * stepDur * 0.95f, f, f, w, vol, 0.004f, release, vibrato, sustain);
            }
        }

        public static int StepCount(string seq) => Tokens(seq).Count;

        public static void AddDrums(float[] buf, string pattern, float stepDur, int repeats, float vol)
        {
            int idx = 0;
            for (int r = 0; r < repeats; r++)
                foreach (char c in pattern)
                {
                    float t = idx * stepDur;
                    switch (c)
                    {
                        case 'k':
                            AddTone(buf, t, 0.13f, 150f, 40f, Wave.Sine, vol * 1.3f, 0.001f, 0.05f);
                            break;
                        case 's':
                            AddNoise(buf, t, 0.12f, vol * 0.7f, 2f, 0.6f, 0.3f);
                            AddTone(buf, t, 0.06f, 220f, 160f, Wave.Triangle, vol * 0.5f);
                            break;
                        case 'h':
                            AddNoise(buf, t, 0.035f, vol * 0.35f, 2f, 1f, 1f, true);
                            break;
                    }
                    idx++;
                }
        }

        /// <summary>Echo simple para dar profundidad a la melodía.</summary>
        public static void Echo(float[] buf, float delay, float feedback)
        {
            int d = (int)(delay * Rate);
            for (int i = d; i < buf.Length; i++) buf[i] += buf[i - d] * feedback;
        }
    }
}
