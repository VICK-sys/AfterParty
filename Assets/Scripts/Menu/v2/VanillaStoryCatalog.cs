using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

[Serializable]
public sealed class VanillaStoryAnimation
{
    public string name;
    public string prefix;
    public int frameRate = 24;
    public bool looped;
    public bool flipX;
    public bool flipY;
    public int[] frameIndices;
    public float[] offsets = { 0, 0 };
}

[Serializable]
public sealed class VanillaStoryPropData
{
    public string assetPath;
    public float scale = 1;
    public float alpha = 1;
    public bool isPixel;
    public float danceEvery = 1;
    public float[] offsets = { 0, 0 };
    public string startingAnimation = "";
    public bool flipX;
    public bool flipY;
    public VanillaStoryAnimation[] animations = Array.Empty<VanillaStoryAnimation>();
}

[Serializable]
public sealed class VanillaStoryLevel
{
    public string id;
    public string name;
    public string titleAsset;
    public string background = "#F9CF51";
    public bool visible = true;
    public string[] songs;
    public string[] songNames;
    public string[] difficulties;
    public VanillaStoryPropData[] props;

    public string[] Tracks
    {
        get
        {
            if (id == "sserafim") return new[] { "SPAGHETTI", "(feat. j-hope of BTS)", "(Clean ver.)" };
            if (id == "weekend1") return VanillaStoryCampaign.HasBeaten(id)
                ? new[] { "Darnell", "Lit Up", "2hot", "Blazin'" } : new[] { "Darnell", "Lit Up", "2hot" };
            return songNames;
        }
    }
}

public static class VanillaStoryCatalog
{
    public static List<VanillaStoryLevel> Load()
    {
        return JsonConvert.DeserializeObject<List<VanillaStoryLevel>>(Resources.Load<TextAsset>("VanillaStory/levels").text)
            .Where(level => level.visible && (level.id == "tutorial" || level.id == "week1" || level.id == "week2" || level.id == "week3"
                || level.id == "week4" || level.id == "week5" || level.id == "week6" || level.id == "week7" || level.id == "weekend1" || level.id == "sserafim")).ToList();
    }

    public static bool TryPlaylist(VanillaStoryLevel level, string difficulty, out List<VanillaFreeplaySong> playlist, out string missing)
    {
        return TryPlaylist(level, difficulty, Path.Combine(Application.streamingAssetsPath, "Bundles"), out playlist, out missing);
    }

    public static bool TryPlaylist(VanillaStoryLevel level, string difficulty, string root, out List<VanillaFreeplaySong> playlist, out string missing)
    {
        playlist = new List<VanillaFreeplaySong>();
        var unavailable = new List<string>();
        string manifest = Path.Combine(root, "vanilla-import.json");
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        if (File.Exists(manifest))
            foreach (JToken song in JObject.Parse(File.ReadAllText(manifest))["songs"])
                if (string.IsNullOrEmpty((string)song["variation"]))
                    paths[(string)song["id"]] = Path.GetFullPath(Path.Combine(root, (string)song["path"]));
        var installed = VanillaFreeplayCatalog.Discover(root).ToDictionary(song => song.id, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < level.songs.Length; i++)
        {
            if (!paths.TryGetValue(level.songs[i], out string path) || !installed.TryGetValue(path, out VanillaFreeplaySong song)
                || song.Difficulty(difficulty) == null)
                unavailable.Add(level.songNames[i]);
            else
                playlist.Add(song);
        }
        missing = string.Join(", ", unavailable);
        if (unavailable.Count == 0 && playlist.Count > 0) return true;
        playlist.Clear();
        return false;
    }
}
