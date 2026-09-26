using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

[InitializeOnLoad]
public static class VanillaMixPresentationValidation
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Active = "VanillaMixPresentationValidation.Active";
    private static readonly string[] Songs = { "senpai", "roses", "stress", "pico", "pico", "pico", "pico", "senpai", "stress" };
    private static readonly string[] Cases = { "Senpai-Pico-dialogue", "Roses-Pico-dialogue", "Stress-Pico-video-outro", "Pico-normal", "Pico-skip", "Pico-opponent-explodes", "Pico-player-explodes", "Senpai-base-control", "Stress-base-control" };
    private static int index = int.TryParse(Environment.GetEnvironmentVariable("UNITY_PARTY_MIX_PRESENTATION_START"), out int start) ? start : 0;
    private static int phase;
    private static int errors;
    private static double changed;
    private static double lastAdvance;
    private static double skipRequested;
    private static float pausedTime;
    private static float outroStart;
    private static AudioClip beforeOutroClip;
    private static bool finishing;
    private static bool captured;
    private static bool sawLaugh;
    private static bool sawFade;
    private static bool sawOutroSubtitle;
    private static bool sawCorpse;
    private static bool sawBlood;
    private static bool skipSent;
    private static Song song;
    private static bool parentProbe;
    private static bool parentFinished;
    private static bool parentSaved;
    private static int parentChecks;
    private static long parentElapsedTicks;
    private static float parentClipLength;
    private static string parentScoreKey;
    private static string parentSongPath;
    private static readonly Dictionary<string, (bool exists, int value)> parentIntPrefs = new Dictionary<string, (bool, int)>();
    private static bool parentClearExisted;
    private static float parentClearValue;
    private static readonly HashSet<int> corpseFrames = new HashSet<int>();
    private static float bloodScale;
    private static HashSet<string> dialogueLines;
    private static readonly HashSet<string> seenLines = new HashSet<string>();
    private static readonly List<string> completed = new List<string>();
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_MIX_PRESENTATION_PATH") ?? Path.Combine(Path.GetTempPath(), "UnityPartyMixPresentation");
    private static bool Week3 => index >= 3 && index <= 6;
    private static bool Explodes => index == 5 || index == 6;
    private static bool PlayerShoots => index != 6;
    private static VanillaCampaignPresentation Presentation => song.vanillaPlayback.Presentation;
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);

    static VanillaMixPresentationValidation()
    {
        if (!SessionState.GetBool(Active, false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        };
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run presentation validation in an isolated batch editor.");
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "SongValidation";
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool(Active, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(Cases[index] + ": " + message);
    }

    private static void Next(int value)
    {
        phase = value;
        changed = EditorApplication.timeSinceStartup;
    }

    private static void Tick()
    {
        if (finishing || !EditorApplication.isPlaying) return;
        if (changed == 0) changed = EditorApplication.timeSinceStartup;
        double elapsed = EditorApplication.timeSinceStartup - changed;
        try
        {
            Require(errors == 0 && elapsed < 90, "Errors or timeout at phase " + phase);
            if (VanillaResultsScreen.Active != null) VanillaResultsScreen.Active.Accept();
            if (phase == 0) Launch(elapsed);
            else if (phase == 90)
            {
                if (SceneManager.GetActiveScene().name != "Title") return;
                int end = int.TryParse(Environment.GetEnvironmentVariable("UNITY_PARTY_MIX_PRESENTATION_END"), out int limit) ? limit : Songs.Length;
                if (++index == end) Finish(true);
                else Next(0);
            }
            else
            {
                song = Object.FindAnyObjectByType<Song>();
                if (index == 6 && parentProbe && song != null) CheckParentProbe();
                if (index == 6 && phase == 21 && SceneManager.GetActiveScene().name == "Title")
                {
                    Require(sawCorpse && sawBlood, "Player explosion exited before showing the victim and blood.");
                    Require(parentFinished && parentSaved && parentChecks > 20, "Real SongStart did not complete without countdown and save the source result.");
                    Require(PlayerPrefs.GetInt("Freeplay.Rank." + parentScoreKey, -1) == 0
                        && PlayerPrefs.HasKey("Freeplay.Clear." + parentScoreKey)
                        && PlayerPrefs.GetFloat("Freeplay.Clear." + parentScoreKey) == 0
                        && PlayerPrefs.GetInt(parentScoreKey, 0) == 0, "Scripted completion did not retain zero rank, clear, and score.");
                    RestoreParentPreferences();
                    Passed();
                    Next(90);
                    return;
                }
                if (song?.vanillaPlayback == null) return;
                Require(!VanillaStoryCampaign.Running, "The case accidentally enabled Story Mode.");
                Require(song.vanillaPlayback.SongId == Songs[index], "Wrong song loaded.");
                Require(song.vanillaPlayback.Variation == (index < 7 ? "pico" : ""), "Wrong variation loaded.");
                if (index < 2) Dialogue(elapsed);
                else if (index == 2) Stress(elapsed);
                else if (Week3) Doppelganger(elapsed);
                else Control();
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(false); }
    }

    private static void Launch(double elapsed)
    {
        if (elapsed < 7 || Object.FindAnyObjectByType<MenuV2>() == null) return;
        OptionsV2.DesperateMode = OptionsV2.LiteMode = OptionsV2.Middlescroll = false;
        VanillaStoryCampaign.ReturnToMenu();
        VanillaFreeplay.ReturnToFreeplay = false;
        Pause.ResetSession();
        var item = VanillaFreeplayCatalog.Discover(Path.Combine(Application.streamingAssetsPath, "Bundles")).Single(entry =>
        {
            var data = JObject.Parse(File.ReadAllText(Path.Combine(entry.meta.songPath, "Vanilla.json")));
            return (string)data["song"] == Songs[index] && ((string)data["variation"] ?? "") == (index < 7 ? "pico" : "");
        });
        Song.currentSongMeta = item.meta;
        Song.difficulty = item.Difficulty("Hard");
        Song.modeOfPlay = PlayModes.Autoplay;
        if (Week3)
        {
            typeof(Pause).GetField("sessionSong", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, item.meta.songPath);
            Pause.PlayedCampaignIntro = true;
        }
        if (index == 4) Player.pauseKey = Player.keybinds.pauseKeyCode = KeyCode.Q;
        if (index < 2)
        {
            string root = Path.Combine(Application.streamingAssetsPath, "Bundles/Week6Assets/dialogue");
            var source = JObject.Parse(File.ReadAllText(Path.Combine(root, Songs[index] + "-pico.json")));
            dialogueLines = new HashSet<string>(source["dialogue"].Select(line => string.Concat(line["text"].Values<string>())));
            var original = JObject.Parse(File.ReadAllText(Path.Combine(root, Songs[index] + ".json")));
            var originalLines = original["dialogue"].Select(line => string.Concat(line["text"].Values<string>()));
            Require(!dialogueLines.SetEquals(originalLines), "Original dialogue negative control has identical lines.");
        }
        seenLines.Clear();
        captured = sawLaugh = sawFade = sawOutroSubtitle = sawCorpse = sawBlood = skipSent = false;
        lastAdvance = skipRequested = 0;
        song = null;
        SceneManager.LoadScene("Game_Backup3");
        Next(1);
    }

    private static void Dialogue(double elapsed)
    {
        Require(Presentation != null, "No presentation controller attached.");
        if (song.songStarted)
        {
            Require(captured && seenLines.SetEquals(dialogueLines), "Dialogue did not display every source line.");
            Require(!Presentation.Busy && song.uiCamera.enabled && song.battleCanvas.enabled && song.musicSources[0].isPlaying,
                "Dialogue did not restore HUD and gameplay audio.");
            Require(Pause.PlayedCampaignIntro, "Dialogue completion did not retain retry state.");
            Passed();
            ExitSong();
            return;
        }
        Text text = Presentation.GetComponentsInChildren<Text>().FirstOrDefault(item => item.name == "Dialogue");
        if (text == null) return;
        Require(Presentation.Busy && !song.IsCountingDown && !song.uiCamera.enabled && !song.battleCanvas.enabled,
            "Dialogue overlaps countdown or gameplay HUD.");
        string content = text.text.Replace("\n", "");
        if (dialogueLines.Contains(content)) seenLines.Add(content);
        if (!captured && text.text.Length > 12)
        {
            Require(text.font != null, "Dialogue font did not load.");
            CaptureDialogue();
            captured = true;
        }
        if (EditorApplication.timeSinceStartup - lastAdvance > .6)
        {
            Presentation.AdvanceDialogue();
            lastAdvance = EditorApplication.timeSinceStartup;
        }
    }

    private static void Stress(double elapsed)
    {
        Require(Presentation != null, "No presentation controller attached.");
        if (phase == 1)
        {
            if (!Presentation.VideoActive || Presentation.VideoTime < 1.5f) return;
            Require(!song.songStarted && !song.IsCountingDown && !song.uiCamera.enabled && !song.battleCanvas.enabled,
                "Video overlaps countdown or gameplay HUD.");
            Require(Presentation.Video.url.EndsWith("stress-pico.mp4") && Presentation.Video.isPlaying && Presentation.Video.frame > 0,
                "Pico video is not decoding.");
            Text caption = Presentation.GetComponentsInChildren<Text>().Single(item => item.name == "Cutscene Subtitles");
            Require(caption.text == "Back for some tasty revenge, huh?", "Pico subtitle is missing or has unstripped markup.");
            CaptureVideo();
            Pause.instance.PauseSong();
            Require(Pause.instance.IsPaused && Pause.instance.View.Labels.SequenceEqual(new[] { "Resume", "Skip Cutscene", "Restart Cutscene", "Exit to Menu" }),
                "Video pause menu is incorrect.");
            pausedTime = Presentation.VideoTime;
            Next(2);
        }
        else if (phase == 2)
        {
            if (elapsed < .7) return;
            Require(Math.Abs(Presentation.VideoTime - pausedTime) < .03 && !Presentation.Video.isPlaying, "Paused video advanced.");
            Pause.instance.ContinueSong();
            Next(3);
        }
        else if (phase == 3)
        {
            if (elapsed < .7) return;
            Require(Presentation.VideoTime > pausedTime + .3 && Presentation.Video.isPlaying, "Video did not resume.");
            if (Environment.GetEnvironmentVariable("UNITY_PARTY_MIX_PRESENTATION_FULL_VIDEO") != "1") Presentation.SkipVideo();
            Next(4);
        }
        else if (phase == 4)
        {
            if (!Presentation.VideoActive && !song.songStarted)
            {
                var cover = Presentation.GetComponentsInChildren<Image>().FirstOrDefault(image => image.name == "Overlay");
                bool covered = cover != null && cover.enabled && cover.color.a >= .999f;
                Require(covered || song.IsCountingDown, "Gameplay became visible before the countdown started.");
                if (cover != null && cover.color.a > .05f && cover.color.a < .95f)
                {
                    Require(song.IsCountingDown && !Presentation.Busy, "Reveal did not overlap the active countdown.");
                    sawFade = true;
                }
            }
            if (!song.songStarted) return;
            Require(sawFade, "No countdown reveal was observed.");
            sawFade = false;
            Require(!Presentation.Busy && !Presentation.VideoActive && song.uiCamera.enabled && song.battleCanvas.enabled,
                "Video skip did not restore gameplay.");
            song.enabled = false;
            song.respawning = true;
            foreach (AudioSource audio in song.musicSources) audio.Stop();
            Require((bool)typeof(Player).GetMethod("CanAcceptInput", Private).Invoke(Player.instance, null), "Input control did not start enabled before the outro.");
            beforeOutroClip = Field<AudioSource>(Presentation, "sound").clip;
            Require(!Presentation.AllowEnd(song), "Pico outro did not delay completion in Freeplay.");
            Next(5);
        }
        else if (phase == 5)
        {
            AudioSource audio = Field<AudioSource>(Presentation, "sound");
            if (!audio.isPlaying || audio.clip == beforeOutroClip || audio.time < .05f) return;
            Require(!song.stopwatch.IsRunning && !song.beatStopwatch.IsRunning && !(bool)typeof(Player).GetMethod("CanAcceptInput", Private).Invoke(Player.instance, null), "Outro retained gameplay input.");
            Require(Presentation.Busy && Presentation.OwnsCamera && !Presentation.OutroFinished && !song.uiCamera.enabled && !song.battleCanvas.enabled,
                "Outro did not acquire camera and HUD.");
            Require(song.vanillaPlayback.CampaignStage.CharacterGraphic(1).Animation == "stressPicoEnding", "Tankman outro pose is missing: " + song.vanillaPlayback.CampaignStage.CharacterGraphic(1).Animation + ", audio=" + audio.clip.name + ", time=" + audio.time);
            outroStart = Time.time - audio.time;
            Next(6);
        }
        else if (phase == 6)
        {
            float age = Time.time - outroStart;
            sawOutroSubtitle |= Presentation.GetComponentsInChildren<Text>().Any(text => text.text == "Singing that was cool and all but...");
            if (!Presentation.OutroFinished)
            {
                Require(Presentation.Busy && !Presentation.AllowEnd(song), "Outro released completion before its final frame.");
                if (age < 7) Require(!song.vanillaPlayback.CampaignStage.CharacterGraphic(0).Animation.StartsWith("laughEnd"), "Pico laughed before frame 176.");
                if (age > 7.6f && age < 10.5f)
                {
                    Require(song.vanillaPlayback.CampaignStage.CharacterGraphic(0).Animation.StartsWith("laughEnd"), "Pico did not laugh after frame 176.");
                    sawLaugh = true;
                }
                if (age > 11.8f)
                {
                    sawFade |= Presentation.GetComponentsInChildren<Image>().Any(image => image.name == "Overlay" && image.color.a > .2f);
                    if (!captured) { VanillaSongValidation.CaptureStage(song, Path.Combine(Output, Cases[index] + "-outro.png")); captured = true; }
                }
                return;
            }
            Require(age >= 13.2f && age < 15 && sawLaugh && sawFade && sawOutroSubtitle, "Outro observation failed: age=" + age + ", laugh=" + sawLaugh + ", fade=" + sawFade + ", subtitle=" + sawOutroSubtitle);
            Require(!Presentation.Busy && !Presentation.OwnsCamera && Presentation.AllowEnd(song), "Outro did not release completion.");
            Require(Presentation.GetComponentsInChildren<Image>().Any(image => image.name == "Overlay" && image.isActiveAndEnabled && image.color == Color.black),
                "Stress Pico outro removed its final cover before the scene handoff.");
            Passed();
            ExitSong();
        }
    }

    private static void Doppelganger(double elapsed)
    {
        if (phase == 1)
        {
            if (!song.songStarted) return;
            Require(Presentation != null && !Presentation.Busy, "Suppressed intro still runs.");
            song.enabled = false;
            song.songStarted = false;
            song.stopwatch.Stop();
            song.beatStopwatch.Stop();
            foreach (AudioSource audio in song.musicSources) audio.Stop();
            if (index == 6) BeginParentProbe();
            else
            {
                var routine = (IEnumerator)typeof(VanillaCampaignPresentation).GetMethod("PicoDoppelgangerIntro", Private).Invoke(Presentation, null);
                Random.State state = Random.state;
                try
                {
                    Random.InitState(Seed(PlayerShoots, Explodes));
                    Presentation.StartCoroutine(routine);
                }
                finally { Random.state = state; }
            }
            Next(20);
            return;
        }
        var stage = song.vanillaPlayback.Week3Stage;
        AudioSource music = Field<AudioSource>(Presentation, "music");
        var effects = Field<List<VanillaWeek2Graphic>>(stage, "mixEffects");
        if (phase == 20)
        {
            if (!music.isPlaying || music.time < .7f) return;
            Require(Presentation.Busy && Presentation.OwnsCamera && !song.songStarted && !song.IsCountingDown,
                "Seeded cutscene did not acquire playback ownership.");
            Require(!stage.CharacterGraphic(0).gameObject.activeSelf && !stage.CharacterGraphic(1).gameObject.activeSelf,
                "Original actors overlap the doppelgangers.");
            var actors = effects.Where(graphic => graphic.name == "doppleganger").ToArray();
            Require(actors.Length == 2 && actors.All(graphic => graphic.gameObject.activeInHierarchy && graphic.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0),
                "Doppelganger graphics are absent or empty.");
            Require(actors[0].Animation == (PlayerShoots ? "shoot" : "explode") + "Player"
                && actors[1].Animation == (!PlayerShoots ? "shoot" : Explodes ? "explode" : "cigarette") + "Opponent",
                "Seed control did not choose the requested shooter and explosion outcome.");
            Require(music.clip != null && music.clip.name != "missing-cutscene-control", "Cutscene music did not load.");
            VanillaSongValidation.CaptureStage(song, Path.Combine(Output, Cases[index] + ".png"));
            captured = true;
            Next(21);
        }
        else if (phase == 21)
        {
            if (index == 4 && !skipSent && music.time >= 2)
            {
                typeof(Song).GetMethod("HandleManualStartExit", Private).Invoke(song, new object[] { true });
                Require(!LoadingTransition.instance.toggled && !Pause.instance.Transitioning && !Pause.instance.IsPaused,
                    "Remapped pause input reached exit or pause during the cutscene.");
                Presentation.AdvanceDialogue();
                skipSent = true;
                skipRequested = EditorApplication.timeSinceStartup;
            }
            if (index == 4 && skipSent && EditorApplication.timeSinceStartup - skipRequested >= .3
                && EditorApplication.timeSinceStartup - skipRequested < .5)
                Require(Presentation.Busy, "The first skip input skipped immediately.");
            if (index == 4 && skipSent && EditorApplication.timeSinceStartup - skipRequested >= .7 && skipRequested > 0)
            {
                Presentation.AdvanceDialogue();
                skipRequested = -1;
            }
            sawCorpse |= effects.Any(graphic => graphic.gameObject.activeSelf && graphic.Animation == "loop" + (PlayerShoots ? "Opponent" : "Player"));
            sawBlood |= effects.Any(graphic => graphic.name == "bloodPool" && graphic.gameObject.activeSelf && graphic.Animation == "poolAnim");
            if (Presentation.Busy) return;
            Require(index != 6, "Player explosion resumed gameplay instead of exiting.");
            Require(captured && !music.isPlaying && !Presentation.OwnsCamera && song.uiCamera.enabled && song.battleCanvas.enabled,
                "Doppelganger completion did not restore the HUD and camera.");
            Require(stage.OpponentExploded == Explodes, "Explosion outcome was not retained.");
            Require(stage.CharacterGraphic(0).gameObject.activeSelf && stage.CharacterGraphic(1).gameObject.activeSelf != Explodes,
                "Doppelganger completion restored the wrong actor.");
            if (Explodes)
            {
                Require(sawCorpse && sawBlood && song.OpponentVocals.mute, "Opponent explosion lost corpse, blood, or vocal mute.");
                stage.ResetStage();
                Require(stage.OpponentExploded && !stage.CharacterGraphic(1).gameObject.activeSelf, "Stage reset revived the exploded opponent.");
                Pause.instance.RestartSong();
                Next(22);
                return;
            }
            else
            {
                Require(!song.OpponentVocals.mute && !sawCorpse && !sawBlood, "Normal/skip negative control retained explosion state.");
                Require(effects.Where(graphic => graphic.name == "doppleganger").All(graphic => !graphic.gameObject.activeSelf), "Doppelgangers remain visible after completion.");
                if (index == 3) Require(effects.Single(graphic => graphic.name == "cigarette").gameObject.activeSelf, "Normal ending omitted the cigarette.");
                if (index == 4) Require(elapsed < 7, "Skip completed at the normal cutscene deadline.");
            }
            Passed();
            ExitSong();
        }
        else if (phase == 22)
        {
            Require(!Presentation.Busy, "Retry replayed the doppelganger intro.");
            if (!song.songStarted) return;
            Require(stage.OpponentExploded && !stage.CharacterGraphic(1).gameObject.activeSelf && stage.CharacterGraphic(0).gameObject.activeSelf,
                "Scene reload lost the opponent explosion outcome.");
            Require(song.OpponentVocals.mute && effects.Any(graphic => graphic.gameObject.activeInHierarchy && graphic.Animation == "loopOpponent")
                && effects.Any(graphic => graphic.name == "bloodPool" && graphic.gameObject.activeInHierarchy),
                "Scene reload lost exploded-opponent vocals, corpse, or blood: mute=" + song.OpponentVocals.mute + ", effects=" + string.Join(",", effects.Select(graphic => graphic.name + "/" + graphic.Animation + "/" + graphic.gameObject.activeInHierarchy)));
            corpseFrames.Clear();
            bloodScale = effects.Single(graphic => graphic.name == "bloodPool").transform.localScale.x;
            Next(23);
        }
        else if (phase == 23)
        {
            corpseFrames.Add(effects.Single(graphic => graphic.Animation == "loopOpponent").Frame);
            if (elapsed < 1) return;
            Require(corpseFrames.Count > 1, "Retry corpse loop froze.");
            Require(effects.Single(graphic => graphic.name == "bloodPool").transform.localScale.x > bloodScale + .01f,
                "Retry blood pool did not grow after its final frame.");
            Passed();
            ExitSong();
        }
    }

    private static void BeginParentProbe()
    {
        song.StopCoroutine("SongStart");
        song.beatStopwatch = null;
        song.vocalSource.Stop();
        if (song.OpponentVocals != null) song.OpponentVocals.Stop();
        song.ResetFunkinScore();
        song.playerOneStats = new PlayerStat();
        song.playerTwoStats = new PlayerStat();
        Player.instance.ClearInput();
        Player.demoMode = false;
        Song.modeOfPlay = PlayModes.Boyfriend;
        typeof(VanillaCampaignPresentation).GetField("playedIntro", Private).SetValue(Presentation, false);
        Pause.PlayedCampaignIntro = false;
        Require(!Pause.PracticeMode && Pause.DeathCount == 0, "Parent probe inherited practice or death state.");
        parentElapsedTicks = song.stopwatch.ElapsedTicks;
        parentClipLength = song.musicClip.length;
        parentSongPath = Song.currentSongMeta.songPath;
        parentScoreKey = Song.currentSongMeta.songName + Song.currentSongMeta.bundleMeta.bundleName
            + Song.difficulty.ToLowerInvariant() + PlayModes.Boyfriend;
        parentIntPrefs.Clear();
        foreach (string key in new[] { parentScoreKey, "Freeplay.Rank." + parentScoreKey })
        {
            parentIntPrefs[key] = (PlayerPrefs.HasKey(key), PlayerPrefs.GetInt(key, 0));
            PlayerPrefs.DeleteKey(key);
        }
        string clearKey = "Freeplay.Clear." + parentScoreKey;
        parentClearExisted = PlayerPrefs.HasKey(clearKey);
        parentClearValue = PlayerPrefs.GetFloat(clearKey, 0);
        PlayerPrefs.DeleteKey(clearKey);
        PlayerPrefs.Save();
        VanillaFreeplay.ReturnToFreeplay = true;
        VanillaFreeplay.ArmRankReturn(Song.currentSongMeta, Song.difficulty, PlayModes.Boyfriend);
        parentProbe = true;
        parentFinished = parentSaved = false;
        parentChecks = 0;
        CheckParentProbe();
        var routine = (IEnumerator)typeof(Song).GetMethod("SongStart", Private).Invoke(song, new object[] { 0f });
        song.StartCoroutine(SeededParent(routine, Seed(false, true), true));
    }

    private static IEnumerator SeededParent(IEnumerator routine, int seed, bool root = false)
    {
        while (true)
        {
            Random.State previous = Random.state;
            bool moved;
            object current;
            try
            {
                Random.InitState(seed);
                moved = routine.MoveNext();
                current = moved ? routine.Current : null;
            }
            finally { Random.state = previous; }
            CheckParentProbe();
            if (!moved) break;
            if (current is IEnumerator nested) yield return SeededParent(nested, seed);
            else yield return current;
        }
        if (root) parentFinished = true;
    }

    private static void CheckParentProbe()
    {
        if (!parentProbe || song == null) return;
        parentChecks++;
        Require(!song.IsCountingDown && !song.songStarted, "Real SongStart entered countdown or gameplay after scripted completion.");
        Require(song.musicSources.All(source => !source.isPlaying) && !song.vocalSource.isPlaying
            && (song.OpponentVocals == null || !song.OpponentVocals.isPlaying), "Real SongStart restarted gameplay audio.");
        Require(!(bool)typeof(Player).GetMethod("CanAcceptInput", Private).Invoke(Player.instance, null), "Real SongStart enabled gameplay input.");
        Require(!Presentation.GetComponentsInChildren<RawImage>().Any(image => image.name == "Countdown"), "Real SongStart created countdown graphics.");
        Require(song.stopwatch.ElapsedTicks == parentElapsedTicks && !song.stopwatch.IsRunning
            && song.beatStopwatch == null && song.musicClip.length == parentClipLength, "Parent probe altered the elapsed clock, beat clock, or source duration.");
        Require(!song.FreeplayAborted && !song.isDead, "Scripted completion used the abort or game-over route.");
        if (song.songSetupDone || parentSaved) return;
        Require(PlayerPrefs.GetInt("Freeplay.Rank." + parentScoreKey, -1) == 0
            && PlayerPrefs.HasKey("Freeplay.Clear." + parentScoreKey)
            && PlayerPrefs.GetFloat("Freeplay.Clear." + parentScoreKey) == 0
            && PlayerPrefs.GetInt(parentScoreKey, 0) == 0, "Scripted completion failed to save the source zero result.");
        var change = (VanillaFreeplayRankChange)typeof(VanillaFreeplay).GetField("pendingRank", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        Require(change != null && change.songPath == parentSongPath && change.difficulty == Song.difficulty
            && change.mode == PlayModes.Boyfriend && change.oldRank == -1 && change.newRank == 0,
            "Scripted completion did not queue its Freeplay rank return.");
        parentSaved = true;
    }

    private static void RestoreParentPreferences()
    {
        if (parentScoreKey == null) return;
        foreach (var entry in parentIntPrefs)
        {
            if (entry.Value.exists) PlayerPrefs.SetInt(entry.Key, entry.Value.value);
            else PlayerPrefs.DeleteKey(entry.Key);
        }
        string clearKey = "Freeplay.Clear." + parentScoreKey;
        if (parentClearExisted) PlayerPrefs.SetFloat(clearKey, parentClearValue);
        else PlayerPrefs.DeleteKey(clearKey);
        PlayerPrefs.Save();
        parentIntPrefs.Clear();
        parentScoreKey = null;
        parentProbe = false;
    }

    private static int Seed(bool shooter, bool explosion)
    {
        Random.State state = Random.state;
        try
        {
            for (int seed = 0; seed < 100000; seed++)
            {
                Random.InitState(seed);
                bool player = Random.value < .5f;
                bool boom = Random.value < .08f;
                if (player == shooter && boom == explosion) return seed;
            }
            throw new InvalidOperationException("Cannot find a cutscene seed.");
        }
        finally { Random.state = state; }
    }

    private static void Control()
    {
        if (Presentation != null)
            Require(!Presentation.Busy && !Presentation.VideoActive && !Presentation.GetComponentsInChildren<Text>().Any(text => text.name == "Dialogue"),
                "Base Freeplay song entered a Pico presentation.");
        if (!song.songStarted) return;
        Require(song.uiCamera.enabled && song.battleCanvas.enabled, "Base Freeplay HUD stayed hidden.");
        if (index == 8) Require(Presentation.AllowEnd(song) && !Presentation.Busy, "Base Stress entered the Pico outro.");
        Passed();
        ExitSong();
    }

    private static void Passed()
    {
        completed.Add(Cases[index]);
        Debug.Log("MIX PRESENTATION PASSED: " + Cases[index]);
    }

    private static void ExitSong()
    {
        song.respawning = false;
        VanillaStoryCampaign.ReturnToMenu();
        Pause.instance.QuitSong();
        Next(90);
    }

    private static void CaptureVideo()
    {
        RenderTexture previous = RenderTexture.active;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = Presentation.Video.targetTexture;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            Require(texture.GetPixels32().Count(pixel => pixel.r > 40 || pixel.g > 40 || pixel.b > 40) > 10000, "Decoded video is blank.");
            File.WriteAllBytes(Path.Combine(Output, Cases[index] + ".png"), texture.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; Object.DestroyImmediate(texture); }
    }

    private static void CaptureDialogue()
    {
        Canvas canvas = Presentation.GetComponentInChildren<Canvas>();
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        Transform[] transforms = canvas.GetComponentsInChildren<Transform>(true);
        int[] layers = transforms.Select(item => item.gameObject.layer).ToArray();
        var host = new GameObject("Mix Presentation Capture");
        Camera camera = host.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 360;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.gray;
        camera.cullingMask = 1 << 31;
        camera.transform.position = new Vector3(0, 0, -1000);
        camera.farClipPlane = 2000;
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        var target = new RenderTexture(1280, 720, 24);
        RenderTexture previous = RenderTexture.active;
        var features = AssetDatabase.FindAssets("t:UniversalRendererData")
            .Select(id => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(id)))
            .SelectMany(data => data.rendererFeatures).Where(feature => feature != null && feature.isActive).Distinct().ToArray();
        try
        {
            foreach (Transform item in transforms) item.gameObject.layer = 31;
            foreach (var feature in features) feature.SetActive(false);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1000;
            scaler.enabled = false;
            canvas.scaleFactor = 1;
            Canvas.ForceUpdateCanvases();
            foreach (var graphic in canvas.GetComponentsInChildren<VanillaDialogueGraphic>())
                Require(graphic.canvasRenderer.GetMesh()?.vertexCount > 0, "Dialogue artwork has no canvas mesh: " + graphic.name);
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1) camera.cullingMask = 0;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    image.Apply();
                    int colored = image.GetPixels32().Count(pixel => Math.Abs(pixel.r - pixel.g) > 45 || Math.Abs(pixel.g - pixel.b) > 45);
                    Require(pass == 0 ? colored > 3000 : colored == 0, "Dialogue render or hidden-camera control failed: " + colored);
                    if (pass == 0) File.WriteAllBytes(Path.Combine(Output, Cases[index] + ".png"), image.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(image); }
            }
        }
        finally
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            scaler.enabled = true;
            for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
            foreach (var feature in features) feature.SetActive(true);
            RenderTexture.active = previous;
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(target);
        }
    }

    private static void Finish(bool passed)
    {
        RestoreParentPreferences();
        finishing = true;
        SessionState.SetBool(Active, false);
        string result = "MIX PRESENTATION VALIDATION: passed=" + passed + ", errors=" + errors + ", case=" + index + ", phase=" + phase;
        File.WriteAllText(Path.Combine(Output, "result.txt"), result + "\n" + string.Join("\n", completed) + "\n");
        Debug.Log(result);
        EditorApplication.Exit(passed ? 0 : 1);
    }
}

