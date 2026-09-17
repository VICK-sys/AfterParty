using UnityEditor;
using UnityEngine;

public sealed class VanillaFreeplayAssets : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/VanillaFreeplay/", System.StringComparison.Ordinal)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = assetPath.Contains("/icons/") || assetPath.Contains("/fonts/") ? FilterMode.Point : FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
    }

    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Resources/VanillaFreeplay/", System.StringComparison.Ordinal)) return;
        var importer = (AudioImporter)assetImporter;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = assetPath.Contains("freeplayRandom") ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
        importer.defaultSampleSettings = settings;
    }
}
