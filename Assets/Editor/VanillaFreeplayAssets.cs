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
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        importer.maxTextureSize = Mathf.Max(4096, Mathf.NextPowerOfTwo(Mathf.Max(width, height)));
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
        settings.loadType = assetPath.Contains("freeplayRandom") || assetPath.Contains("/cartoons/") ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
        importer.defaultSampleSettings = settings;
    }
}
