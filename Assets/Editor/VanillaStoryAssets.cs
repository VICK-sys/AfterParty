using System;
using UnityEditor;
using UnityEngine;

public sealed class VanillaStoryAssets : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/VanillaStory/", StringComparison.Ordinal)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        importer.maxTextureSize = Mathf.Max(4096, Mathf.NextPowerOfTwo(Mathf.Max(width, height)));
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
    }
}
