using UnityEditor;
using UnityEngine;

public sealed class VanillaTextAssets : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/VanillaText/", System.StringComparison.Ordinal)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = false;
        importer.sRGBTexture = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.isReadable = assetPath.EndsWith("/dialogue.png", System.StringComparison.Ordinal);
    }
}
