using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BombaBlast
{
    /// <summary>Entrada de menú navegable con teclado, gamepad y ratón.</summary>
    public class MenuEntry
    {
        public Text text;
        public Func<string> label;
        public Action select;
        public Action<int> leftRight;
        public bool enabled = true;
    }

    public class MenuList
    {
        public readonly List<MenuEntry> entries = new List<MenuEntry>();
        public int sel;
        float t;

        public void Update()
        {
            t += Time.unscaledDeltaTime;
            var nav = GameInput.MenuNav();
            if (nav.y != 0 && entries.Count > 0)
            {
                int start = sel;
                do { sel = (sel - nav.y + entries.Count) % entries.Count; } while (!entries[sel].enabled && sel != start);
                AudioManager.I.Play(Sfx.MenuMove);
            }
            if (nav.x != 0 && sel < entries.Count && entries[sel].leftRight != null)
            {
                entries[sel].leftRight(nav.x);
                AudioManager.I.Play(Sfx.MenuMove);
            }
            if (GameInput.ConfirmPressed) Activate(sel);
            Refresh();
        }

        public void Activate(int i)
        {
            if (i < 0 || i >= entries.Count || !entries[i].enabled) return;
            sel = i;
            AudioManager.I.Play(Sfx.MenuSelect);
            entries[i].select?.Invoke();
        }

        public void Refresh()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                string l = e.label != null ? e.label() : e.text.text;
                bool s = i == sel;
                e.text.text = s ? "►  " + l + "  ◄" : l;
                e.text.color = !e.enabled ? new Color(1, 1, 1, 0.3f) : s ? new Color(1f, 0.85f, 0.25f) : new Color(1, 1, 1, 0.85f);
                float sc = s ? 1.08f + 0.03f * Mathf.Sin(t * 6f) : 1f;
                e.text.rectTransform.localScale = Vector3.one * sc;
            }
        }
    }

    /// <summary>Construye y gestiona todas las pantallas (uGUI creado por código).</summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager I { get; private set; }
        public Font font;
        Canvas canvas;
        RectTransform root;

        // Pantallas
        public GameObject title, levelSelect, options, instructions, stageIntro, hud, pause, levelClear, gameOver, victory;
        public MenuList titleMenu, optionsMenu, pauseMenu, gameOverMenu, victoryMenu, instructionsMenu, clearMenu;

        // Intro de nivel
        public Text introNumber, introName, introSub;
        // HUD
        public Text hudLives, hudBombs, hudFire, hudSpeed, hudTime, hudScore, hudLevel, hudEnemies, hudShield;
        public GameObject hudRemoteGO, hudShieldGO;
        Image hudClock;
        // Fin de nivel
        public Text clearTitle, clearBody;
        public Text gameOverScore, victoryBody, titleHighScore;
        // Selección de nivel
        public readonly List<Image> levelCards = new List<Image>();
        public readonly List<Text> levelCardTexts = new List<Text>();
        public readonly List<GameObject> levelLocks = new List<GameObject>();

        // Efectos de UI
        Image flash, fade;
        float flashAlpha;
        Text toast;
        float toastTime;
        readonly List<(Text t, Vector3 world, float age)> popups = new List<(Text, Vector3, float)>();
        readonly List<(RectTransform rt, float speed, float phase, float y)> titleSprites = new List<(RectTransform, float, float, float)>();
        Text titleLogo, titleLogoShadow, pressStart;
        float uiTime;

        void Awake()
        {
            I = this;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var cgo = new GameObject("Canvas");
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            cgo.AddComponent<GraphicRaycaster>();
            root = cgo.GetComponent<RectTransform>();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(transform, false);
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            BuildHUD();
            BuildTitle();
            BuildLevelSelect();
            BuildOptions();
            BuildInstructions();
            BuildStageIntro();
            BuildPause();
            BuildLevelClear();
            BuildGameOver();
            BuildVictory();

            toast = MakeText(root, "", 44, Color.white, new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(1600, 70));
            AddOutline(toast, 3);
            flash = MakeImage(root, new Color(1, 1, 1, 0), Vector2.zero, Vector2.one);
            flash.raycastTarget = false;
            fade = MakeImage(root, new Color(0, 0, 0, 1), Vector2.zero, Vector2.one);
            fade.raycastTarget = false;
        }

        // ------------------------------------------------------------ Helpers
        public RectTransform MakeRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        public Image MakeImage(Transform parent, Color c, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rt = MakeRect(parent, "Image", anchorMin, anchorMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            return img;
        }

        public Image MakeIcon(Transform parent, Sprite s, Vector2 anchor, Vector2 pos, float height)
        {
            var go = new GameObject("Icon", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            float aspect = s.rect.width / s.rect.height;
            rt.sizeDelta = new Vector2(height * aspect, height);
            var img = go.AddComponent<Image>();
            img.sprite = s;
            img.raycastTarget = false;
            return img;
        }

        public Text MakeText(Transform parent, string txt, int size, Color c, Vector2 anchor, Vector2 pos, Vector2 box,
            TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = go.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.color = c; t.alignment = align; t.fontStyle = style;
            t.text = txt;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        public static void AddOutline(Graphic g, float d, Color? c = null)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = c ?? new Color(0, 0, 0, 0.9f);
            o.effectDistance = new Vector2(d, -d);
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, 0.6f);
            s.effectDistance = new Vector2(d * 1.5f, -d * 2f);
        }

        GameObject MakeScreen(string name, Color bg)
        {
            var rt = MakeRect(root, name, Vector2.zero, Vector2.one);
            if (bg.a > 0)
            {
                var img = rt.gameObject.AddComponent<Image>();
                img.color = bg;
            }
            rt.gameObject.SetActive(false);
            return rt.gameObject;
        }

        Image MakePanel(Transform parent, Vector2 pos, Vector2 size, Color fill, Color border)
        {
            var go = new GameObject("Panel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var b = go.AddComponent<Image>(); b.color = border;
            var inner = MakeImage(rt, fill, Vector2.zero, Vector2.one);
            inner.rectTransform.offsetMin = new Vector2(6, 6);
            inner.rectTransform.offsetMax = new Vector2(-6, -6);
            return inner;
        }

        MenuList MakeMenu(Transform parent, Vector2 startPos, float spacing, int size, params (string label, Action action)[] items)
        {
            var m = new MenuList();
            for (int i = 0; i < items.Length; i++)
            {
                var t = MakeText(parent, items[i].label, size, Color.white, new Vector2(0.5f, 0.5f),
                    startPos + new Vector2(0, -spacing * i), new Vector2(1000, spacing));
                AddOutline(t, 3);
                var e = new MenuEntry { text = t, select = items[i].action };
                string fixedLabel = items[i].label;
                e.label = () => fixedLabel;
                m.entries.Add(e);
                AddPointer(t, m, i);
            }
            m.Refresh();
            return m;
        }

        public void AddPointer(Graphic g, MenuList m, int index)
        {
            g.raycastTarget = true;
            var trig = g.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { if (m.sel != index) { m.sel = index; AudioManager.I.Play(Sfx.MenuMove); } });
            trig.triggers.Add(enter);
            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener(_ => m.Activate(index));
            trig.triggers.Add(click);
        }

        // ------------------------------------------------------------ Pantallas
        void BuildHUD()
        {
            hud = MakeScreen("HUD", Color.clear);
            var bar = MakeImage(hud.transform, new Color(0.07f, 0.08f, 0.13f, 0.95f), new Vector2(0, 1), new Vector2(1, 1));
            bar.rectTransform.offsetMin = new Vector2(0, -96);
            var line = MakeImage(bar.transform, new Color(0.55f, 0.45f, 0.95f, 0.9f), new Vector2(0, 0), new Vector2(1, 0));
            line.rectTransform.offsetMax = new Vector2(0, 4);

            var a = new Vector2(0, 0.5f);
            float x = 60;
            MakeIcon(bar.transform, SpriteLib.Get("player_down_0"), a, new Vector2(x, 2), 70);
            hudLives = MakeText(bar.transform, "x3", 40, Color.white, a, new Vector2(x + 85, 0), new Vector2(100, 60), TextAnchor.MiddleLeft);
            AddOutline(hudLives, 2);
            x += 190;
            MakeIcon(bar.transform, SpriteLib.Get("bomb_0"), a, new Vector2(x, 4), 62);
            hudBombs = MakeText(bar.transform, "1", 40, Color.white, a, new Vector2(x + 75, 0), new Vector2(80, 60), TextAnchor.MiddleLeft);
            AddOutline(hudBombs, 2);
            x += 150;
            MakeIcon(bar.transform, SpriteLib.Get("pu_fire"), a, new Vector2(x, 2), 56);
            hudFire = MakeText(bar.transform, "1", 40, Color.white, a, new Vector2(x + 75, 0), new Vector2(80, 60), TextAnchor.MiddleLeft);
            AddOutline(hudFire, 2);
            x += 150;
            MakeIcon(bar.transform, SpriteLib.Get("pu_speed"), a, new Vector2(x, 2), 56);
            hudSpeed = MakeText(bar.transform, "1", 40, Color.white, a, new Vector2(x + 75, 0), new Vector2(80, 60), TextAnchor.MiddleLeft);
            AddOutline(hudSpeed, 2);
            x += 150;
            hudRemoteGO = MakeIcon(bar.transform, SpriteLib.Get("pu_remote"), a, new Vector2(x, 2), 56).gameObject;
            x += 90;
            var sh = MakeIcon(bar.transform, SpriteLib.Get("pu_shield"), a, new Vector2(x, 2), 56);
            hudShieldGO = sh.gameObject;
            hudShield = MakeText(sh.transform, "12", 30, new Color(0.6f, 0.9f, 1f), new Vector2(1, 0.5f), new Vector2(40, 0), new Vector2(70, 50), TextAnchor.MiddleLeft);
            AddOutline(hudShield, 2);

            var c = new Vector2(0.5f, 0.5f);
            hudClock = MakeIcon(bar.transform, SpriteLib.Get("ui_clock"), c, new Vector2(-95, 0), 54);
            hudTime = MakeText(bar.transform, "03:00", 52, Color.white, c, new Vector2(35, 0), new Vector2(220, 70));
            AddOutline(hudTime, 3);

            var r = new Vector2(1, 0.5f);
            hudLevel = MakeText(bar.transform, "NIVEL 1", 26, new Color(0.75f, 0.7f, 1f), r, new Vector2(-560, 16), new Vector2(420, 36), TextAnchor.MiddleRight);
            AddOutline(hudLevel, 2);
            hudEnemies = MakeText(bar.transform, "Enemigos: 3", 26, new Color(1f, 0.6f, 0.65f), r, new Vector2(-560, -18), new Vector2(420, 36), TextAnchor.MiddleRight);
            AddOutline(hudEnemies, 2);
            hudScore = MakeText(bar.transform, "PUNTOS\n000000", 30, new Color(1f, 0.9f, 0.4f), r, new Vector2(-170, 0), new Vector2(300, 90), TextAnchor.MiddleRight);
            AddOutline(hudScore, 2);
        }

        void BuildTitle()
        {
            title = MakeScreen("Title", new Color(0.06f, 0.07f, 0.12f, 1f));
            // Fondo de baldosas
            var grass = SpriteLib.Get("tile_grass");
            var wall = SpriteLib.Get("tile_wall");
            var crate = SpriteLib.Get("tile_crate");
            var bgRoot = MakeRect(title.transform, "bg", Vector2.zero, Vector2.one);
            for (int gx = 0; gx < 21; gx++)
                for (int gy = 0; gy < 12; gy++)
                {
                    bool border = gy == 0 || gy == 11;
                    var s = border ? wall : ((gx * 7 + gy * 3) % 9 == 0 ? crate : grass);
                    var img = MakeIcon(bgRoot, s, Vector2.zero, new Vector2(gx * 96 + 48, gy * 96 + 48), 96);
                    img.color = border ? new Color(0.55f, 0.55f, 0.6f) : new Color(0.35f, 0.4f, 0.38f);
                }
            var dim = MakeImage(title.transform, new Color(0.03f, 0.03f, 0.08f, 0.55f), Vector2.zero, Vector2.one);
            dim.raycastTarget = false;

            // Sprites animados desfilando
            string[] parade = { "player_right_0", "enemy_red_2", "enemy_blue_2", "enemy_purple_2", "enemy_green_2" };
            for (int i = 0; i < parade.Length; i++)
            {
                var img = MakeIcon(title.transform, SpriteLib.Get(parade[i]), new Vector2(0, 0), new Vector2(-200 - i * 150, 150), i == 0 ? 120 : 100);
                if (i > 0) img.rectTransform.localScale = new Vector3(-1, 1, 1);
                titleSprites.Add((img.rectTransform, 260f, i * 0.7f, 150));
            }
            var b1 = MakeIcon(title.transform, SpriteLib.Get("bomb_3"), new Vector2(0.5f, 0.5f), new Vector2(-620, 280), 150);
            titleSprites.Add((b1.rectTransform, 0, 0, 280));
            var b2 = MakeIcon(title.transform, SpriteLib.Get("bomb_1"), new Vector2(0.5f, 0.5f), new Vector2(620, 280), 150);
            titleSprites.Add((b2.rectTransform, 0, 1.5f, 280));

            titleLogoShadow = MakeText(title.transform, "BOMBA BLAST", 150, new Color(0.9f, 0.2f, 0.3f), new Vector2(0.5f, 0.5f), new Vector2(8, 272), new Vector2(1600, 200));
            titleLogo = MakeText(title.transform, "BOMBA BLAST", 150, new Color(1f, 0.85f, 0.2f), new Vector2(0.5f, 0.5f), new Vector2(0, 280), new Vector2(1600, 200));
            AddOutline(titleLogo, 5);
            var sub = MakeText(title.transform, "¡Explota, esquiva y escapa!", 40, new Color(0.8f, 0.85f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(1200, 60), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            AddOutline(sub, 2);

            titleMenu = MakeMenu(title.transform, new Vector2(0, 40), 78, 50,
                ("JUGAR", () => GameManager.I.NewGame(0)),
                ("SELECCIONAR NIVEL", () => GameManager.I.OpenLevelSelect()),
                ("INSTRUCCIONES", () => GameManager.I.OpenScreen(GameManager.State.Instructions)),
                ("OPCIONES", () => GameManager.I.OpenScreen(GameManager.State.Options)),
                ("SALIR", () => GameManager.I.Quit()));

            titleHighScore = MakeText(title.transform, "RÉCORD: 0", 34, new Color(1f, 0.9f, 0.4f), new Vector2(0.5f, 0), new Vector2(0, 290), new Vector2(800, 50));
            AddOutline(titleHighScore, 2);
            pressStart = MakeText(title.transform, "Flechas / WASD para navegar  ·  ENTER para elegir", 24, new Color(1, 1, 1, 0.6f), new Vector2(0.5f, 0), new Vector2(0, 245), new Vector2(1200, 40), TextAnchor.MiddleCenter, FontStyle.Normal);
        }

        void BuildLevelSelect()
        {
            levelSelect = MakeScreen("LevelSelect", new Color(0.06f, 0.07f, 0.12f, 0.98f));
            var t = MakeText(levelSelect.transform, "SELECCIONAR NIVEL", 80, new Color(1f, 0.85f, 0.2f), new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(1400, 120));
            AddOutline(t, 4);
            for (int i = 0; i < Levels.All.Length; i++)
            {
                var def = Levels.All[i];
                int col = i % 3, row = i / 3;
                var pos = new Vector2((col - 1) * 520, 130 - row * 330);
                var panel = MakePanel(levelSelect.transform, pos, new Vector2(480, 300), new Color(0.12f, 0.13f, 0.22f), new Color(0.35f, 0.3f, 0.6f));
                levelCards.Add(panel.transform.parent.GetComponent<Image>());
                // Vista previa del piso
                for (int k = 0; k < 5; k++)
                {
                    var tile = MakeIcon(panel.transform, SpriteLib.Get(k % 2 == 0 ? def.floor : "tile_crate"), new Vector2(0.5f, 1f), new Vector2(-160 + k * 80, -60), 76);
                    tile.color = def.floorTint;
                }
                var num = MakeText(panel.transform, "NIVEL " + (i + 1), 44, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(440, 60));
                AddOutline(num, 2);
                var nm = MakeText(panel.transform, def.name, 30, new Color(0.75f, 0.85f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -55), new Vector2(440, 50));
                AddOutline(nm, 2);
                levelCardTexts.Add(num);
                // Iconos de enemigos
                var kinds = new HashSet<EnemyKind>(def.enemies);
                int ki = 0;
                foreach (var k in kinds)
                {
                    MakeIcon(panel.transform, SpriteLib.Get(EnemyStats.For(k).sprite + "0"), new Vector2(0.5f, 0f), new Vector2(-(kinds.Count - 1) * 30 + ki * 60, 40), 46);
                    ki++;
                }
                var lockGo = MakeImage(panel.transform, new Color(0, 0, 0, 0.75f), Vector2.zero, Vector2.one);
                var lt = MakeText(lockGo.transform, "BLOQUEADO", 40, new Color(1, 1, 1, 0.7f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440, 60));
                AddOutline(lt, 2);
                levelLocks.Add(lockGo.gameObject);
                int idx = i;
                var btn = panel.transform.parent.GetComponent<Image>();
                btn.raycastTarget = true;
                var trig = btn.gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => GameManager.I.HoverLevel(idx));
                trig.triggers.Add(enter);
                var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                click.callback.AddListener(_ => GameManager.I.PickLevel(idx));
                trig.triggers.Add(click);
            }
            var hint = MakeText(levelSelect.transform, "Flechas para elegir  ·  ENTER para jugar  ·  ESC para volver", 28, new Color(1, 1, 1, 0.6f), new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(1400, 50), TextAnchor.MiddleCenter, FontStyle.Normal);
            _ = hint;
        }

        void BuildOptions()
        {
            options = MakeScreen("Options", new Color(0.06f, 0.07f, 0.12f, 0.98f));
            var t = MakeText(options.transform, "OPCIONES", 90, new Color(1f, 0.85f, 0.2f), new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(1400, 130));
            AddOutline(t, 4);
            optionsMenu = MakeMenu(options.transform, new Vector2(0, 160), 90, 46,
                ("MÚSICA", null), ("EFECTOS", null), ("PANTALLA COMPLETA", null), ("BORRAR PROGRESO", null), ("VOLVER", () => GameManager.I.OpenScreen(GameManager.State.Title)));
            var am = AudioManager.I;
            string Bar(float v) { int n = Mathf.RoundToInt(v * 10); return new string('■', n) + new string('□', 10 - n); }
            var e0 = optionsMenu.entries[0];
            e0.label = () => "MÚSICA   " + Bar(am.MusicVolume);
            e0.leftRight = d => am.SetMusicVolume(am.MusicVolume + d * 0.1f);
            e0.select = () => am.SetMusicVolume(am.MusicVolume >= 0.99f ? 0f : am.MusicVolume + 0.1f);
            var e1 = optionsMenu.entries[1];
            e1.label = () => "EFECTOS   " + Bar(am.SfxVolume);
            e1.leftRight = d => am.SetSfxVolume(am.SfxVolume + d * 0.1f);
            e1.select = () => am.SetSfxVolume(am.SfxVolume >= 0.99f ? 0f : am.SfxVolume + 0.1f);
            var e2 = optionsMenu.entries[2];
            e2.label = () => "PANTALLA COMPLETA:  " + (Screen.fullScreen ? "SÍ" : "NO");
            e2.select = () => Screen.fullScreen = !Screen.fullScreen;
            e2.leftRight = _ => Screen.fullScreen = !Screen.fullScreen;
            var e3 = optionsMenu.entries[3];
            e3.label = () => GameManager.I != null && GameManager.I.ConfirmReset ? "¿SEGURO?  PULSA OTRA VEZ" : "BORRAR PROGRESO";
            e3.select = () => GameManager.I.ResetProgress();
            var hint = MakeText(options.transform, "◄ ► para ajustar", 28, new Color(1, 1, 1, 0.6f), new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(1000, 50), TextAnchor.MiddleCenter, FontStyle.Normal);
            _ = hint;
        }

        void BuildInstructions()
        {
            instructions = MakeScreen("Instructions", new Color(0.06f, 0.07f, 0.12f, 0.98f));
            var t = MakeText(instructions.transform, "CÓMO JUGAR", 80, new Color(1f, 0.85f, 0.2f), new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(1400, 110));
            AddOutline(t, 4);

            var ctrl = MakePanel(instructions.transform, new Vector2(-480, 90), new Vector2(820, 560), new Color(0.1f, 0.11f, 0.19f), new Color(0.35f, 0.3f, 0.6f));
            var ct = MakeText(ctrl.transform,
                "<color=#FFD94A>CONTROLES</color>\n\n" +
                "Mover:  Flechas / WASD / Stick\n" +
                "Poner bomba:  ESPACIO / X / (A)\n" +
                "Detonar remota:  Z / E / (B)\n" +
                "Pausa:  ESC / P / START\n\n" +
                "<color=#FFD94A>OBJETIVO</color>\n\n" +
                "Destruye las cajas y derrota a todos\nlos enemigos. Encuentra la <color=#7FE6FF>rejilla</color>\nescondida bajo una caja y entra\nen ella antes de que acabe el tiempo.",
                30, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 520));
            AddOutline(ct, 2);

            var pw = MakePanel(instructions.transform, new Vector2(480, 90), new Vector2(820, 560), new Color(0.1f, 0.11f, 0.19f), new Color(0.35f, 0.3f, 0.6f));
            MakeText(pw.transform, "<color=#FFD94A>POTENCIADORES</color>", 30, Color.white, new Vector2(0.5f, 1f), new Vector2(0, -36), new Vector2(760, 50));
            string[] names = { "pu_fire", "pu_bomb", "pu_speed", "pu_shield", "pu_remote" };
            string[] desc = { "Más fuego: explosiones más largas", "Más bombas: una bomba extra a la vez", "Más velocidad", "Escudo: invulnerable por 12 s", "Bomba remota: detónala con Z" };
            for (int i = 0; i < names.Length; i++)
            {
                MakeIcon(pw.transform, SpriteLib.Get(names[i]), new Vector2(0, 1f), new Vector2(70, -100 - i * 62), 52);
                var d = MakeText(pw.transform, desc[i], 26, Color.white, new Vector2(0, 1f), new Vector2(430, -100 - i * 62), new Vector2(640, 50), TextAnchor.MiddleLeft);
                AddOutline(d, 1);
            }
            MakeText(pw.transform, "<color=#FFD94A>ENEMIGOS</color>", 30, Color.white, new Vector2(0.5f, 1f), new Vector2(0, -420), new Vector2(760, 50));
            EnemyKind[] ks = { EnemyKind.Green, EnemyKind.Blue, EnemyKind.Red, EnemyKind.Purple };
            string[] en = { "Lento", "Ágil", "Cazador", "Jefe (2 golpes)" };
            for (int i = 0; i < ks.Length; i++)
            {
                MakeIcon(pw.transform, SpriteLib.Get(EnemyStats.For(ks[i]).sprite + "0"), new Vector2(0.5f, 0f), new Vector2(-285 + i * 190, 80), 60);
                MakeText(pw.transform, en[i] + "\n" + EnemyStats.For(ks[i]).points + " pts", 20, new Color(1, 1, 1, 0.85f), new Vector2(0.5f, 0f), new Vector2(-285 + i * 190, 25), new Vector2(190, 50));
            }
            instructionsMenu = MakeMenu(instructions.transform, new Vector2(0, -330), 70, 46, ("VOLVER", () => GameManager.I.OpenScreen(GameManager.State.Title)));
        }

        void BuildStageIntro()
        {
            stageIntro = MakeScreen("StageIntro", new Color(0.03f, 0.03f, 0.06f, 1f));
            introNumber = MakeText(stageIntro.transform, "NIVEL 1", 120, new Color(1f, 0.85f, 0.2f), new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(1400, 160));
            AddOutline(introNumber, 5);
            introName = MakeText(stageIntro.transform, "", 64, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1400, 90));
            AddOutline(introName, 3);
            introSub = MakeText(stageIntro.transform, "", 34, new Color(0.75f, 0.8f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(1400, 60), TextAnchor.MiddleCenter, FontStyle.Italic);
            AddOutline(introSub, 2);
        }

        void BuildPause()
        {
            pause = MakeScreen("Pause", new Color(0, 0, 0, 0.7f));
            var p = MakePanel(pause.transform, Vector2.zero, new Vector2(760, 560), new Color(0.1f, 0.11f, 0.19f, 0.97f), new Color(0.55f, 0.45f, 0.95f));
            var t = MakeText(p.transform, "PAUSA", 96, new Color(1f, 0.85f, 0.2f), new Vector2(0.5f, 1f), new Vector2(0, -95), new Vector2(700, 130));
            AddOutline(t, 4);
            pauseMenu = MakeMenu(p.transform, new Vector2(0, 30), 85, 46,
                ("CONTINUAR", () => GameManager.I.Resume()),
                ("REINICIAR NIVEL", () => GameManager.I.RestartLevelFromPause()),
                ("OPCIONES DE SONIDO", null),
                ("MENÚ PRINCIPAL", () => GameManager.I.BackToTitle()));
            var am = AudioManager.I;
            var e = pauseMenu.entries[2];
            e.label = () => "SONIDO: " + (am.SfxVolume > 0 || am.MusicVolume > 0 ? "ACTIVADO" : "SILENCIO");
            e.select = () =>
            {
                bool on = am.SfxVolume > 0 || am.MusicVolume > 0;
                am.SetMusicVolume(on ? 0 : 0.6f); am.SetSfxVolume(on ? 0 : 0.8f);
            };
        }

        void BuildLevelClear()
        {
            levelClear = MakeScreen("LevelClear", new Color(0.02f, 0.03f, 0.08f, 0.82f));
            clearTitle = MakeText(levelClear.transform, "¡NIVEL COMPLETADO!", 100, new Color(0.5f, 1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 280), new Vector2(1600, 140));
            AddOutline(clearTitle, 5);
            clearBody = MakeText(levelClear.transform, "", 44, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1200, 320));
            AddOutline(clearBody, 2);
            clearMenu = MakeMenu(levelClear.transform, new Vector2(0, -260), 80, 50, ("CONTINUAR", () => GameManager.I.ContinueAfterClear()));
        }

        void BuildGameOver()
        {
            gameOver = MakeScreen("GameOver", new Color(0.08f, 0.01f, 0.02f, 0.9f));
            var t = MakeText(gameOver.transform, "FIN DEL JUEGO", 130, new Color(1f, 0.3f, 0.35f), new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1600, 170));
            AddOutline(t, 6);
            MakeIcon(gameOver.transform, SpriteLib.Get("bomb_0"), new Vector2(0.5f, 0.5f), new Vector2(0, 100), 110);
            gameOverScore = MakeText(gameOver.transform, "", 44, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1200, 140));
            AddOutline(gameOverScore, 2);
            gameOverMenu = MakeMenu(gameOver.transform, new Vector2(0, -180), 80, 50,
                ("REINTENTAR NIVEL", () => GameManager.I.RetryAfterGameOver()),
                ("MENÚ PRINCIPAL", () => GameManager.I.BackToTitle()));
        }

        void BuildVictory()
        {
            victory = MakeScreen("Victory", new Color(0.04f, 0.04f, 0.1f, 0.92f));
            var t = MakeText(victory.transform, "¡VICTORIA!", 150, new Color(1f, 0.85f, 0.2f), new Vector2(0.5f, 0.5f), new Vector2(0, 290), new Vector2(1600, 190));
            AddOutline(t, 6);
            string[] row = { "enemy_green_0", "enemy_blue_0", "player_down_0", "enemy_red_0", "enemy_purple_0" };
            for (int i = 0; i < row.Length; i++)
            {
                var img = MakeIcon(victory.transform, SpriteLib.Get(row[i]), new Vector2(0.5f, 0.5f), new Vector2((i - 2) * 150, 130), i == 2 ? 140 : 100);
                titleSprites.Add((img.rectTransform, 0, i * 0.5f, 130));
            }
            victoryBody = MakeText(victory.transform, "", 42, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(1400, 220));
            AddOutline(victoryBody, 2);
            victoryMenu = MakeMenu(victory.transform, new Vector2(0, -260), 80, 50, ("MENÚ PRINCIPAL", () => GameManager.I.BackToTitle()));
        }

        // ------------------------------------------------------------ API
        public void ShowOnly(params GameObject[] screens)
        {
            foreach (var s in new[] { title, levelSelect, options, instructions, stageIntro, hud, pause, levelClear, gameOver, victory })
                s.SetActive(Array.IndexOf(screens, s) >= 0);
        }

        public void Flash(Color c, float alpha) { flash.color = new Color(c.r, c.g, c.b, alpha); flashAlpha = alpha; }

        public void SetFade(float a) => fade.color = new Color(0, 0, 0, a);
        public float FadeAlpha => fade.color.a;

        public void Toast(string msg) { toast.text = msg; toastTime = 2.4f; }
        public void ClearTransient()
        {
            toastTime = 0; toast.text = "";
            foreach (var p in popups) Destroy(p.t.gameObject);
            popups.Clear();
        }

        public void Popup(Vector3 world, string text, Color c)
        {
            var t = MakeText(root, text, 34, c, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 50));
            AddOutline(t, 2);
            t.transform.SetSiblingIndex(flash.transform.GetSiblingIndex());
            popups.Add((t, world, 0f));
        }

        public void UpdateHUD(GameManager gm, Board b)
        {
            if (b == null || b.Player == null) return;
            var st = b.Player.Stats;
            hudLives.text = "x" + gm.Lives;
            hudBombs.text = st.maxBombs.ToString();
            hudFire.text = st.range.ToString();
            hudSpeed.text = (st.speed + 1).ToString();
            hudRemoteGO.SetActive(st.remote);
            hudShieldGO.SetActive(b.Player.ShieldTime > 0);
            hudShield.text = Mathf.CeilToInt(b.Player.ShieldTime).ToString();
            int secs = Mathf.CeilToInt(b.TimeLeft);
            hudTime.text = $"{secs / 60:00}:{secs % 60:00}";
            bool hurry = b.TimeLeft <= 30f;
            hudTime.color = hurry && Mathf.Sin(uiTime * 10f) > 0 ? new Color(1f, 0.3f, 0.3f) : Color.white;
            hudClock.rectTransform.localRotation = Quaternion.Euler(0, 0, hurry ? Mathf.Sin(uiTime * 30f) * 10f : 0);
            hudScore.text = "PUNTOS\n" + gm.Score.ToString("000000");
            hudLevel.text = "NIVEL " + (gm.LevelIndex + 1) + " · " + b.Def.name;
            hudEnemies.text = b.EnemiesLeft > 0 ? "Enemigos: " + b.EnemiesLeft : (b.ExitOpen ? "¡Salida abierta!" : "");
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            uiTime += dt;
            if (flashAlpha > 0)
            {
                flashAlpha = Mathf.Max(0, flashAlpha - dt * 1.5f);
                var c = flash.color; c.a = flashAlpha; flash.color = c;
            }
            if (toastTime > 0)
            {
                toastTime -= dt;
                float a = Mathf.Clamp01(toastTime / 0.4f);
                float pop = toastTime > 2.2f ? Mathf.Lerp(1f, 1.3f, (toastTime - 2.2f) / 0.2f) : 1f;
                toast.color = new Color(1f, 1f, 1f, a);
                toast.rectTransform.localScale = Vector3.one * pop;
            }
            else if (toast.text != "") toast.text = "";

            var cam = Camera.main;
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var (t, world, age) = popups[i];
                age += dt;
                if (age > 1.2f || cam == null) { Destroy(t.gameObject); popups.RemoveAt(i); continue; }
                popups[i] = (t, world, age);
                Vector3 sp = cam.WorldToScreenPoint(world + Vector3.up * age * 0.9f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out var local);
                t.rectTransform.anchoredPosition = local;
                var c = t.color; c.a = Mathf.Clamp01((1.2f - age) / 0.4f); t.color = c;
                t.rectTransform.localScale = Vector3.one * (age < 0.15f ? Mathf.Lerp(0.5f, 1.2f, age / 0.15f) : 1f);
            }

            // Animaciones de las pantallas de título / victoria
            if (title.activeSelf || victory.activeSelf)
            {
                for (int i = 0; i < titleSprites.Count; i++)
                {
                    var (rt, speed, phase, y) = titleSprites[i];
                    if (!rt.gameObject.activeInHierarchy) continue;
                    if (speed > 0)
                    {
                        float x = ((uiTime * speed + phase * 400f) % 2600f) - 300f;
                        rt.anchoredPosition = new Vector2(x, y + Mathf.Abs(Mathf.Sin(uiTime * 8f + phase)) * 14f);
                    }
                    else
                    {
                        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y + Mathf.Sin(uiTime * 3f + phase) * 18f);
                        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(uiTime * 2f + phase) * 8f);
                    }
                }
                float s = 1f + 0.03f * Mathf.Sin(uiTime * 3f);
                titleLogo.rectTransform.localScale = Vector3.one * s;
                titleLogoShadow.rectTransform.localScale = Vector3.one * s;
                titleLogo.color = Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.6f, 0.15f), 0.5f + 0.5f * Mathf.Sin(uiTime * 2f));
                pressStart.color = new Color(1, 1, 1, 0.4f + 0.3f * Mathf.Sin(uiTime * 3f));
            }
        }
    }
}
