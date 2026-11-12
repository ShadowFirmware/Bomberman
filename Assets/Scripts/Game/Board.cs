using System.Collections.Generic;
using UnityEngine;

namespace BombaBlast
{
    public enum Cell { Empty, Wall, Crate, Hazard, Deco }

    public class PlayerStats
    {
        public int maxBombs = 1;
        public int range = 1;
        public int speed = 0;
        public bool remote;

        public PlayerStats Clone() => (PlayerStats)MemberwiseClone();
    }

    /// <summary>Tablero de juego: rejilla, bloques, bombas, llamas, objetos y lógica del nivel.</summary>
    public class Board : MonoBehaviour
    {
        public LevelDef Def { get; private set; }
        public int W { get; private set; }
        public int H { get; private set; }

        Cell[,] grid;
        SpriteRenderer[,] blockSr;
        Bomb[,] bombAt;
        PowerUp[,] powerAt;
        float[,] fire;
        bool[,] danger;
        readonly Dictionary<Vector2Int, PowerKind> hidden = new Dictionary<Vector2Int, PowerKind>();
        readonly List<SpriteRenderer> hazardSr = new List<SpriteRenderer>();

        public Player Player { get; private set; }
        public readonly List<Enemy> Enemies = new List<Enemy>();
        readonly List<Bomb> bombs = new List<Bomb>();
        readonly List<Flame> flames = new List<Flame>();
        readonly List<PowerUp> powerUps = new List<PowerUp>();

        Vector2Int exitCell;
        bool exitRevealed, exitOpen;
        SpriteRenderer exitSr, exitGlow;
        float time;
        float hazardFxTimer;

        public float TimeLeft { get; private set; }
        public bool Finished { get; private set; }
        public bool ExitOpen => exitOpen;
        public int EnemiesLeft => Enemies.Count;
        public Vector2Int SpawnCell => new Vector2Int(1, H - 2);

        System.Random rng;

        // ------------------------------------------------------------ Construcción
        public void Build(LevelDef def, PlayerStats stats)
        {
            Def = def; W = def.width; H = def.height;
            rng = new System.Random(def.seed);
            grid = new Cell[W, H];
            blockSr = new SpriteRenderer[W, H];
            bombAt = new Bomb[W, H];
            powerAt = new PowerUp[W, H];
            fire = new float[W, H];
            danger = new bool[W, H];
            TimeLeft = def.timeSeconds;

            // Estructura base: borde y pilares
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    bool border = x == 0 || y == 0 || x == W - 1 || y == H - 1;
                    bool pillar = x % 2 == 0 && y % 2 == 0;
                    grid[x, y] = border || pillar ? Cell.Wall : Cell.Empty;
                    if (pillar && !border && def.decos != null && def.decos.Length > 0 && rng.NextDouble() < def.decoChance)
                        grid[x, y] = Cell.Deco;
                }

            var spawn = SpawnCell;
            // Charcos de agua / lava
            if (def.hazard != HazardKind.None)
            {
                for (int i = 0; i < def.hazardPools; i++)
                {
                    for (int tries = 0; tries < 30; tries++)
                    {
                        var c = new Vector2Int(rng.Next(1, W - 1), rng.Next(1, H - 1));
                        if (Manhattan(c, spawn) < 5) continue;
                        var cells = new List<Vector2Int>();
                        bool horizontal = rng.Next(2) == 0;
                        for (int k = -1; k <= 1; k++)
                        {
                            var cc = horizontal ? new Vector2Int(c.x + k, c.y) : new Vector2Int(c.x, c.y + k);
                            if (InBounds(cc) && grid[cc.x, cc.y] == Cell.Empty && Manhattan(cc, spawn) >= 4) cells.Add(cc);
                        }
                        if (cells.Count < 2) continue;
                        foreach (var cc in cells) grid[cc.x, cc.y] = Cell.Hazard;
                        if (!Connected()) { foreach (var cc in cells) grid[cc.x, cc.y] = Cell.Empty; continue; }
                        break;
                    }
                }
            }

            // Cajas
            for (int x = 1; x < W - 1; x++)
                for (int y = 1; y < H - 1; y++)
                {
                    if (grid[x, y] != Cell.Empty) continue;
                    if (Manhattan(new Vector2Int(x, y), spawn) <= 2) continue;
                    if (rng.NextDouble() < def.crateDensity) grid[x, y] = Cell.Crate;
                }

            // Salida y power-ups escondidos en cajas
            var crates = new List<Vector2Int>();
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (grid[x, y] == Cell.Crate) crates.Add(new Vector2Int(x, y));
            Shuffle(crates);
            crates.Sort((a, b) => Manhattan(b, spawn).CompareTo(Manhattan(a, spawn)));
            // La salida en una caja lejana (entre el 30% más lejano)
            int farIdx = rng.Next(0, Mathf.Max(1, crates.Count / 3));
            exitCell = crates[farIdx];
            crates.RemoveAt(farIdx);
            Shuffle(crates);
            int ci = 0;
            foreach (var p in def.powerUps)
                if (ci < crates.Count) hidden[crates[ci++]] = p;

            CreateVisuals();

            // Jugador
            Player = CreateEntity<Player>("Player");
            Player.Init(this, spawn, stats);

            // Enemigos: lejos del jugador
            var free = new List<Vector2Int>();
            for (int x = 1; x < W - 1; x++)
                for (int y = 1; y < H - 1; y++)
                {
                    var c = new Vector2Int(x, y);
                    if (Manhattan(c, spawn) >= 6 && (grid[x, y] == Cell.Empty || grid[x, y] == Cell.Crate) && c != exitCell && !hidden.ContainsKey(c))
                        free.Add(c);
                }
            Shuffle(free);
            // Preferir celdas vacías
            free.Sort((a, b) => (grid[a.x, a.y] == Cell.Empty ? 0 : 1).CompareTo(grid[b.x, b.y] == Cell.Empty ? 0 : 1));
            for (int i = 0; i < def.enemies.Length && i < free.Count; i++)
            {
                var c = free[i];
                if (grid[c.x, c.y] == Cell.Crate) RemoveBlock(c);
                var e = CreateEntity<Enemy>("Enemy_" + def.enemies[i]);
                e.Init(this, c, def.enemies[i]);
                Enemies.Add(e);
            }
        }

        T CreateEntity<T>(string n) where T : Component
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        void CreateVisuals()
        {
            var floorSprite = SpriteLib.Get(Def.floor);
            var wallSprite = SpriteLib.Get("tile_wall");
            var crateSprite = SpriteLib.Get("tile_crate");
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    var c = new Vector2Int(x, y);
                    var pos = CellToWorld(c);
                    // Piso con patrón de tablero para mejor lectura
                    var f = SpriteLib.MakeRenderer("floor", transform, floorSprite, 0);
                    f.transform.position = pos;
                    f.transform.localScale = Vector3.one * 1.002f;
                    float shade = (x + y) % 2 == 0 ? 1f : 0.9f;
                    f.color = Def.floorTint * shade;

                    switch (grid[x, y])
                    {
                        case Cell.Wall:
                            blockSr[x, y] = SpriteLib.MakeRenderer("wall", transform, wallSprite, RowOrder(y));
                            blockSr[x, y].transform.position = pos;
                            AddShadow(pos, 1f);
                            break;
                        case Cell.Deco:
                            string d = Def.decos[rng.Next(Def.decos.Length)];
                            float ppu = d == "deco_tree" ? 80f : 88f;
                            blockSr[x, y] = SpriteLib.MakeRenderer("deco", transform, SpriteLib.Get(d, ppu), RowOrder(y));
                            blockSr[x, y].transform.position = pos + new Vector3(0, d == "deco_tree" ? 0.15f : 0.02f, 0);
                            AddShadow(pos + new Vector3(0, -0.3f, 0), 0.9f);
                            break;
                        case Cell.Crate:
                            blockSr[x, y] = SpriteLib.MakeRenderer("crate", transform, crateSprite, RowOrder(y));
                            blockSr[x, y].transform.position = pos;
                            break;
                        case Cell.Hazard:
                            var hs = SpriteLib.MakeRenderer("hazard", transform,
                                SpriteLib.Get(Def.hazard == HazardKind.Lava ? "tile_lava" : "tile_water"), 1);
                            hs.transform.position = pos;
                            hs.transform.localScale = Vector3.one * 1.002f;
                            hazardSr.Add(hs);
                            break;
                    }
                }

            exitSr = SpriteLib.MakeRenderer("exit", transform, SpriteLib.Get("tile_grate"), 2);
            exitSr.transform.position = CellToWorld(exitCell);
            exitSr.gameObject.SetActive(false);
            exitGlow = SpriteLib.MakeRenderer("exitGlow", transform, SpriteLib.SoftCircle, 3);
            exitGlow.transform.position = CellToWorld(exitCell);
            exitGlow.transform.localScale = Vector3.one * 1.6f;
            exitGlow.color = new Color(0.4f, 0.9f, 1f, 0f);
            exitGlow.gameObject.SetActive(false);
        }

        void AddShadow(Vector3 pos, float size)
        {
            var s = SpriteLib.MakeRenderer("shadow", transform, SpriteLib.SoftCircle, 1);
            s.transform.position = pos + new Vector3(0.12f, -0.12f, 0);
            s.transform.localScale = new Vector3(size * 1.2f, size * 1.1f, 1);
            s.color = new Color(0, 0, 0, 0.35f);
        }

        // ------------------------------------------------------------ Utilidades
        public int RowOrder(float y) => 1000 + Mathf.RoundToInt((H - y) * 10f);
        public Vector3 CellToWorld(Vector2Int c) => new Vector3(c.x - W * 0.5f + 0.5f, c.y - H * 0.5f + 0.5f, 0);
        public Vector2Int WorldToCell(Vector3 p) => new Vector2Int(Mathf.RoundToInt(p.x + W * 0.5f - 0.5f), Mathf.RoundToInt(p.y + H * 0.5f - 0.5f));
        public float WorldToRow(float wy) => wy + H * 0.5f - 0.5f;
        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < W && c.y < H;
        public static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        public Cell CellAt(Vector2Int c) => InBounds(c) ? grid[c.x, c.y] : Cell.Wall;
        public bool HasBomb(Vector2Int c) => InBounds(c) && bombAt[c.x, c.y] != null;
        public bool IsFire(Vector2Int c) => InBounds(c) && fire[c.x, c.y] > 0f;
        public bool IsDanger(Vector2Int c) => InBounds(c) && (danger[c.x, c.y] || fire[c.x, c.y] > 0f);

        public bool CanEnter(Vector2Int c) => InBounds(c) && grid[c.x, c.y] == Cell.Empty && bombAt[c.x, c.y] == null;

        void Shuffle<T>(List<T> l)
        {
            for (int i = l.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); }
        }

        bool Connected()
        {
            var start = SpawnCell;
            var seen = new bool[W, H];
            var q = new Queue<Vector2Int>();
            q.Enqueue(start); seen[start.x, start.y] = true;
            int count = 1;
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var d in Dirs)
                {
                    var n = c + d;
                    if (!InBounds(n) || seen[n.x, n.y]) continue;
                    var t = grid[n.x, n.y];
                    if (t == Cell.Wall || t == Cell.Deco || t == Cell.Hazard) continue;
                    seen[n.x, n.y] = true; count++; q.Enqueue(n);
                }
            }
            int total = 0;
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (grid[x, y] == Cell.Empty || grid[x, y] == Cell.Crate) total++;
            return count == total;
        }

        public static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        /// <summary>Primer paso de la ruta más corta (BFS) hacia el objetivo, o (0,0) si no hay.</summary>
        public Vector2Int PathStep(Vector2Int from, Vector2Int to, int maxDepth)
        {
            if (from == to) return Vector2Int.zero;
            var prev = new Dictionary<Vector2Int, Vector2Int>();
            var q = new Queue<Vector2Int>();
            var depth = new Dictionary<Vector2Int, int>();
            q.Enqueue(from); prev[from] = from; depth[from] = 0;
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c == to)
                {
                    var step = c;
                    while (prev[step] != from) step = prev[step];
                    return step - from;
                }
                if (depth[c] >= maxDepth) continue;
                foreach (var d in Dirs)
                {
                    var n = c + d;
                    if (prev.ContainsKey(n)) continue;
                    if (n != to && !CanEnter(n)) continue;
                    prev[n] = c; depth[n] = depth[c] + 1;
                    q.Enqueue(n);
                }
            }
            return Vector2Int.zero;
        }

        // ------------------------------------------------------------ Bombas
        public int BombsOwned => bombs.Count;

        public bool TryPlaceBomb(Vector2Int c, PlayerStats s)
        {
            if (!InBounds(c) || bombAt[c.x, c.y] != null || grid[c.x, c.y] != Cell.Empty) return false;
            if (bombs.Count >= s.maxBombs) return false;
            var b = CreateEntity<Bomb>("Bomb");
            b.Init(this, c, s.range, s.remote);
            bombAt[c.x, c.y] = b;
            bombs.Add(b);
            AudioManager.I.Play(Sfx.BombPlace);
            return true;
        }

        public void DetonateOldestRemote()
        {
            foreach (var b in bombs)
                if (b.Remote && !b.Exploded)
                {
                    AudioManager.I.Play(Sfx.Detonate);
                    b.Trigger(0f);
                    return;
                }
        }

        public void Explode(Bomb b)
        {
            if (b.Exploded) return;
            b.Exploded = true;
            var c = b.Cell;
            bombAt[c.x, c.y] = null;
            bombs.Remove(b);
            Destroy(b.gameObject);

            AddFlame(c, Vector2Int.zero, true, false);
            int cellsHit = 1;
            foreach (var d in Dirs)
            {
                for (int i = 1; i <= b.Range; i++)
                {
                    var n = c + d * i;
                    var t = CellAt(n);
                    if (t == Cell.Wall || t == Cell.Deco) break;
                    if (t == Cell.Crate)
                    {
                        AddFlame(n, d, false, true);
                        BreakCrate(n);
                        cellsHit++;
                        break;
                    }
                    var other = bombAt[n.x, n.y];
                    AddFlame(n, d, false, i == b.Range);
                    cellsHit++;
                    if (other != null && !other.Exploded) { other.Trigger(0.06f); break; }
                    if (powerAt[n.x, n.y] != null && powerAt[n.x, n.y].Ready)
                    {
                        var p = powerAt[n.x, n.y];
                        powerAt[n.x, n.y] = null;
                        powerUps.Remove(p);
                        FXManager.I.Sparkle(CellToWorld(n), new Color(1f, 0.6f, 0.2f));
                        Destroy(p.gameObject);
                        break;
                    }
                }
            }

            float power = Mathf.Clamp(cellsHit / 4f, 1f, 2.2f);
            FXManager.I.ExplosionFX(CellToWorld(c), power);
            AudioManager.I.Play(Sfx.Explosion, 0.9f, Random.Range(0.85f, 1.1f));
            GameManager.I.FlashScreen(new Color(1f, 0.85f, 0.5f), 0.12f);
        }

        void AddFlame(Vector2Int c, Vector2Int dir, bool center, bool tip)
        {
            fire[c.x, c.y] = Mathf.Max(fire[c.x, c.y], Flame.Duration);
            var go = new GameObject("Flame");
            go.transform.SetParent(transform, false);
            var f = go.AddComponent<Flame>();
            f.Init(CellToWorld(c), dir, center, tip);
            flames.Add(f);
        }

        readonly HashSet<Vector2Int> breaking = new HashSet<Vector2Int>();

        void BreakCrate(Vector2Int c)
        {
            if (grid[c.x, c.y] != Cell.Crate || breaking.Contains(c)) return;
            breaking.Add(c);
            var sr = blockSr[c.x, c.y];
            blockSr[c.x, c.y] = null;
            GameManager.I.AddScore(10, null);
            StartCoroutine(CrateBreakRoutine(c, sr));
        }

        System.Collections.IEnumerator CrateBreakRoutine(Vector2Int c, SpriteRenderer sr)
        {
            if (sr != null)
            {
                // La caja sigue bloqueando el paso (y las llamas) mientras se rompe
                sr.sprite = SpriteLib.Get("tile_crate_damaged");
                float t = 0;
                while (t < 0.35f)
                {
                    t += Time.deltaTime;
                    sr.transform.localScale = Vector3.one * (1f + Mathf.Sin(t * 60f) * 0.05f);
                    sr.color = Color.Lerp(Color.white, new Color(1f, 0.6f, 0.4f), t / 0.35f);
                    yield return null;
                }
                FXManager.I.CrateDebris(CellToWorld(c));
                AudioManager.I.Play(Sfx.CrateBreak, 0.7f, Random.Range(0.9f, 1.15f));
                Destroy(sr.gameObject);
            }
            grid[c.x, c.y] = Cell.Empty;
            breaking.Remove(c);
            yield return new WaitForSeconds(0.25f);
            if (c == exitCell)
            {
                exitRevealed = true;
                exitSr.gameObject.SetActive(true);
                FXManager.I.Sparkle(CellToWorld(c), new Color(0.5f, 0.9f, 1f), 16);
                if (Enemies.Count == 0) OpenExit();
            }
            else if (hidden.TryGetValue(c, out var kind))
            {
                hidden.Remove(c);
                SpawnPowerUp(c, kind);
            }
        }

        void RemoveBlock(Vector2Int c)
        {
            grid[c.x, c.y] = Cell.Empty;
            if (blockSr[c.x, c.y] != null) { Destroy(blockSr[c.x, c.y].gameObject); blockSr[c.x, c.y] = null; }
        }

        void SpawnPowerUp(Vector2Int c, PowerKind k)
        {
            var p = CreateEntity<PowerUp>("PowerUp_" + k);
            p.Init(this, c, k);
            powerAt[c.x, c.y] = p;
            powerUps.Add(p);
            FXManager.I.Sparkle(CellToWorld(c), Color.yellow, 10);
        }

        void OpenExit()
        {
            if (exitOpen) return;
            exitOpen = true;
            if (exitRevealed)
            {
                AudioManager.I.Play(Sfx.ExitOpen);
                FXManager.I.Ring(CellToWorld(exitCell), new Color(0.5f, 0.9f, 1f, 0.9f), 0.3f, 3f, 0.6f);
            }
            GameManager.I.ShowToast(exitRevealed ? "¡La salida está abierta!" : "¡Todos los enemigos derrotados! Busca la salida");
        }

        // ------------------------------------------------------------ Bucle principal
        public void Tick(float dt)
        {
            if (Finished) return;
            time += dt;

            // Temporizador
            if (Player.Alive && !Player.Celebrating)
            {
                float before = TimeLeft;
                TimeLeft -= dt;
                if (before > 30f && TimeLeft <= 30f)
                {
                    AudioManager.I.SetMusicPitch(1.12f);
                    GameManager.I.ShowToast("¡Date prisa!");
                }
                if (TimeLeft <= 10f && Mathf.FloorToInt(before) != Mathf.FloorToInt(TimeLeft) && TimeLeft > 0)
                    AudioManager.I.Play(Sfx.TimeWarning, 0.6f);
                if (TimeLeft <= 0f)
                {
                    TimeLeft = 0f;
                    GameManager.I.ShowToast("¡Se acabó el tiempo!");
                    Player.Kill(true);
                }
            }

            // Mapa de peligro (celdas que alcanzarán bombas activas)
            System.Array.Clear(danger, 0, danger.Length);
            foreach (var b in bombs) MarkDanger(b);

            for (int i = bombs.Count - 1; i >= 0; i--)
                if (i < bombs.Count) bombs[i].Tick(dt);

            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (fire[x, y] > 0) fire[x, y] -= dt;

            for (int i = flames.Count - 1; i >= 0; i--)
                if (flames[i].Tick(dt)) { Destroy(flames[i].gameObject); flames.RemoveAt(i); }

            foreach (var p in powerUps) p.Tick(dt);

            Player.Tick(dt);

            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                var e = Enemies[i];
                e.Tick(dt);
                if (e.Dead) continue;
                if (!e.Invulnerable && IsFire(e.CurrentCell)) e.Hit();
                if (Player.Alive && !Player.Shielded && !Player.Celebrating &&
                    Vector2.Distance(Player.Position, e.Position) < 0.62f)
                    Player.Kill(false);
            }
            for (int i = Enemies.Count - 1; i >= 0; i--)
                if (Enemies[i].Removable)
                {
                    Destroy(Enemies[i].gameObject);
                    Enemies.RemoveAt(i);
                    if (Enemies.Count == 0) OpenExit();
                }

            if (Player.Alive && !Player.Celebrating)
            {
                var pc = Player.CurrentCell;
                if (IsFire(pc) && !Player.Shielded) Player.Kill(false);
                var pu = powerAt[pc.x, pc.y];
                if (pu != null && pu.Ready && Player.Alive)
                {
                    powerAt[pc.x, pc.y] = null;
                    powerUps.Remove(pu);
                    Player.Collect(pu.Kind);
                    FXManager.I.Sparkle(CellToWorld(pc), Color.yellow, 18);
                    FXManager.I.Ring(CellToWorld(pc), new Color(1f, 1f, 0.6f, 0.9f), 0.2f, 1.6f, 0.3f);
                    Destroy(pu.gameObject);
                }
            }

            // Salida
            if (exitRevealed && exitSr != null)
            {
                if (exitOpen)
                {
                    exitGlow.gameObject.SetActive(true);
                    float k = 0.5f + 0.5f * Mathf.Sin(time * 5f);
                    exitGlow.color = new Color(0.4f, 0.9f, 1f, 0.35f + 0.35f * k);
                    exitSr.color = Color.Lerp(Color.white, new Color(0.6f, 1f, 1f), k);
                    if (Random.value < dt * 6f)
                        FXManager.I.Emit(CellToWorld(exitCell) + (Vector3)(Random.insideUnitCircle * 0.4f), new Vector2(0, 1.2f), 0.8f,
                            0.1f, 0.02f, new Color(0.6f, 1f, 1f), new Color(0.6f, 1f, 1f, 0f));
                    if (Player.Alive && !Player.Celebrating && Player.IsCentered && Player.CurrentCell == exitCell)
                    {
                        Finished = true;
                        Player.Celebrate();
                        GameManager.I.OnLevelComplete();
                    }
                }
            }

            // Animación de agua/lava
            foreach (var h in hazardSr)
            {
                float off = h.transform.position.x * 0.7f + h.transform.position.y * 0.5f;
                float p2 = 0.88f + 0.12f * Mathf.Sin(time * 2.5f + off);
                h.color = new Color(p2, p2, Mathf.Min(1f, p2 + 0.1f));
            }
            if (Def.hazard == HazardKind.Lava && hazardSr.Count > 0)
            {
                hazardFxTimer -= dt;
                if (hazardFxTimer <= 0)
                {
                    hazardFxTimer = 0.08f;
                    var h = hazardSr[Random.Range(0, hazardSr.Count)];
                    FXManager.I.Emit(h.transform.position + (Vector3)(Random.insideUnitCircle * 0.4f),
                        new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.6f, 1.5f)), Random.Range(0.6f, 1.2f), 0.09f, 0.02f,
                        new Color(1f, 0.7f, 0.2f), new Color(1f, 0.2f, 0f, 0f));
                }
            }
        }

        /// <summary>Reaparece el jugador en el inicio sin reiniciar el nivel.</summary>
        public void RespawnPlayer()
        {
            if (TimeLeft <= 0f) TimeLeft = Def.timeSeconds;
            Player.Respawn(SpawnCell);
        }

        void MarkDanger(Bomb b)
        {
            var c = b.Cell;
            danger[c.x, c.y] = true;
            foreach (var d in Dirs)
                for (int i = 1; i <= b.Range; i++)
                {
                    var n = c + d * i;
                    var t = CellAt(n);
                    if (t == Cell.Wall || t == Cell.Deco || t == Cell.Crate) break;
                    danger[n.x, n.y] = true;
                }
        }
    }
}
