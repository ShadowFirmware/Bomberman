using System.Collections;
using UnityEngine;

namespace BombaBlast
{
    /// <summary>Máquina de estados principal: menús, niveles, vidas, puntuación y progreso.</summary>
    public class GameManager : MonoBehaviour
    {
        public enum State { Title, LevelSelect, Options, Instructions, Loading, StageIntro, Playing, Paused, Dying, LevelClear, GameOver, Victory }

        public static GameManager I { get; private set; }

        public State Current { get; private set; } = State.Title;
        public int Lives { get; private set; }
        public int Score { get; private set; }
        public int LevelIndex { get; private set; }
        public int HighScore { get; private set; }
        public int Unlocked { get; private set; }
        public bool ConfirmReset { get; private set; }

        const int StartLives = 3;
        Board board;
        PlayerStats stats, statsAtLevelStart;
        int scoreAtLevelStart;
        int levelSelectIndex;
        Camera cam;
        Vector3 camBase;
        bool busy;
        UIManager ui;

        // ------------------------------------------------------------ Arranque
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (I != null) return;
            var go = new GameObject("BombaBlast");
            go.AddComponent<AudioManager>();
            go.AddComponent<FXManager>();
            go.AddComponent<UIManager>();
            go.AddComponent<GameManager>();
        }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            HighScore = PlayerPrefs.GetInt("highScore", 0);
            Unlocked = Mathf.Clamp(PlayerPrefs.GetInt("unlocked", 1), 1, Levels.All.Length);
        }

        void Start()
        {
            ui = UIManager.I;
            cam = Camera.main;
            if (cam == null)
            {
                var cgo = new GameObject("Main Camera");
                cgo.tag = "MainCamera";
                cam = cgo.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.11f);
            camBase = new Vector3(0, 0, -10);
            cam.transform.position = camBase;
            OpenScreen(State.Title);
            StartCoroutine(Fade(1f, 0f, 0.6f));
        }

        // ------------------------------------------------------------ Pantallas
        public void OpenScreen(State s)
        {
            Time.timeScale = 1f;
            Current = s;
            ConfirmReset = false;
            switch (s)
            {
                case State.Title:
                    ui.titleHighScore.text = "RÉCORD: " + HighScore.ToString("000000");
                    ui.ShowOnly(ui.title);
                    ui.titleMenu.sel = 0;
                    AudioManager.I.PlayMusic(Music.Title);
                    break;
                case State.Options:
                    ui.ShowOnly(ui.options); ui.optionsMenu.sel = 0; break;
                case State.Instructions:
                    ui.ShowOnly(ui.instructions); ui.instructionsMenu.sel = 0; break;
            }
        }

        public void OpenLevelSelect()
        {
            Current = State.LevelSelect;
            levelSelectIndex = Mathf.Clamp(Unlocked - 1, 0, Levels.All.Length - 1);
            ui.ShowOnly(ui.levelSelect);
            RefreshLevelCards();
        }

        void RefreshLevelCards()
        {
            for (int i = 0; i < ui.levelCards.Count; i++)
            {
                bool sel = i == levelSelectIndex;
                bool locked = i >= Unlocked;
                ui.levelLocks[i].SetActive(locked);
                ui.levelCards[i].color = sel ? new Color(1f, 0.85f, 0.25f) : new Color(0.35f, 0.3f, 0.6f);
                ui.levelCards[i].rectTransform.localScale = Vector3.one * (sel ? 1.06f : 1f);
            }
        }

        public void HoverLevel(int i)
        {
            if (Current != State.LevelSelect || i == levelSelectIndex) return;
            levelSelectIndex = i;
            AudioManager.I.Play(Sfx.MenuMove);
            RefreshLevelCards();
        }

        public void PickLevel(int i)
        {
            if (Current != State.LevelSelect) return;
            if (i >= Unlocked) { AudioManager.I.Play(Sfx.MenuBack); ui.Toast("Nivel bloqueado: supera el anterior"); return; }
            AudioManager.I.Play(Sfx.MenuSelect);
            NewGame(i);
        }

        public void ResetProgress()
        {
            if (!ConfirmReset) { ConfirmReset = true; return; }
            ConfirmReset = false;
            PlayerPrefs.SetInt("unlocked", 1);
            PlayerPrefs.SetInt("highScore", 0);
            Unlocked = 1; HighScore = 0;
            ui.Toast("Progreso borrado");
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------ Partida
        public void NewGame(int level)
        {
            if (busy) return;
            Lives = StartLives;
            Score = 0;
            stats = new PlayerStats();
            StartCoroutine(LoadLevelRoutine(level, true));
        }

        IEnumerator LoadLevelRoutine(int level, bool fresh)
        {
            busy = true;
            Current = State.Loading;
            Time.timeScale = 1f;
            yield return Fade(ui.FadeAlpha, 1f, 0.35f);
            LevelIndex = level;
            if (fresh)
            {
                statsAtLevelStart = stats.Clone();
                scoreAtLevelStart = Score;
            }
            BuildBoard();

            // Intro del nivel
            Current = State.StageIntro;
            var def = Levels.All[level];
            ui.introNumber.text = "NIVEL " + (level + 1);
            ui.introName.text = def.name;
            ui.introSub.text = def.subtitle + "\n\nVidas: " + Lives;
            ui.ShowOnly(ui.stageIntro);
            AudioManager.I.StopMusic();
            yield return Fade(1f, 0f, 0.3f);
            AudioManager.I.Play(Sfx.StageStart);
            yield return new WaitForSecondsRealtime(2.2f);
            yield return Fade(0f, 1f, 0.3f);
            ui.ShowOnly(ui.hud);
            ui.UpdateHUD(this, board);
            yield return Fade(1f, 0f, 0.35f);
            ui.Toast("¡A JUGAR!");
            AudioManager.I.PlayMusic(def.music, true);
            Current = State.Playing;
            busy = false;
        }

        void BuildBoard()
        {
            DestroyBoard();
            var def = Levels.All[LevelIndex];
            var go = new GameObject("Board");
            board = go.AddComponent<Board>();
            stats = statsAtLevelStart.Clone();
            board.Build(def, stats);
            cam.backgroundColor = def.bgColor;
            FitCamera();
        }

        void DestroyBoard()
        {
            if (board != null) Destroy(board.gameObject);
            board = null;
            FXManager.I.Clear();
            ui.ClearTransient();
        }

        void FitCamera()
        {
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            const float hudFrac = 96f / 1080f;
            float sizeV = (board.H + 0.4f) / (1f - hudFrac) / 2f;
            float sizeH = (board.W + 0.6f) / (2f * aspect);
            cam.orthographicSize = Mathf.Max(sizeV, sizeH);
            float hudUnits = 2f * cam.orthographicSize * hudFrac;
            camBase = new Vector3(0, hudUnits * 0.5f, -10);
        }

        // ------------------------------------------------------------ Eventos del juego
        public void AddScore(int pts, Vector3? world)
        {
            Score += pts;
            if (world.HasValue) Popup(world.Value + Vector3.up * 0.5f, pts.ToString(), Color.white);
        }

        public void Popup(Vector3 world, string text, Color c) => ui.Popup(world, text, c);
        public void ShowToast(string msg) => ui.Toast(msg);
        public void FlashScreen(Color c, float a) => ui.Flash(c, a);

        public void OnPlayerDeathFinished()
        {
            if (Current != State.Playing) return;
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            busy = true;
            Current = State.Dying;
            Lives--;
            if (Lives > 0)
            {
                // Reaparecer en el mismo mapa: cajas, enemigos, potenciadores y tiempo se conservan
                yield return new WaitForSeconds(0.3f);
                board.RespawnPlayer();
                ui.Toast(Lives == 1 ? "¡Última vida!" : "Vidas restantes: " + Lives);
                AudioManager.I.PlayMusic(board.Def.music, true);
                if (board.TimeLeft <= 30f) AudioManager.I.SetMusicPitch(1.12f);
                Current = State.Playing;
                busy = false;
                yield break;
            }
            yield return new WaitForSeconds(0.3f);
            SaveHighScore();
            AudioManager.I.Play(Sfx.GameOver);
            ui.gameOverScore.text = "Puntuación: " + Score.ToString("000000") + "\nRécord: " + HighScore.ToString("000000");
            ui.gameOver.SetActive(true);
            ui.gameOverMenu.sel = 0;
            Current = State.GameOver;
            busy = false;
        }

        public void RetryAfterGameOver()
        {
            if (busy) return;
            Lives = StartLives;
            Score = 0;
            scoreAtLevelStart = 0;
            statsAtLevelStart = new PlayerStats();
            StartCoroutine(LoadLevelRoutine(LevelIndex, false));
        }

        public void OnLevelComplete()
        {
            StartCoroutine(LevelCompleteRoutine());
        }

        IEnumerator LevelCompleteRoutine()
        {
            busy = true;
            Current = State.LevelClear;
            AudioManager.I.StopMusic();
            AudioManager.I.Play(Sfx.LevelClear);
            FlashScreen(Color.white, 0.4f);
            yield return new WaitForSeconds(2f);

            if (LevelIndex + 1 >= Unlocked && LevelIndex + 1 < Levels.All.Length)
            {
                Unlocked = LevelIndex + 2;
                PlayerPrefs.SetInt("unlocked", Unlocked);
            }

            ui.ShowOnly(ui.hud, ui.levelClear);
            ui.clearMenu.entries[0].text.gameObject.SetActive(false);
            int timeBonus = Mathf.CeilToInt(board.TimeLeft) * 10;
            int lifeBonus = 0;
            int shown = 0;
            string Body(int tb) =>
                $"Tiempo restante:  {Mathf.CeilToInt(board.TimeLeft)} s\n" +
                $"Bonificación de tiempo:  <color=#FFD94A>{tb}</color>\n\n" +
                $"PUNTUACIÓN:  <color=#FFD94A>{Score:000000}</color>";
            ui.clearBody.text = Body(0);
            yield return new WaitForSeconds(0.5f);
            // Conteo animado
            while (shown < timeBonus)
            {
                int step = Mathf.Max(10, timeBonus / 60);
                int add = Mathf.Min(step, timeBonus - shown);
                shown += add; Score += add;
                ui.clearBody.text = Body(shown);
                AudioManager.I.Play(Sfx.Tally, 0.6f);
                yield return new WaitForSeconds(0.025f);
            }
            Score += lifeBonus;
            SaveHighScore();
            yield return new WaitForSeconds(0.4f);
            ui.clearMenu.entries[0].text.gameObject.SetActive(true);
            ui.clearMenu.sel = 0;
            busy = false;
        }

        public void ContinueAfterClear()
        {
            if (busy) return;
            if (LevelIndex + 1 >= Levels.All.Length)
            {
                StartCoroutine(VictoryRoutine());
                return;
            }
            stats = board.Player.Stats.Clone();
            statsAtLevelStart = stats.Clone();
            scoreAtLevelStart = Score;
            StartCoroutine(LoadLevelRoutine(LevelIndex + 1, false));
        }

        IEnumerator VictoryRoutine()
        {
            busy = true;
            yield return Fade(0f, 1f, 0.4f);
            DestroyBoard();
            Score += Lives * 1000;
            SaveHighScore();
            ui.victoryBody.text = "¡Has superado todos los niveles!\n\n" +
                $"Bonificación de vidas: {Lives} x 1000\nPUNTUACIÓN FINAL: <color=#FFD94A>{Score:000000}</color>\nRécord: {HighScore:000000}";
            ui.ShowOnly(ui.victory);
            ui.victoryMenu.sel = 0;
            Current = State.Victory;
            AudioManager.I.Play(Sfx.Victory);
            yield return Fade(1f, 0f, 0.5f);
            yield return new WaitForSeconds(3.5f);
            if (Current == State.Victory) AudioManager.I.PlayMusic(Music.Title);
            busy = false;
        }

        void SaveHighScore()
        {
            if (Score > HighScore)
            {
                HighScore = Score;
                PlayerPrefs.SetInt("highScore", HighScore);
                PlayerPrefs.Save();
            }
        }

        // ------------------------------------------------------------ Pausa
        void Pause()
        {
            Current = State.Paused;
            Time.timeScale = 0f;
            ui.pause.SetActive(true);
            ui.pauseMenu.sel = 0;
            AudioManager.I.Play(Sfx.Pause);
            AudioManager.I.DuckMusic(true);
        }

        public void Resume()
        {
            Current = State.Playing;
            Time.timeScale = 1f;
            ui.pause.SetActive(false);
            AudioManager.I.DuckMusic(false);
        }

        public void RestartLevelFromPause()
        {
            Time.timeScale = 1f;
            AudioManager.I.DuckMusic(false);
            ui.pause.SetActive(false);
            Score = scoreAtLevelStart;
            StartCoroutine(LoadLevelRoutine(LevelIndex, false));
        }

        public void BackToTitle()
        {
            StartCoroutine(BackToTitleRoutine());
        }

        IEnumerator BackToTitleRoutine()
        {
            busy = true;
            Current = State.Loading;
            Time.timeScale = 1f;
            AudioManager.I.DuckMusic(false);
            SaveHighScore();
            yield return Fade(ui.FadeAlpha, 1f, 0.35f);
            DestroyBoard();
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.11f);
            camBase = new Vector3(0, 0, -10);
            OpenScreen(State.Title);
            yield return Fade(1f, 0f, 0.4f);
            busy = false;
        }

        IEnumerator Fade(float from, float to, float dur)
        {
            float t = 0;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                ui.SetFade(Mathf.Lerp(from, to, t / dur));
                yield return null;
            }
            ui.SetFade(to);
        }

        // ------------------------------------------------------------ Bucle
        void Update()
        {
            if (board != null) FitCamera();
            if (cam != null) cam.transform.position = camBase + FXManager.I.ShakeOffset;

            switch (Current)
            {
                case State.Title:
                    if (!busy) ui.titleMenu.Update();
                    break;
                case State.Options:
                    ui.optionsMenu.Update();
                    if (GameInput.BackPressed) { AudioManager.I.Play(Sfx.MenuBack); OpenScreen(State.Title); }
                    break;
                case State.Instructions:
                    ui.instructionsMenu.Update();
                    if (GameInput.BackPressed) { AudioManager.I.Play(Sfx.MenuBack); OpenScreen(State.Title); }
                    break;
                case State.LevelSelect:
                    UpdateLevelSelect();
                    break;
                case State.Playing:
                    if (busy) break;
                    if (GameInput.PausePressed) { Pause(); break; }
                    board.Tick(Time.deltaTime);
                    ui.UpdateHUD(this, board);
                    break;
                case State.Dying:
                    if (board != null) { board.Tick(Time.deltaTime); ui.UpdateHUD(this, board); }
                    break;
                case State.Paused:
                    if (GameInput.PausePressed) { Resume(); break; }
                    ui.pauseMenu.Update();
                    break;
                case State.LevelClear:
                    if (board != null) board.Player.Tick(Time.deltaTime);
                    if (!busy) ui.clearMenu.Update();
                    break;
                case State.GameOver:
                    if (!busy) ui.gameOverMenu.Update();
                    break;
                case State.Victory:
                    ui.victoryMenu.Update();
                    break;
            }
        }

        void UpdateLevelSelect()
        {
            if (busy) return;
            var nav = GameInput.MenuNav();
            int n = Levels.All.Length;
            int prev = levelSelectIndex;
            if (nav.x != 0) levelSelectIndex = (levelSelectIndex + nav.x + n) % n;
            if (nav.y != 0) levelSelectIndex = (levelSelectIndex - nav.y * 3 + n * 3) % n;
            if (prev != levelSelectIndex) { AudioManager.I.Play(Sfx.MenuMove); RefreshLevelCards(); }
            if (GameInput.ConfirmPressed) PickLevel(levelSelectIndex);
            else if (GameInput.BackPressed) { AudioManager.I.Play(Sfx.MenuBack); OpenScreen(State.Title); }
        }

        void OnApplicationQuit() => PlayerPrefs.Save();
    }
}
