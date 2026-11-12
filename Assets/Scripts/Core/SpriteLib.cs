using System.Collections.Generic;
using UnityEngine;

namespace BombaBlast
{
    /// <summary>
    /// Carga los sprites extraídos de la hoja (Resources/Sprites) y genera sprites
    /// auxiliares (píxel blanco, círculos) en tiempo de ejecución.
    /// </summary>
    public static class SpriteLib
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static Material unlitMat;
        static bool unlitSearched;

        /// <summary>Material sin iluminación para que los sprites luzcan con sus colores originales.</summary>
        public static Material SpriteMaterial
        {
            get
            {
                if (!unlitSearched)
                {
                    unlitSearched = true;
                    var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (sh != null) unlitMat = new Material(sh);
                }
                return unlitMat;
            }
        }

        /// <summary>Obtiene un sprite por nombre. ppu = píxeles por unidad (0 = el ancho de la textura ocupa 1 unidad).</summary>
        public static Sprite Get(string name, float ppu = 0f)
        {
            string key = name + "@" + ppu;
            if (cache.TryGetValue(key, out var s)) return s;
            var tex = Resources.Load<Texture2D>("Sprites/" + name);
            if (tex == null)
            {
                Debug.LogWarning("Sprite no encontrado: " + name);
                s = White;
            }
            else
            {
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                float p = ppu > 0 ? ppu : tex.width;
                s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), p, 0, SpriteMeshType.FullRect);
                s.name = name;
            }
            cache[key] = s;
            return s;
        }

        public static Sprite[] Frames(string prefix, int count, float ppu)
        {
            var arr = new Sprite[count];
            for (int i = 0; i < count; i++) arr[i] = Get(prefix + i, ppu);
            return arr;
        }

        static Sprite white;
        public static Sprite White
        {
            get
            {
                if (white == null)
                {
                    var t = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                    var c = new Color32[16];
                    for (int i = 0; i < 16; i++) c[i] = new Color32(255, 255, 255, 255);
                    t.SetPixels32(c); t.Apply();
                    white = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
                }
                return white;
            }
        }

        static Sprite softCircle, hardCircle, ring;
        /// <summary>Círculo con borde difuso (sombras, brillos).</summary>
        public static Sprite SoftCircle => softCircle ??= MakeCircle(64, true, false);
        /// <summary>Círculo pixelado sólido (partículas redondas).</summary>
        public static Sprite HardCircle => hardCircle ??= MakeCircle(16, false, false);
        /// <summary>Anillo (escudo, ondas de choque).</summary>
        public static Sprite Ring => ring ??= MakeCircle(48, false, true);

        static Sprite MakeCircle(int size, bool soft, bool isRing)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = soft ? FilterMode.Bilinear : FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    float a;
                    if (soft) a = Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d);
                    else if (isRing) a = (d <= 1f && d >= 0.8f) ? 1f : (d < 0.8f ? 0.18f : 0f);
                    else a = d <= 1f ? 1f : 0f;
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels32(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        /// <summary>Crea un GameObject con SpriteRenderer configurado.</summary>
        public static SpriteRenderer MakeRenderer(string goName, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            var m = SpriteMaterial;
            if (m != null) sr.sharedMaterial = m;
            return sr;
        }
    }
}
