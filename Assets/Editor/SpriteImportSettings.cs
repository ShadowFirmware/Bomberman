using UnityEditor;
using UnityEngine;

namespace BombaBlast.EditorTools
{
    /// <summary>Configura los PNG de Resources/Sprites como pixel art (sin filtrado ni compresión).</summary>
    public class SpriteImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("Assets/Resources/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = 2048;
        }

        [MenuItem("Bomba Blast/Reimportar sprites")]
        static void Reimport()
        {
            AssetDatabase.ImportAsset("Assets/Resources", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }
    }
}
