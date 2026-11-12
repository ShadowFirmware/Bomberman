using UnityEngine;

namespace BombaBlast
{
    /// <summary>Movimiento por rejilla con interpolación suave (compartido por jugador y enemigos).</summary>
    public abstract class GridMover : MonoBehaviour
    {
        protected Board board;
        protected Vector2Int cell, target, moveDir;
        protected float progress;
        protected bool moving;
        protected Vector2Int facing = Vector2Int.down;

        public Vector2Int CurrentCell => moving && progress >= 0.5f ? target : cell;
        public Vector2 Position => transform.position;
        public bool IsCentered => !moving;

        protected abstract bool CanEnter(Vector2Int c);

        protected bool TryStart(Vector2Int dir)
        {
            facing = dir;
            var n = cell + dir;
            if (!CanEnter(n)) return false;
            target = n; moveDir = dir; moving = true; progress = 0f;
            return true;
        }

        protected void Reverse()
        {
            (cell, target) = (target, cell);
            moveDir = -moveDir; facing = moveDir;
            progress = 1f - progress;
        }

        /// <summary>Avanza; devuelve true si llegó al centro de una celda en este paso (con el sobrante en leftover).</summary>
        protected bool Advance(float speed, float dt, out float leftover)
        {
            leftover = 0f;
            if (!moving) return false;
            progress += speed * dt;
            if (progress >= 1f)
            {
                leftover = (progress - 1f) / Mathf.Max(0.001f, speed);
                cell = target; moving = false; progress = 0f;
                return true;
            }
            return false;
        }

        protected void UpdatePosition()
        {
            var a = board.CellToWorld(cell);
            transform.position = moving ? Vector3.Lerp(a, board.CellToWorld(target), progress) : a;
        }

        protected int SortOrder(int bias) => board.RowOrder(board.WorldToRow(transform.position.y)) + bias;
    }

    public class Player : GridMover
    {
        public PlayerStats Stats { get; private set; }
        public bool Alive { get; private set; } = true;
        public bool Celebrating { get; private set; }
        public float ShieldTime { get; private set; }
        public bool Shielded => ShieldTime > 0f || spawnGrace > 0f;

        SpriteRenderer sr, shadow, shield;
        Sprite[] down, up, left, right;
        float anim, deathTime, spawnGrace = 1.2f, celebrateTime, life;

        public float Speed => 3.7f + 0.6f * Stats.speed;

        public void Init(Board b, Vector2Int c, PlayerStats stats)
        {
            board = b; cell = c; Stats = stats;
            const float ppu = 88f;
            down = SpriteLib.Frames("player_down_", 4, ppu);
            up = SpriteLib.Frames("player_up_", 4, ppu);
            left = SpriteLib.Frames("player_left_", 4, ppu);
            right = SpriteLib.Frames("player_right_", 4, ppu);
            shadow = SpriteLib.MakeRenderer("shadow", transform, SpriteLib.SoftCircle, 4);
            shadow.transform.localPosition = new Vector3(0, -0.4f, 0);
            shadow.transform.localScale = new Vector3(0.75f, 0.28f, 1);
            shadow.color = new Color(0, 0, 0, 0.45f);
            sr = SpriteLib.MakeRenderer("sprite", transform, down[0], 0);
            sr.transform.localPosition = new Vector3(0, 0.18f, 0);
            shield = SpriteLib.MakeRenderer("shield", transform, SpriteLib.Ring, 0);
            shield.transform.localPosition = new Vector3(0, 0.15f, 0);
            shield.color = new Color(0.4f, 0.85f, 1f, 0.8f);
            shield.gameObject.SetActive(false);
            UpdatePosition();
            UpdateSprite();
        }

        protected override bool CanEnter(Vector2Int c) => board.CanEnter(c);

        public void Tick(float dt)
        {
            life += dt;
            if (!Alive) { TickDeath(dt); return; }
            if (Celebrating) { TickCelebrate(dt); return; }

            if (spawnGrace > 0) spawnGrace -= dt;
            if (ShieldTime > 0)
            {
                float before = ShieldTime;
                ShieldTime -= dt;
                if (before > 2f && ShieldTime <= 2f) GameManager.I.ShowToast("El escudo se agota...");
            }

            var input = GameInput.Move();

            if (moving)
            {
                // Permitir dar media vuelta en mitad de un paso (sin volver a meterse en una bomba ya dejada atrás)
                if (input == -moveDir && !(board.HasBomb(cell) && progress > 0.5f)) Reverse();
                if (Advance(Speed, dt, out float leftover))
                {
                    if (input != Vector2Int.zero && TryStart(input)) progress = Mathf.Min(0.9f, leftover * Speed);
                    else if (input != Vector2Int.zero) facing = input;
                }
            }
            else if (input != Vector2Int.zero)
            {
                if (!TryStart(input)) facing = input;
            }

            if (GameInput.BombPressed)
                board.TryPlaceBomb(CurrentCell, Stats);
            if (GameInput.DetonatePressed && Stats.remote)
                board.DetonateOldestRemote();

            UpdatePosition();
            anim = moving ? anim + dt * 9f : 0f;
            UpdateSprite();
        }

        void UpdateSprite()
        {
            Sprite[] set = facing == Vector2Int.up ? up : facing == Vector2Int.left ? left : facing == Vector2Int.right ? right : down;
            sr.sprite = set[moving ? (int)anim % 4 : 0];
            float bob = moving ? Mathf.Abs(Mathf.Sin(anim * Mathf.PI * 0.5f)) * 0.05f : Mathf.Sin(life * 3f) * 0.015f;
            sr.transform.localPosition = new Vector3(0, 0.18f + bob, 0);
            sr.sortingOrder = SortOrder(5);
            shadow.sortingOrder = sr.sortingOrder - 2;

            bool blink = spawnGrace > 0f || (ShieldTime > 0f && ShieldTime < 2f);
            sr.color = blink && Mathf.Sin(life * 40f) > 0 ? new Color(1, 1, 1, 0.45f) : Color.white;

            shield.gameObject.SetActive(ShieldTime > 0f);
            if (ShieldTime > 0f)
            {
                float p = 1f + 0.06f * Mathf.Sin(life * 8f);
                shield.transform.localScale = Vector3.one * 1.25f * p;
                shield.sortingOrder = sr.sortingOrder + 1;
                shield.transform.Rotate(0, 0, 90f * Time.deltaTime);
                float a = ShieldTime < 2f ? (Mathf.Sin(life * 20f) > 0 ? 0.8f : 0.2f) : 0.75f;
                shield.color = new Color(0.4f, 0.85f, 1f, a);
            }
        }

        public void Collect(PowerKind k)
        {
            string label;
            switch (k)
            {
                case PowerKind.Fire: Stats.range = Mathf.Min(Stats.range + 1, 8); label = "¡+FUEGO!"; break;
                case PowerKind.Bomb: Stats.maxBombs = Mathf.Min(Stats.maxBombs + 1, 8); label = "¡+BOMBA!"; break;
                case PowerKind.Speed: Stats.speed = Mathf.Min(Stats.speed + 1, 4); label = "¡+VELOCIDAD!"; break;
                case PowerKind.Shield: ShieldTime = 12f; label = "¡ESCUDO!"; AudioManager.I.Play(Sfx.Shield); break;
                default: Stats.remote = true; label = "¡BOMBA REMOTA! (Z)"; break;
            }
            AudioManager.I.Play(Sfx.PowerUp);
            GameManager.I.AddScore(50, null);
            GameManager.I.Popup(transform.position + Vector3.up * 0.8f, label, new Color(1f, 0.92f, 0.3f));
        }

        public void Kill(bool force)
        {
            if (!Alive || Celebrating) return;
            if (!force && Shielded) return;
            Alive = false;
            deathTime = 0f;
            shield.gameObject.SetActive(false);
            AudioManager.I.StopMusic();
            AudioManager.I.Play(Sfx.PlayerDie);
            FXManager.I.Shake(0.25f, 0.4f);
            GameManager.I.FlashScreen(new Color(1f, 0.3f, 0.3f), 0.35f);
        }

        void TickDeath(float dt)
        {
            deathTime += dt;
            float t = deathTime;
            if (t < 0.6f)
            {
                // Parpadeo y giro
                sr.sprite = down[((int)(t * 20f)) % 4];
                sr.color = Mathf.Sin(t * 50f) > 0 ? Color.white : new Color(1f, 0.4f, 0.4f);
                sr.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 30f) * 15f);
            }
            else if (t < 1.4f)
            {
                float k = (t - 0.6f) / 0.8f;
                sr.transform.localRotation = Quaternion.Euler(0, 0, k * 720f);
                sr.transform.localScale = Vector3.one * (1f - k);
                sr.color = new Color(1, 1, 1, 1f - k * 0.5f);
                shadow.transform.localScale = new Vector3(0.75f * (1f - k), 0.28f * (1f - k), 1);
                if (Random.value < dt * 30f)
                    FXManager.I.Emit(sr.transform.position, Random.insideUnitCircle * 2f, 0.5f, 0.1f, 0.02f, Color.white, new Color(1, 0.5f, 0.5f, 0));
            }
            else if (sr.enabled)
            {
                sr.enabled = false;
                shadow.enabled = false;
                FXManager.I.EnemyPoof(transform.position, new Color(1f, 0.5f, 0.6f));
            }
            if (t > 2.2f && t - dt <= 2.2f) GameManager.I.OnPlayerDeathFinished();
        }

        /// <summary>Reaparece en la celda indicada conservando el estado del mapa.</summary>
        public void Respawn(Vector2Int c)
        {
            cell = target = c;
            moving = false; progress = 0f;
            facing = Vector2Int.down;
            Alive = true;
            ShieldTime = 0f;
            spawnGrace = 3f;
            sr.enabled = true;
            shadow.enabled = true;
            sr.color = Color.white;
            sr.transform.localRotation = Quaternion.identity;
            sr.transform.localScale = Vector3.one;
            shadow.transform.localScale = new Vector3(0.75f, 0.28f, 1);
            UpdatePosition();
            UpdateSprite();
            FXManager.I.Sparkle(transform.position, new Color(0.6f, 0.9f, 1f), 20);
            FXManager.I.Ring(transform.position, new Color(0.6f, 0.9f, 1f, 0.9f), 0.2f, 2f, 0.4f);
        }

        public void Celebrate()
        {
            Celebrating = true;
            celebrateTime = 0f;
            moving = false;
            UpdatePosition();
        }

        void TickCelebrate(float dt)
        {
            celebrateTime += dt;
            float jump = Mathf.Abs(Mathf.Sin(celebrateTime * 9f)) * 0.3f;
            sr.transform.localPosition = new Vector3(0, 0.18f + jump, 0);
            sr.sprite = down[((int)(celebrateTime * 8f)) % 4];
            if (Random.value < dt * 12f)
                FXManager.I.Emit(transform.position + (Vector3)(Random.insideUnitCircle * 0.5f), new Vector2(0, 2f), 0.8f, 0.12f, 0.02f,
                    Color.HSVToRGB(Random.value, 0.6f, 1f), new Color(1, 1, 1, 0));
        }
    }
}
