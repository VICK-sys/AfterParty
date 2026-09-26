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
        importer.filterMode = assetPath.Contains("/icons/") ? FilterMode.Point : FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        if (assetPath == "Assets/Resources/VanillaFreeplay/fonts/header/5by7-32.png") importer.isReadable = true;
        foreach (string character in new[] { "bfChill", "gfChill", "picoChill", "neneChill" })
            if (assetPath == "Assets/Resources/VanillaFreeplay/charSelect/" + character + "/spritemap1.png")
                foreach (string platform in new[] { "Standalone", "WindowsStoreApps" })
                {
                    var settings = importer.GetPlatformTextureSettings(platform);
                    settings.overridden = true;
                    settings.maxTextureSize = importer.maxTextureSize;
                    settings.format = TextureImporterFormat.BC7;
                    settings.compressionQuality = 100;
                    importer.SetPlatformTextureSettings(settings);
                }
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
