using System.Collections.Generic;
using UnityEngine;

namespace BombaBlast
{
    /// <summary>Partículas ligeras basadas en sprites, ondas expansivas y temblor de cámara.</summary>
    public class FXManager : MonoBehaviour
    {
        public static FXManager I { get; private set; }

        class Particle
        {
            public SpriteRenderer sr;
            public Vector2 vel;
            public float life, maxLife, size0, size1, gravity, drag, spin;
            public Color c0, c1;
        }

        readonly List<Particle> active = new List<Particle>();
        readonly Stack<Particle> pool = new Stack<Particle>();
        float shakeTime, shakeDur, shakeAmp;

        public Vector3 ShakeOffset { get; private set; }

        void Awake() { I = this; }

        Particle Spawn()
        {
            Particle p = pool.Count > 0 ? pool.Pop() : null;
            if (p == null)
            {
                p = new Particle { sr = SpriteLib.MakeRenderer("p", transform, SpriteLib.White, 200) };
            }
            p.sr.gameObject.SetActive(true);
            active.Add(p);
            return p;
        }

        public void Emit(Vector2 pos, Vector2 vel, float life, float size0, float size1, Color c0, Color c1,
            float gravity = 0f, float drag = 0f, Sprite sprite = null, int order = 200, float spin = 0f)
        {
            var p = Spawn();
            p.sr.sprite = sprite != null ? sprite : SpriteLib.White;
            p.sr.sortingOrder = order + 2000;
            p.sr.transform.position = pos;
            p.sr.transform.rotation = Quaternion.identity;
            p.vel = vel; p.life = p.maxLife = life; p.size0 = size0; p.size1 = size1;
            p.c0 = c0; p.c1 = c1; p.gravity = gravity; p.drag = drag; p.spin = spin;
            Apply(p, 0f);
        }

        void Apply(Particle p, float k)
        {
            float s = Mathf.Lerp(p.size0, p.size1, k);
            float baseSize = p.sr.sprite.bounds.size.x;
            p.sr.transform.localScale = Vector3.one * (s / Mathf.Max(0.0001f, baseSize));
            p.sr.color = Color.Lerp(p.c0, p.c1, k);
        }

        public void Burst(Vector2 pos, int count, Color a, Color b, float speedMin, float speedMax, float lifeMin, float lifeMax,
            float sizeMin, float sizeMax, float gravity = 0f, float drag = 2f, Sprite sprite = null, int order = 200, float spread = 0f)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float sp = Random.Range(speedMin, speedMax);
                Vector2 v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp;
                Vector2 off = Random.insideUnitCircle * spread;
                Color c = Color.Lerp(a, b, Random.value);
                Color end = c; end.a = 0f;
                float size = Random.Range(sizeMin, sizeMax);
                Emit(pos + off, v, Random.Range(lifeMin, lifeMax), size, size * 0.3f, c, end, gravity, drag, sprite, order,
                    Random.Range(-360f, 360f));
            }
        }

        // ------------------------------------------------------------- Presets
        public void ExplosionFX(Vector2 pos, float power)
        {
            Burst(pos, (int)(18 * power), new Color(1f, 0.95f, 0.5f), new Color(1f, 0.45f, 0.05f), 2f, 6f * power, 0.25f, 0.6f,
                0.06f, 0.16f, 0f, 3f, SpriteLib.White, 210);
            Burst(pos, (int)(10 * power), new Color(0.35f, 0.33f, 0.38f, 0.8f), new Color(0.6f, 0.58f, 0.6f, 0.6f), 0.4f, 1.6f,
                0.6f, 1.2f, 0.3f, 0.6f, -0.6f, 1.5f, SpriteLib.SoftCircle, 190, 0.3f);
            Ring(pos, new Color(1f, 0.85f, 0.5f, 0.8f), 0.3f, 1.8f + power * 0.4f, 0.35f);
            Shake(0.12f + 0.05f * power, 0.3f);
        }

        public void CrateDebris(Vector2 pos)
        {
            Burst(pos, 14, new Color(0.85f, 0.55f, 0.25f), new Color(0.55f, 0.32f, 0.12f), 2f, 5f, 0.4f, 0.8f, 0.08f, 0.18f, -14f, 1f,
                SpriteLib.White, 205, 0.2f);
            Burst(pos, 6, new Color(0.7f, 0.65f, 0.55f, 0.6f), new Color(0.5f, 0.5f, 0.5f, 0.4f), 0.3f, 1f, 0.5f, 0.9f, 0.3f, 0.5f,
                -0.4f, 1f, SpriteLib.SoftCircle, 195, 0.2f);
        }

        public void Sparkle(Vector2 pos, Color c, int n = 12)
        {
            Burst(pos, n, c, Color.white, 1.5f, 4f, 0.3f, 0.7f, 0.05f, 0.12f, 0f, 3f, SpriteLib.White, 220);
        }

        public void EnemyPoof(Vector2 pos, Color c)
        {
            Burst(pos, 20, c, Color.white, 1.5f, 5f, 0.3f, 0.7f, 0.08f, 0.18f, -4f, 2f, SpriteLib.HardCircle, 215);
            Ring(pos, new Color(c.r, c.g, c.b, 0.8f), 0.2f, 1.4f, 0.3f);
        }

        public void Ring(Vector2 pos, Color c, float size0, float size1, float life)
        {
            Color end = c; end.a = 0f;
            Emit(pos, Vector2.zero, life, size0, size1, c, end, 0, 0, SpriteLib.Ring, 225);
        }

        public void Shake(float amp, float dur)
        {
            if (amp >= shakeAmp * (shakeTime / Mathf.Max(0.001f, shakeDur)))
            {
                shakeAmp = amp; shakeDur = dur; shakeTime = dur;
            }
        }

        public void Clear()
        {
            foreach (var p in active) { p.sr.gameObject.SetActive(false); pool.Push(p); }
            active.Clear();
            shakeTime = 0; ShakeOffset = Vector3.zero;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var p = active[i];
                p.life -= dt;
                if (p.life <= 0)
                {
                    p.sr.gameObject.SetActive(false);
                    pool.Push(p);
                    active.RemoveAt(i);
                    continue;
                }
                p.vel.y -= p.gravity * dt;
                p.vel *= 1f / (1f + p.drag * dt);
                var t = p.sr.transform;
                t.position += (Vector3)(p.vel * dt);
                if (p.spin != 0) t.Rotate(0, 0, p.spin * dt);
                Apply(p, 1f - p.life / p.maxLife);
            }

            if (shakeTime > 0)
            {
                shakeTime -= dt;
                float k = Mathf.Clamp01(shakeTime / shakeDur);
                ShakeOffset = (Vector3)(Random.insideUnitCircle * shakeAmp * k);
            }
            else ShakeOffset = Vector3.zero;
        }
    }
}
