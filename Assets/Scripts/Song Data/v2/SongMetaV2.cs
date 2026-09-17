using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

[Serializable]
public class SongMetaV2
{
    [JsonProperty("Song Name")]
    public string songName;
    [JsonProperty("Song Credits")]
    public Dictionary<string, string> credits;
    [JsonProperty("Song Difficulties")]
    public Dictionary<string, Color> difficulties;
    [JsonProperty("Song Description")]
    public string songDescription;

    [JsonProperty("Song Variations")]
    public Dictionary<string, SongVariation> variations;

    public SongVariation GetVariation(string difficulty)
    {
        if (variations != null)
            foreach (var entry in variations)
                if (string.Equals(entry.Key, difficulty, StringComparison.OrdinalIgnoreCase))
                    return entry.Value;
        return null;
    }

    public string AssetPath(string fileName, string difficulty)
    {
        string suffix = GetVariation(difficulty)?.assetSuffix ?? string.Empty;
        return Path.Combine(songPath, Path.GetFileNameWithoutExtension(fileName) + suffix + Path.GetExtension(fileName));
    }
    
    //NOT SERIALIZED
    [JsonIgnore] public string songPath;
    [JsonIgnore] public Sprite songCover;
    [JsonIgnore] public BundleMeta bundleMeta;

}

[Serializable]
public class SongVariation
{
    [JsonProperty("Asset Suffix")] public string assetSuffix;
    [JsonProperty("Song Name")] public string songName;
    [JsonProperty("Song Credits")] public Dictionary<string, string> credits;
}
