using System.Collections.Generic;
using UnityEngine;

namespace BombaBlast
{
    public class Enemy : GridMover
    {
        public EnemyKind Kind { get; private set; }
        public bool Dead { get; private set; }
        public bool Removable { get; private set; }
        public bool Invulnerable => invuln > 0f || spawnTime < 1f;

        EnemyStats st;
        int hp;
        float invuln, idle, anim, spawnTime, deathTime, thinkJitter;
        SpriteRenderer sr, shadow;
        Sprite[] frames;
        readonly List<Vector2Int> options = new List<Vector2Int>();

        public void Init(Board b, Vector2Int c, EnemyKind k)
        {
            board = b; cell = c; Kind = k;
            st = EnemyStats.For(k);
            hp = st.hp;
            frames = SpriteLib.Frames(st.sprite, 4, 92f);
            shadow = SpriteLib.MakeRenderer("shadow", transform, SpriteLib.SoftCircle, 4);
            shadow.transform.localPosition = new Vector3(0, -0.36f, 0);
            shadow.transform.localScale = new Vector3(0.8f, 0.28f, 1);
            shadow.color = new Color(0, 0, 0, 0.4f);
            sr = SpriteLib.MakeRenderer("sprite", transform, frames[0], 0);
            sr.transform.localPosition = new Vector3(0, 0.05f, 0);
            anim = Random.value * 10f;
            thinkJitter = Random.Range(0.85f, 1.15f);
            UpdatePosition();
        }

        protected override bool CanEnter(Vector2Int c) => board.CanEnter(c);

        public void Tick(float dt)
        {
            anim += dt;
            spawnTime += dt;
            if (Dead) { TickDeath(dt); return; }
            if (invuln > 0) invuln -= dt;

            float speed = st.speed * thinkJitter;
            if (moving)
            {
                // Si colocan una bomba (o aparece fuego) delante, dar media vuelta
                if (progress < 0.5f && (board.HasBomb(target) || (st.smart && board.IsFire(target)))) Reverse();
                if (Advance(speed, dt, out float leftover))
                {
                    if (Decide()) progress = Mathf.Min(0.9f, leftover * speed);
                }
            }
            else
            {
                idle -= dt;
                if (idle <= 0f && !Decide()) idle = 0.25f;
            }

            UpdatePosition();
            UpdateSprite();
        }

        bool Decide()
        {
            options.Clear();
            foreach (var d in Board.Dirs) if (CanEnter(cell + d)) options.Add(d);
            if (options.Count == 0) return false;

            bool inDanger = board.IsDanger(cell);
            if (st.smart)
            {
                // Evitar celdas peligrosas salvo que estemos ya en peligro y no haya alternativa
                var safe = options.FindAll(d => !board.IsDanger(cell + d));
                if (safe.Count > 0) options.RemoveAll(d => board.IsDanger(cell + d));
                else if (!inDanger) return false; // esperar quieto
            }

            Vector2Int choice = Vector2Int.zero;
            var p = board.Player;
            if (st.chases && p != null && p.Alive)
            {
                int dist = Board.Manhattan(cell, p.CurrentCell);
                float chaseChance = Kind == EnemyKind.Purple ? 0.75f : 0.8f;
                if (dist <= st.chaseRange && Random.value < chaseChance)
                {
                    var step = board.PathStep(cell, p.CurrentCell, 30);
                    if (step != Vector2Int.zero && options.Contains(step)) choice = step;
                }
            }

            if (choice == Vector2Int.zero)
            {
                if (moveDir != Vector2Int.zero && options.Contains(moveDir) && Random.value < 0.7f) choice = moveDir;
                else
                {
                    var noBack = options.FindAll(d => d != -moveDir);
                    var pool = noBack.Count > 0 ? noBack : options;
                    choice = pool[Random.Range(0, pool.Count)];
                }
            }
            return TryStart(choice);
        }

        void UpdateSprite()
        {
            int f;
            bool flip = false;
            if (facing == Vector2Int.up) f = 3;
            else if (facing == Vector2Int.down) f = 0;
            else
            {
                f = moving && ((int)(anim * 6f)) % 2 == 0 ? 1 : 2;
                flip = facing == Vector2Int.right;
            }
            sr.sprite = frames[f];
            sr.flipX = flip;
            float squash = Mathf.Sin(anim * (moving ? 14f : 5f)) * 0.06f;
            float pop = spawnTime < 0.4f ? Mathf.SmoothStep(0f, 1f, spawnTime / 0.4f) : 1f;
            sr.transform.localScale = new Vector3(1f + squash, 1f - squash, 1f) * pop;
            sr.sortingOrder = SortOrder(5);
            shadow.sortingOrder = sr.sortingOrder - 2;
            if (invuln > 0) sr.color = Mathf.Sin(anim * 50f) > 0 ? new Color(1, 1, 1, 0.4f) : Color.white;
            else if (spawnTime < 1f) sr.color = new Color(1, 1, 1, Mathf.Clamp01(spawnTime * 2f));
            else sr.color = Color.white;
        }

        public void Hit()
        {
            if (Dead || Invulnerable) return;
            hp--;
            if (hp > 0)
            {
                invuln = 1.2f;
                AudioManager.I.Play(Sfx.EnemyHit);
                FXManager.I.Sparkle(transform.position, st.color, 10);
                return;
            }
            Dead = true;
            deathTime = 0f;
            AudioManager.I.Play(Sfx.EnemyDie, 1f, Random.Range(0.9f, 1.1f));
            GameManager.I.AddScore(st.points, transform.position);
        }

        void TickDeath(float dt)
        {
            deathTime += dt;
            float k = deathTime / 0.5f;
            sr.color = Mathf.Sin(deathTime * 60f) > 0 ? Color.white : new Color(1, 1, 1, 0.3f);
            sr.transform.localScale = new Vector3(1f + k * 0.6f, Mathf.Max(0f, 1f - k), 1f);
            if (k >= 1f && !Removable)
            {
                FXManager.I.EnemyPoof(transform.position, st.color);
                Removable = true;
            }
        }
    }
}
