using UnityEngine;

namespace BombaBlast
{
    public class Bomb : MonoBehaviour
    {
        public const float Fuse = 2.6f;
        public const float RemoteMax = 20f;

        Board board;
        public Vector2Int Cell { get; private set; }
        public int Range { get; private set; }
        public bool Remote { get; private set; }
        public bool Exploded { get; set; }

        SpriteRenderer sr, shadow;
        Sprite[] frames;
        float timer, anim, phase, life;
        float triggerDelay = -1f;

        public void Init(Board b, Vector2Int cell, int range, bool remote)
        {
            board = b; Cell = cell; Range = range; Remote = remote;
            timer = remote ? RemoteMax : Fuse;
            transform.position = b.CellToWorld(cell);
            frames = SpriteLib.Frames("bomb_", 4, 100f);
            shadow = SpriteLib.MakeRenderer("shadow", transform, SpriteLib.SoftCircle, 4);
            shadow.transform.localPosition = new Vector3(0, -0.32f, 0);
            shadow.transform.localScale = new Vector3(0.8f, 0.3f, 1);
            shadow.color = new Color(0, 0, 0, 0.45f);
            sr = SpriteLib.MakeRenderer("bomb", transform, frames[0], b.RowOrder(cell.y) + 3);
            sr.transform.localPosition = new Vector3(0, 0.12f, 0);
        }

        public void Trigger(float delay)
        {
            if (triggerDelay < 0f || delay < triggerDelay) triggerDelay = delay;
        }

        public void Tick(float dt)
        {
            if (Exploded) return;
            life += dt;
            anim += dt * 10f;
            sr.sprite = frames[(int)anim % frames.Length];

            if (triggerDelay >= 0f)
            {
                triggerDelay -= dt;
                if (triggerDelay <= 0f) { board.Explode(this); return; }
            }

            timer -= dt;
            float urgency = Remote ? 0.3f : 1f - Mathf.Clamp01(timer / Fuse);
            phase += dt * Mathf.Lerp(7f, 32f, urgency * urgency);
            float pop = life < 0.18f ? Mathf.Lerp(0.3f, 1.15f, life / 0.18f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((life - 0.18f) / 0.1f));
            float pulse = 1f + 0.07f * Mathf.Sin(phase);
            sr.transform.localScale = new Vector3(pulse * pop, (2f - pulse) * pop, 1f);
            if (Remote)
                sr.color = Color.Lerp(Color.white, new Color(0.6f, 0.8f, 1f), 0.5f + 0.5f * Mathf.Sin(life * 6f));
            else
                sr.color = urgency > 0.65f && Mathf.Sin(phase * 0.5f) > 0 ? new Color(1f, 0.55f, 0.55f) : Color.white;

            // chispas de la mecha
            if (Random.value < dt * 25f)
            {
                Vector2 tip = (Vector2)sr.transform.position + new Vector2(0.18f, 0.42f) * sr.transform.localScale.x;
                FXManager.I.Emit(tip, new Vector2(Random.Range(-1f, 1f), Random.Range(0.3f, 1.6f)), Random.Range(0.15f, 0.35f),
                    0.06f, 0.01f, new Color(1f, 0.95f, 0.5f), new Color(1f, 0.4f, 0f, 0f), 3f, 1f);
            }

            if (timer <= 0f) board.Explode(this);
        }
    }

    /// <summary>Visual de una celda de llama.</summary>
    public class Flame : MonoBehaviour
    {
        public const float Duration = 0.65f;
        SpriteRenderer sr, glow;
        float age;
        bool center;
        Vector2Int dir;
        Sprite[] frames;

        public void Init(Vector3 pos, Vector2Int d, bool isCenter, bool tip)
        {
            transform.position = pos;
            center = isCenter; dir = d;
            frames = SpriteLib.Frames("explosion_", 4, 100f);
            glow = SpriteLib.MakeRenderer("glow", transform, SpriteLib.SoftCircle, 1490);
            glow.color = new Color(1f, 0.55f, 0.1f, 0.55f);
            glow.transform.localScale = Vector3.one * (center ? 2.6f : 1.8f);
            sr = SpriteLib.MakeRenderer("flame", transform, frames[0], 1500);
            if (!center) sr.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0, 4) * 90f);
        }

        /// <summary>Devuelve true cuando debe eliminarse.</summary>
        public bool Tick(float dt)
        {
            age += dt;
            float k = age / Duration;
            int f;
            if (center) f = Mathf.Min(3, (int)(age / 0.06f));
            else f = age < 0.05f ? 1 : (age < 0.12f ? 2 : 3);
            sr.sprite = frames[f];

            float fade = k > 0.6f ? 1f - (k - 0.6f) / 0.4f : 1f;
            float flicker = 1f + 0.08f * Mathf.Sin(age * 60f);
            float baseScale = center ? 1.05f : 0.9f;
            Vector3 s = Vector3.one * baseScale * flicker * Mathf.Lerp(0.6f, 1f, fade);
            if (!center)
            {
                // estirar a lo largo del eje de la explosión
                bool horiz = dir.x != 0;
                bool rotated = Mathf.RoundToInt(sr.transform.localEulerAngles.z / 90f) % 2 == 1;
                if (horiz ^ rotated) { s.x *= 1.12f; s.y *= 0.78f; } else { s.x *= 0.78f; s.y *= 1.12f; }
            }
            sr.transform.localScale = s;
            sr.color = new Color(1f, 1f, 1f, fade);
            glow.color = new Color(1f, 0.55f, 0.1f, 0.55f * fade);
            return age >= Duration;
        }
    }

    public class PowerUp : MonoBehaviour
    {
        public PowerKind Kind { get; private set; }
        public bool Ready => life > 0.35f;
        SpriteRenderer sr, glow, shadow;
        float life;

        public static string SpriteName(PowerKind k)
        {
            switch (k)
            {
                case PowerKind.Fire: return "pu_fire";
                case PowerKind.Bomb: return "pu_bomb";
                case PowerKind.Speed: return "pu_speed";
                case PowerKind.Shield: return "pu_shield";
                default: return "pu_remote";
            }
        }

        public void Init(Board b, Vector2Int c, PowerKind k)
        {
            Kind = k;
            transform.position = b.CellToWorld(c);
            glow = SpriteLib.MakeRenderer("glow", transform, SpriteLib.SoftCircle, 3);
            glow.transform.localScale = Vector3.one * 1.3f;
            shadow = SpriteLib.MakeRenderer("shadow", transform, SpriteLib.SoftCircle, 4);
            shadow.transform.localPosition = new Vector3(0, -0.33f, 0);
            shadow.color = new Color(0, 0, 0, 0.4f);
            sr = SpriteLib.MakeRenderer("icon", transform, SpriteLib.Get(SpriteName(k), 82f), b.RowOrder(c.y) + 2);
        }

        public void Tick(float dt)
        {
            life += dt;
            float pop = life < 0.3f ? Mathf.SmoothStep(0f, 1f, life / 0.3f) * 1.2f : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((life - 0.3f) / 0.15f));
            float bob = Mathf.Sin(life * 4f) * 0.07f;
            sr.transform.localPosition = new Vector3(0, 0.05f + bob, 0);
            sr.transform.localScale = Vector3.one * pop;
            shadow.transform.localScale = new Vector3(0.6f - bob, 0.2f, 1f);
            float g = 0.5f + 0.5f * Mathf.Sin(life * 6f);
            glow.color = new Color(1f, 0.95f, 0.5f, 0.25f + 0.25f * g);
        }
    }
}
