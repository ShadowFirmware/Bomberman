using UnityEngine;

namespace BombaBlast
{
    public enum EnemyKind { Green, Blue, Red, Purple }
    public enum PowerKind { Fire, Bomb, Speed, Shield, Remote }
    public enum HazardKind { None, Water, Lava }

    /// <summary>Definición de un nivel.</summary>
    public class LevelDef
    {
        public string name;
        public string subtitle;
        public int width, height;
        public string floor;        // tile_grass, tile_sand, tile_stone
        public Color floorTint = Color.white;
        public Color bgColor;
        public string[] decos;      // obstáculos decorativos que sustituyen algunos pilares
        public float decoChance;
        public HazardKind hazard;
        public int hazardPools;
        public float crateDensity;
        public EnemyKind[] enemies;
        public PowerKind[] powerUps;
        public int timeSeconds;
        public Music music;
        public int seed;
    }

    public static class Levels
    {
        public static readonly LevelDef[] All =
        {
            new LevelDef {
                name = "Pradera Verde", subtitle = "¡Aprende a usar tus bombas!",
                width = 13, height = 11, floor = "tile_grass", bgColor = new Color(0.08f,0.12f,0.09f),
                decos = new[]{"deco_bush"}, decoChance = 0.15f, hazard = HazardKind.None,
                crateDensity = 0.38f, enemies = new[]{EnemyKind.Green, EnemyKind.Green, EnemyKind.Green},
                powerUps = new[]{PowerKind.Fire, PowerKind.Bomb}, timeSeconds = 180, music = Music.Meadow, seed = 11 },

            new LevelDef {
                name = "Bosque Frondoso", subtitle = "Los slimes azules acechan entre los árboles",
                width = 15, height = 13, floor = "tile_grass", floorTint = new Color(0.85f,0.95f,0.85f), bgColor = new Color(0.05f,0.1f,0.06f),
                decos = new[]{"deco_tree","deco_bush"}, decoChance = 0.35f, hazard = HazardKind.Water, hazardPools = 1,
                crateDensity = 0.42f, enemies = new[]{EnemyKind.Green, EnemyKind.Green, EnemyKind.Blue, EnemyKind.Blue},
                powerUps = new[]{PowerKind.Fire, PowerKind.Bomb, PowerKind.Speed}, timeSeconds = 180, music = Music.Meadow, seed = 23 },

            new LevelDef {
                name = "Dunas Doradas", subtitle = "Cuidado: los rojos te persiguen",
                width = 15, height = 13, floor = "tile_sand", bgColor = new Color(0.16f,0.12f,0.07f),
                decos = new[]{"deco_rock"}, decoChance = 0.3f, hazard = HazardKind.Water, hazardPools = 2,
                crateDensity = 0.42f, enemies = new[]{EnemyKind.Blue, EnemyKind.Blue, EnemyKind.Red, EnemyKind.Red, EnemyKind.Green},
                powerUps = new[]{PowerKind.Bomb, PowerKind.Fire, PowerKind.Shield, PowerKind.Speed}, timeSeconds = 200, music = Music.Desert, seed = 37 },

            new LevelDef {
                name = "Ruinas de Piedra", subtitle = "Un guardián púrpura vigila las ruinas",
                width = 17, height = 13, floor = "tile_stone", bgColor = new Color(0.09f,0.09f,0.12f),
                decos = new[]{"deco_rock"}, decoChance = 0.2f, hazard = HazardKind.Water, hazardPools = 2,
                crateDensity = 0.45f, enemies = new[]{EnemyKind.Red, EnemyKind.Red, EnemyKind.Blue, EnemyKind.Blue, EnemyKind.Purple},
                powerUps = new[]{PowerKind.Remote, PowerKind.Fire, PowerKind.Bomb, PowerKind.Speed}, timeSeconds = 210, music = Music.Ruins, seed = 41 },

            new LevelDef {
                name = "Oasis Perdido", subtitle = "El agua bloquea el camino... ¡rodéala!",
                width = 17, height = 13, floor = "tile_sand", floorTint = new Color(1f,0.92f,0.85f), bgColor = new Color(0.12f,0.1f,0.08f),
                decos = new[]{"deco_tree","deco_rock"}, decoChance = 0.25f, hazard = HazardKind.Water, hazardPools = 4,
                crateDensity = 0.45f, enemies = new[]{EnemyKind.Red, EnemyKind.Red, EnemyKind.Red, EnemyKind.Purple, EnemyKind.Purple, EnemyKind.Green},
                powerUps = new[]{PowerKind.Fire, PowerKind.Bomb, PowerKind.Shield, PowerKind.Fire}, timeSeconds = 220, music = Music.Desert, seed = 53 },

            new LevelDef {
                name = "Corazón del Volcán", subtitle = "El desafío final. ¡Sobrevive a la lava!",
                width = 19, height = 13, floor = "tile_stone", floorTint = new Color(1f,0.78f,0.72f), bgColor = new Color(0.16f,0.05f,0.04f),
                decos = new[]{"deco_rock"}, decoChance = 0.3f, hazard = HazardKind.Lava, hazardPools = 4,
                crateDensity = 0.48f, enemies = new[]{EnemyKind.Purple, EnemyKind.Purple, EnemyKind.Purple, EnemyKind.Red, EnemyKind.Red, EnemyKind.Red, EnemyKind.Blue},
                powerUps = new[]{PowerKind.Fire, PowerKind.Bomb, PowerKind.Shield, PowerKind.Remote, PowerKind.Speed, PowerKind.Bomb}, timeSeconds = 240, music = Music.Volcano, seed = 67 },
        };
    }

    /// <summary>Estadísticas por tipo de enemigo.</summary>
    public struct EnemyStats
    {
        public float speed; public int hp; public int points; public bool chases; public float chaseRange; public bool smart;
        public string sprite; public Color color;

        public static EnemyStats For(EnemyKind k)
        {
            switch (k)
            {
                case EnemyKind.Green: return new EnemyStats { speed = 1.6f, hp = 1, points = 100, sprite = "enemy_green_", color = new Color(0.4f, 0.9f, 0.3f) };
                case EnemyKind.Blue: return new EnemyStats { speed = 2.2f, hp = 1, points = 200, sprite = "enemy_blue_", color = new Color(0.2f, 0.6f, 1f), smart = true };
                case EnemyKind.Red: return new EnemyStats { speed = 2.8f, hp = 1, points = 400, chases = true, chaseRange = 6, sprite = "enemy_red_", color = new Color(1f, 0.35f, 0.45f), smart = true };
                default: return new EnemyStats { speed = 2.1f, hp = 2, points = 800, chases = true, chaseRange = 99, sprite = "enemy_purple_", color = new Color(0.65f, 0.45f, 1f), smart = true };
            }
        }
    }
}
