using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class VanillaFreeplaySong
{
    public SongMetaV2 meta;
    public string id;
    public string week;
    public readonly Dictionary<string, JObject> details = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
    public string FavoriteKey => "Freeplay.Favorite." + meta.bundleMeta.bundleName + ":" + Path.GetFileName(meta.songPath);
    public bool Favorite => PlayerPrefs.GetInt(FavoriteKey, 0) == 1;
    public string Difficulty(string requested) => meta.difficulties.Keys.FirstOrDefault(d => string.Equals(d, requested, StringComparison.OrdinalIgnoreCase));
    public JObject Details(string difficulty) => details.TryGetValue(difficulty, out JObject value) ? value : null;
    public string Title(string difficulty) => meta.GetVariation(difficulty)?.songName ?? meta.songName;
    public float Bpm(string difficulty) => (float?)Details(difficulty)?["timeChanges"]?.First?["bpm"] ?? 0;
    public int Rating(string difficulty) => (int?)Details(difficulty)?["playData"]?["ratings"]?[difficulty.ToLowerInvariant()] ?? 0;
    public string Icon(string difficulty) => (string)Details(difficulty)?["playData"]?["characters"]?["opponent"] ?? "bf";
    public string Album(string difficulty) => (string)Details(difficulty)?["playData"]?["album"];
    public bool IsNew => details.Values.Any(data => (string)data["playData"]?["album"] == "spaghetti")
        && !new[] { "Easy", "Normal", "Hard" }.Any(difficulty => PlayerPrefs.HasKey("Freeplay.Rank." + ScoreKey(difficulty, PlayModes.Boyfriend)));
    public string[] Instrumentals(string difficulty)
    {
        JToken characters = Details(difficulty)?["playData"]?["characters"];
        return new[] { (string)characters?["instrumental"] ?? "" }
            .Concat(characters?["altInstrumentals"]?.Values<string>() ?? Enumerable.Empty<string>()).ToArray();
    }

    public string InstrumentalPath(string difficulty, string instrumental)
    {
        if (instrumental == Instrumentals(difficulty)[0]) return meta.AssetPath("Inst.ogg", difficulty);
        string direct = Path.Combine(meta.songPath, "Inst" + (string.IsNullOrEmpty(instrumental) ? "" : "-" + instrumental) + ".ogg");
        if (File.Exists(direct)) return direct;
        string siblingName = Path.GetFileName(meta.songPath) + "-" + instrumental;
        string sibling = Directory.GetDirectories(Path.GetDirectoryName(meta.songPath))
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), siblingName, StringComparison.OrdinalIgnoreCase));
        if (sibling == null) return direct;
        return Path.Combine(sibling, "Inst.ogg");
    }

    public float InstrumentalStart(string difficulty, string instrumental) =>
        -((float?)Details(difficulty)?["offsets"]?["altInstrumentals"]?[instrumental] ?? 0) / 1000;
    public string ScoreKey(string difficulty, int mode) => meta.songName + meta.bundleMeta.bundleName + difficulty.ToLowerInvariant() + mode;

    public void ToggleFavorite()
    {
        PlayerPrefs.SetInt(FavoriteKey, Favorite ? 0 : 1);
        PlayerPrefs.Save();
    }
}

public static class VanillaFreeplayCatalog
{
    private static readonly string[] StandardDifficulties = { "Easy", "Normal", "Hard", "Erect", "Nightmare" };

    public static List<VanillaFreeplaySong> Discover(params string[] roots)
    {
        var songs = new List<VanillaFreeplaySong>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string root in roots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (string bundlePath in Directory.GetDirectories(root).OrderBy(p => p, StringComparer.Ordinal))
            {
                string bundleFile = Path.Combine(bundlePath, "bundle-meta.json");
                if (!File.Exists(bundleFile)) continue;
                BundleMeta bundle;
                try { bundle = JsonConvert.DeserializeObject<BundleMeta>(File.ReadAllText(bundleFile)); }
                catch (Exception e) { Debug.LogWarning("Cannot read bundle " + bundlePath + ": " + e.Message); continue; }
                if (bundle == null) continue;
                foreach (string path in Directory.GetDirectories(bundlePath).OrderBy(p => p, StringComparer.Ordinal))
                {
                    if (!seen.Add(Path.GetFullPath(path)) || !File.Exists(Path.Combine(path, "meta.json"))) continue;
                    try
                    {
                        var meta = JsonConvert.DeserializeObject<SongMetaV2>(File.ReadAllText(Path.Combine(path, "meta.json")));
                        if (meta?.difficulties == null || string.IsNullOrWhiteSpace(meta.songName)) continue;
                        meta.songPath = path;
                        meta.bundleMeta = bundle;
                        meta.credits = meta.credits ?? new Dictionary<string, string>();
                        meta.difficulties = meta.difficulties.Where(d => File.Exists(meta.AssetPath("Inst.ogg", d.Key)) && File.Exists(Path.Combine(path, "Chart-" + d.Key.ToLowerInvariant() + ".json")))
                            .ToDictionary(d => d.Key, d => d.Value);
                        if (meta.difficulties.Count == 0) continue;
                        var song = new VanillaFreeplaySong { meta = meta, id = Path.GetFullPath(path), week = bundle.bundleName };
                        foreach (string difficulty in meta.difficulties.Keys)
                        {
                            string suffix = meta.GetVariation(difficulty)?.assetSuffix ?? string.Empty;
                            string source = Path.Combine(path, "Source", "metadata" + suffix + ".json");
                            if (File.Exists(source)) song.details[difficulty] = JObject.Parse(File.ReadAllText(source));
                            else
                            {
                                JToken chart = JObject.Parse(File.ReadAllText(Path.Combine(path, "Chart-" + difficulty.ToLowerInvariant() + ".json")))["song"];
                                song.details[difficulty] = new JObject
                                {
                                    ["timeChanges"] = new JArray(new JObject { ["bpm"] = (float?)chart?["bpm"] ?? 0 }),
                                    ["playData"] = new JObject { ["characters"] = new JObject { ["opponent"] = (string)chart?["player2"] ?? "bf" } }
                                };
                            }
                        }
                        if (Path.GetFileName(bundlePath) == "00-Tutorial") song.week = "Tutorial";
                        if (Path.GetFileName(bundlePath) == "01-Week1") song.week = "Week 1";
                        if (Path.GetFileName(bundlePath) == "02-Week2") song.week = "Week 2";
                        if (Path.GetFileName(bundlePath) == "03-Week3") song.week = "Week 3";
                        if (Path.GetFileName(bundlePath) == "04-Week4") song.week = "Week 4";
                        if (Path.GetFileName(bundlePath) == "05-Week5") song.week = "Week 5";
                        if (Path.GetFileName(bundlePath) == "06-Week6") song.week = "Week 6";
                        if (Path.GetFileName(bundlePath) == "07-Week7") song.week = "Week 7";
                        if (Path.GetFileName(bundlePath) == "08-Weekend1") song.week = "Weekend 1";
                        if (Path.GetFileName(bundlePath) == "09-Sserafim") song.week = "SP. COLLAB 1";
                        songs.Add(song);
                    }
                    catch (Exception e) { Debug.LogWarning("Cannot read Freeplay song " + path + ": " + e.Message); }
                }
            }
        }
        return songs;
    }

    public static List<string> Difficulties(IEnumerable<VanillaFreeplaySong> songs)
    {
        var available = songs.SelectMany(s => s.meta.difficulties.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return available.OrderBy(d => { int index = Array.FindIndex(StandardDifficulties, s => string.Equals(s, d, StringComparison.OrdinalIgnoreCase)); return index < 0 ? 100 : index; })
            .ThenBy(d => d, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool Matches(VanillaFreeplaySong song, string filter)
    {
        if (filter == "ALL") return true;
        if (filter == "fav") return song.Favorite;
        char first = char.ToUpperInvariant(song.meta.songName[0]);
        if (filter == "#") return char.IsDigit(first);
        return first >= filter[0] && first <= filter[filter.Length - 1];
    }

    public static VanillaFreeplayRankChange SaveCompletion(SongMetaV2 meta, string difficulty, int mode, PlayerStat stats, bool completed, int totalNotes)
    {
        if (!completed || mode != 1 || totalNotes <= 0) return null;
        string key = meta.songName + meta.bundleMeta.bundleName + difficulty.ToLowerInvariant() + mode;
        var results = VanillaResultsData.Capture(stats, totalNotes);
        float clear = (float)results.Clear;
        int rank = results.Rank;
        int oldRank = PlayerPrefs.GetInt("Freeplay.Rank." + key, -1);
        PlayerPrefs.SetFloat("Freeplay.Clear." + key, Mathf.Max(clear, PlayerPrefs.GetFloat("Freeplay.Clear." + key, 0)));
        PlayerPrefs.SetInt("Freeplay.Rank." + key, Mathf.Max(rank, oldRank));
        PlayerPrefs.Save();
        return rank > oldRank ? new VanillaFreeplayRankChange
        {
            songPath = meta.songPath, difficulty = difficulty, mode = mode, oldRank = oldRank, newRank = rank
        } : null;
    }
}
