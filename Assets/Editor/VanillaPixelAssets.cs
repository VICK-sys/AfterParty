using UnityEditor;
using UnityEngine;

public sealed class VanillaPixelAssets : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        bool pixel = assetPath.StartsWith("Assets/Resources/FunkinNotes/Pixel/") || assetPath.StartsWith("Assets/Resources/FunkinHud/Pixel/")
            || assetPath == "Assets/Resources/FunkinHud/Icons/icon-bf-pixel.png"
            || assetPath == "Assets/Resources/FunkinHud/Icons/icon-senpai.png"
            || assetPath == "Assets/Resources/FunkinHud/Icons/icon-senpai-angry.png"
            || assetPath == "Assets/Resources/FunkinHud/Icons/icon-spirit.png";
        if (!pixel && !assetPath.StartsWith("Assets/Resources/FunkinHud/Countdown/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 8192;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = pixel ? FilterMode.Point : FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        if (assetPath.EndsWith("arrowEndsNew.png")) importer.wrapModeV = TextureWrapMode.Repeat;
    }
}
