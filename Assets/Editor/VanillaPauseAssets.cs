using System;
using UnityEditor;
using UnityEngine;

public sealed class VanillaPauseAssets : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/FunkinPause/", StringComparison.Ordinal)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.textureShape = TextureImporterShape.Texture2D;
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
        if (!assetPath.StartsWith("Assets/Resources/FunkinPause/", StringComparison.Ordinal)) return;
        var importer = (AudioImporter)assetImporter;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        importer.defaultSampleSettings = settings;
    }
}
