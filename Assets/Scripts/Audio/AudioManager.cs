using System.Collections.Generic;
using UnityEngine;

namespace BombaBlast
{
    public enum Sfx
    {
        BombPlace, Explosion, CrateBreak, PowerUp, EnemyHit, EnemyDie, PlayerDie, MenuMove, MenuSelect, MenuBack,
        ExitOpen, StageStart, LevelClear, GameOver, Victory, TimeWarning, Shield, Detonate, Tally, Pause
    }

    public enum Music { None, Title, Meadow, Desert, Ruins, Volcano }

    /// <summary>Gestor de audio: música en bucle y efectos, todo sintetizado.</summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        AudioSource musicSrc;
        readonly List<AudioSource> sfxSrcs = new List<AudioSource>();
        readonly Dictionary<Sfx, AudioClip> sfx = new Dictionary<Sfx, AudioClip>();
        readonly Dictionary<Music, AudioClip> music = new Dictionary<Music, AudioClip>();
        Music current = Music.None;
        float musicFade = 1f, musicTarget = 1f;

        public float MusicVolume { get; private set; }
        public float SfxVolume { get; private set; }

        void Awake()
        {
            I = this;
            MusicVolume = PlayerPrefs.GetFloat("musicVol", 0.6f);
            SfxVolume = PlayerPrefs.GetFloat("sfxVol", 0.8f);
            musicSrc = gameObject.AddComponent<AudioSource>();
            musicSrc.loop = true;
            musicSrc.playOnAwake = false;
            for (int i = 0; i < 12; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                sfxSrcs.Add(s);
            }
            BuildSfx();
            BuildMusic();
        }

        public void SetMusicVolume(float v)
        {
            MusicVolume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat("musicVol", MusicVolume);
        }

        public void SetSfxVolume(float v)
        {
            SfxVolume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat("sfxVol", SfxVolume);
        }

        void Update()
        {
            musicFade = Mathf.MoveTowards(musicFade, musicTarget, Time.unscaledDeltaTime * 1.5f);
            musicSrc.volume = MusicVolume * musicFade * 0.55f;
        }

        public void Play(Sfx s, float volume = 1f, float pitch = 1f)
        {
            if (!sfx.TryGetValue(s, out var clip)) return;
            AudioSource src = null;
            foreach (var a in sfxSrcs) if (!a.isPlaying) { src = a; break; }
            if (src == null) src = sfxSrcs[Random.Range(0, sfxSrcs.Count)];
            src.pitch = pitch;
            src.clip = clip;
            src.volume = volume * SfxVolume;
            src.Play();
        }

        public void PlayMusic(Music m, bool restart = false)
        {
            if (m == current && !restart && musicSrc.isPlaying) return;
            current = m;
            musicSrc.pitch = 1f;
            if (m == Music.None || !music.ContainsKey(m)) { musicSrc.Stop(); return; }
            musicSrc.clip = music[m];
            musicSrc.Play();
            musicFade = 0f; musicTarget = 1f;
        }

        public void StopMusic() { current = Music.None; musicSrc.Stop(); }
        public void SetMusicPitch(float p) => musicSrc.pitch = p;
        public void DuckMusic(bool duck) => musicTarget = duck ? 0.35f : 1f;

        // ------------------------------------------------------------- Efectos
        void BuildSfx()
        {
            float[] b;

            b = Synth.Buffer(0.12f);
            Synth.AddTone(b, 0, 0.1f, 330, 180, Wave.Square, 0.35f);
            Synth.AddNoise(b, 0, 0.05f, 0.15f, 2f, 0.5f, 0.2f);
            sfx[Sfx.BombPlace] = Synth.ToClip("bomb", b);

            b = Synth.Buffer(1.1f);
            Synth.AddNoise(b, 0, 1.05f, 0.9f, 2.5f, 0.9f, 0.04f);
            Synth.AddTone(b, 0, 0.45f, 110, 28, Wave.Sine, 0.9f, 0.002f, 0.2f);
            Synth.AddTone(b, 0, 0.18f, 220, 50, Wave.Square, 0.25f);
            sfx[Sfx.Explosion] = Synth.ToClip("explosion", b, true);

            b = Synth.Buffer(0.3f);
            Synth.AddNoise(b, 0, 0.25f, 0.6f, 3f, 0.7f, 0.2f);
            Synth.AddTone(b, 0, 0.12f, 180, 90, Wave.Square25, 0.25f);
            sfx[Sfx.CrateBreak] = Synth.ToClip("crate", b);

            b = Synth.Buffer(0.5f);
            string[] up = { "C5", "E5", "G5", "C6", "E6", "G6" };
            for (int i = 0; i < up.Length; i++)
                Synth.AddTone(b, i * 0.06f, 0.1f, Synth.Freq(up[i]), Synth.Freq(up[i]), Wave.Square25, 0.3f);
            sfx[Sfx.PowerUp] = Synth.ToClip("powerup", b);

            b = Synth.Buffer(0.2f);
            Synth.AddTone(b, 0, 0.18f, 900, 300, Wave.Square, 0.3f);
            sfx[Sfx.EnemyHit] = Synth.ToClip("enemyhit", b);

            b = Synth.Buffer(0.45f);
            Synth.AddTone(b, 0, 0.35f, 700, 80, Wave.Square25, 0.35f, 0.002f, 0.1f, 0.05f);
            Synth.AddNoise(b, 0.05f, 0.35f, 0.3f, 2f, 0.6f, 0.1f);
            sfx[Sfx.EnemyDie] = Synth.ToClip("enemydie", b);

            b = Synth.Buffer(1.4f);
            string[] down = { "B5", "A#5", "A5", "G#5", "G5", "F#5", "F5", "E5" };
            for (int i = 0; i < down.Length; i++)
                Synth.AddTone(b, i * 0.08f, 0.1f, Synth.Freq(down[i]), Synth.Freq(down[i]) * 0.97f, Wave.Square, 0.3f);
            Synth.AddTone(b, 0.68f, 0.7f, 400, 60, Wave.Triangle, 0.5f, 0.01f, 0.2f, 0.06f);
            sfx[Sfx.PlayerDie] = Synth.ToClip("playerdie", b);

            b = Synth.Buffer(0.06f);
            Synth.AddTone(b, 0, 0.05f, 880, 880, Wave.Square25, 0.25f);
            sfx[Sfx.MenuMove] = Synth.ToClip("menumove", b);

            b = Synth.Buffer(0.2f);
            Synth.AddTone(b, 0, 0.07f, 660, 660, Wave.Square, 0.25f);
            Synth.AddTone(b, 0.07f, 0.12f, 1320, 1320, Wave.Square, 0.25f);
            sfx[Sfx.MenuSelect] = Synth.ToClip("menuselect", b);

            b = Synth.Buffer(0.2f);
            Synth.AddTone(b, 0, 0.07f, 660, 660, Wave.Square, 0.25f);
            Synth.AddTone(b, 0.07f, 0.12f, 440, 440, Wave.Square, 0.25f);
            sfx[Sfx.MenuBack] = Synth.ToClip("menuback", b);

            b = Synth.Buffer(0.9f);
            Synth.AddTone(b, 0, 0.8f, 200, 1200, Wave.Triangle, 0.4f, 0.01f, 0.2f, 0.03f);
            Synth.AddTone(b, 0.1f, 0.7f, 300, 1800, Wave.Square12, 0.15f, 0.01f, 0.2f);
            sfx[Sfx.ExitOpen] = Synth.ToClip("exitopen", b);

            b = Synth.Buffer(1.6f);
            Synth.AddLine(b, "C5 E5 G5 C6 - - E6 - G6 - - - - -", 0.09f, Wave.Square25, 0.25f);
            Synth.AddLine(b, "C3 . . . G3 . . . C4 - - - - -", 0.09f, Wave.Triangle, 0.4f);
            sfx[Sfx.StageStart] = Synth.ToClip("stagestart", b);

            b = Synth.Buffer(2.4f);
            Synth.AddLine(b, "G4 C5 E5 G5 - E5 G5 - C6 - - - D6 - E6 - - - - -", 0.1f, Wave.Square25, 0.25f);
            Synth.AddLine(b, "E4 G4 C5 E5 - C5 E5 - G5 - - - B5 - C6 - - - - -", 0.1f, Wave.Square12, 0.12f);
            Synth.AddLine(b, "C3 . G3 . C3 . G3 . C3 - - - G2 - C3 - - - - -", 0.1f, Wave.Triangle, 0.4f);
            sfx[Sfx.LevelClear] = Synth.ToClip("levelclear", b);

            b = Synth.Buffer(3.2f);
            Synth.AddLine(b, "G4 - - F#4 - - F4 - - E4 - - - - - - . . D#4 - - - - - - - -", 0.11f, Wave.Square, 0.22f, 0, 1, 0.1f, 0.012f);
            Synth.AddLine(b, "C3 - - B2 - - A#2 - - A2 - - - - - - . . G#2 - - - - - - - -", 0.11f, Wave.Triangle, 0.4f);
            sfx[Sfx.GameOver] = Synth.ToClip("gameover", b);

            b = Synth.Buffer(4.2f);
            string vm = "G4 C5 E5 G5 - - E5 G5 C6 - - - . A5 B5 C6 - D6 - E6 - - - . D6 C6 B5 C6 - - - - - - -";
            Synth.AddLine(b, vm, 0.1f, Wave.Square25, 0.25f);
            Synth.AddLine(b, "C3 . G3 . C3 . G3 . C3 . G3 . F2 . C3 . F2 . C3 . G2 . D3 . G2 C3 - - - - - - -", 0.1f, Wave.Triangle, 0.4f);
            sfx[Sfx.Victory] = Synth.ToClip("victory", b);

            b = Synth.Buffer(0.3f);
            Synth.AddTone(b, 0, 0.1f, 1200, 1200, Wave.Square, 0.25f);
            Synth.AddTone(b, 0.15f, 0.1f, 1200, 1200, Wave.Square, 0.25f);
            sfx[Sfx.TimeWarning] = Synth.ToClip("timewarn", b);

            b = Synth.Buffer(0.5f);
            Synth.AddTone(b, 0, 0.45f, 300, 900, Wave.Sine, 0.4f, 0.01f, 0.2f, 0.08f);
            Synth.AddTone(b, 0, 0.45f, 600, 1800, Wave.Square12, 0.1f, 0.01f, 0.2f);
            sfx[Sfx.Shield] = Synth.ToClip("shield", b);

            b = Synth.Buffer(0.15f);
            Synth.AddTone(b, 0, 0.05f, 1500, 1500, Wave.Square25, 0.25f);
            Synth.AddTone(b, 0.06f, 0.06f, 2000, 2000, Wave.Square25, 0.25f);
            sfx[Sfx.Detonate] = Synth.ToClip("detonate", b);

            b = Synth.Buffer(0.04f);
            Synth.AddTone(b, 0, 0.03f, 1760, 1760, Wave.Square25, 0.18f);
            sfx[Sfx.Tally] = Synth.ToClip("tally", b);

            b = Synth.Buffer(0.25f);
            Synth.AddTone(b, 0, 0.08f, 988, 988, Wave.Square25, 0.25f);
            Synth.AddTone(b, 0.1f, 0.12f, 1319, 1319, Wave.Square25, 0.25f);
            sfx[Sfx.Pause] = Synth.ToClip("pause", b);
        }

        // -------------------------------------------------------------- Música
        void BuildMusic()
        {
            music[Music.Title] = Song("title", 132,
                "E5 . E5 G5 C6 - B5 G5 | A5 - F5 A5 C6 - A5 F5 | G5 - E5 G5 B5 - D6 B5 | C6 - - - G5 - . . |" +
                "E5 . E5 G5 C6 - B5 G5 | A5 - F5 A5 C6 - D6 E6 | D6 - B5 G5 F5 - D5 B4 | C5 - - - . . . .",
                "C3 . C3 . G2 . C3 . | F2 . F2 . C3 . F2 . | G2 . G2 . D3 . G2 . | C3 . G2 . C3 . E3 . |" +
                "C3 . C3 . G2 . C3 . | F2 . F2 . A2 . F2 . | G2 . G2 . B2 . G2 . | C3 . G2 . C3 . . .",
                "k.h.s.h.k.k.s.h.", Wave.Square25, 1f);

            music[Music.Meadow] = Song("meadow", 150,
                "A4 . C5 E5 A5 - G5 E5 | F5 - E5 D5 E5 - . . | A4 . C5 E5 A5 - B5 C6 | B5 - G5 - E5 - . . |" +
                "F5 . F5 A5 C6 - A5 F5 | G5 . G5 B5 D6 - B5 G5 | E5 - A5 - G#5 - B5 - | A5 - - - . . E5 .",
                "A2 . A3 . A2 . A3 . | D3 . D2 . D3 . D2 . | A2 . A3 . A2 . A3 . | E2 . E3 . E2 . E3 . |" +
                "F2 . F3 . F2 . F3 . | G2 . G3 . G2 . G3 . | E2 . E3 . E2 . E3 . | A2 . A3 . A2 . E3 .",
                "k.h.s.hkk.h.s.h.", Wave.Square25, 1f);

            music[Music.Desert] = Song("desert", 140,
                "D5 - F5 - A5 . G5 F5 | E5 - C5 - D5 - . . | D5 - F5 - A5 . C6 A5 | G5 - - - . . . . |" +
                "A#4 - D5 - F5 . E5 D5 | C5 - E5 - G5 - . . | A5 . G5 . F5 . E5 . | D5 - - - . . A4 .",
                "D3 . D3 . D3 . C3 . | C3 . C3 . C3 . A2 . | D3 . D3 . D3 . F3 . | G2 . G2 . G2 . G2 . |" +
                "A#2 . A#2 . A#2 . A#2 . | C3 . C3 . C3 . C3 . | A2 . A2 . A2 . A2 . | D3 . D3 . A2 . D3 .",
                "k..hs.h.k.khs.h.", Wave.Square12, 1f);

            music[Music.Ruins] = Song("ruins", 144,
                "E5 . B4 . E5 F#5 G5 . | F#5 . D5 . B4 - - . | C5 . E5 . G5 A5 B5 . | A5 - G5 - F#5 - . . |" +
                "E5 . B4 . E5 F#5 G5 . | A5 . G5 . F#5 . D5 . | E5 . F#5 . G5 . A5 . | B5 - - - . . . .",
                "E2 . E3 . E2 . E3 . | B1 . B2 . B1 . B2 . | C2 . C3 . C2 . C3 . | D2 . D3 . D2 . D3 . |" +
                "E2 . E3 . E2 . E3 . | D2 . D3 . D2 . D3 . | C2 . C3 . D2 . D3 . | B1 . B2 . B1 . B2 .",
                "k.hhs.h.k.hhs.hs", Wave.Square25, 1f);

            music[Music.Volcano] = Song("volcano", 160,
                "E5 E5 . E5 F5 . E5 . | D5 D5 . D5 E5 . D5 . | C5 C5 . C5 D5 . E5 . | B4 - - - . . . . |" +
                "E5 E5 . E5 G5 . F5 . | E5 . D5 . C5 . B4 . | C5 . D5 . E5 . F5 . | E5 - - - B4 - - -",
                "E2 E3 E2 E3 E2 E3 E2 E3 | D2 D3 D2 D3 D2 D3 D2 D3 | C2 C3 C2 C3 C2 C3 C2 C3 | B1 B2 B1 B2 B1 B2 B1 B2 |" +
                "E2 E3 E2 E3 E2 E3 E2 E3 | D2 D3 D2 D3 D2 D3 D2 D3 | C2 C3 C2 C3 D2 D3 D2 D3 | B1 B2 B1 B2 B1 B2 B1 B2",
                "kkhskkhskkhskshs", Wave.Saw, 1f);
        }

        AudioClip Song(string name, float bpm, string melody, string bass, string drums, Wave leadWave, float leadVol)
        {
            float step = 60f / bpm / 2f;
            int steps = Synth.StepCount(melody);
            float len = steps * step;
            var lead = Synth.Buffer(len);
            Synth.AddLine(lead, melody, step, leadWave, 0.16f * leadVol, 0, 1f, 0.04f, 0.004f, 0.75f);
            // Contracanto a una octava inferior, muy suave
            Synth.AddLine(lead, melody, step, Wave.Triangle, 0.06f, 0, 0.5f);
            Synth.Echo(lead, step * 1.5f, 0.22f);
            var mix = Synth.Buffer(len);
            Synth.AddLine(mix, bass, step, Wave.Triangle, 0.32f, 0, 1f, 0.02f, 0f, 0.8f);
            Synth.AddDrums(mix, drums, step, Mathf.CeilToInt(steps / (float)drums.Length), 0.22f);
            for (int i = 0; i < mix.Length; i++) mix[i] += lead[i];
            return Synth.ToClip(name, mix, true);
        }
    }
}
