using System;
using UnityEditor;
using UnityEngine;

public sealed class VanillaTitleAssets : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/VanillaTitle/", StringComparison.Ordinal)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
    }

    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Resources/VanillaTitle/", StringComparison.Ordinal)) return;
        var importer = (AudioImporter)assetImporter;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = assetPath.EndsWith("confirmMenu.ogg", StringComparison.Ordinal)
            ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 1;
        importer.defaultSampleSettings = settings;
    }
}
