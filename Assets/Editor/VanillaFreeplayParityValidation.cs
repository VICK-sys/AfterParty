using System;
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
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaFreeplayParityValidation
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static VanillaFreeplay freeplay;
    private static MenuV2 menu;
    private static int assertions;
    private static int errors;
    private static double started;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_FREEPLAY_TEST_PATH");

    static VanillaFreeplayParityValidation()
    {
        if (!SessionState.GetBool("FreeplayParity.Active", false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        };
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in an isolated batch editor.");
        Directory.CreateDirectory(Output);
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "FreeplayParityValidation";
        SessionState.SetBool("FreeplayParity.Active", true);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        EditorApplication.EnterPlaymode();
    }

    private static T Field<T>(string name) => (T)typeof(VanillaFreeplay).GetField(name, Instance).GetValue(freeplay);
    private static void Set(string name, object value) => typeof(VanillaFreeplay).GetField(name, Instance).SetValue(freeplay, value);
    private static void Call(string name, params object[] args) => typeof(VanillaFreeplay).GetMethod(name, Instance).Invoke(freeplay, args);
    private static void Require(bool value, string message)
    {
        assertions++;
        if (!value) throw new InvalidOperationException(message);
    }

    private static void Advance(float seconds)
    {
        while (seconds > 0)
        {
            float delta = Mathf.Min(seconds, 1f / 120);
            foreach (string field in new[] { "age", "capsuleAge", "selectionAge" }) Set(field, Field<float>(field) + delta);
            if (Field<float>("confirmAge") >= 0) Set("confirmAge", Field<float>("confirmAge") + delta);
            foreach (var sprite in freeplay.GetComponentsInChildren<VanillaFreeplaySprite>()) sprite.Tick(delta);
            foreach (var animation in freeplay.GetComponentsInChildren<VanillaFreeplayAnimate>()) animation.Tick(delta);
            Call("Draw", delta);
            seconds -= delta;
        }
    }

    private static void Select(string name)
    {
        int index = Field<List<VanillaFreeplaySong>>("filtered").FindIndex(song => song.meta.songName == name) + 1;
        Require(index > 0, "Missing fixture song: " + name);
        freeplay.MoveSelection(index - freeplay.SelectedIndex);
    }

    private static Transform Row => Field<RectTransform>("list").GetChild(freeplay.SelectedIndex);

    private static void Capture(string name)
    {
        typeof(VanillaFreeplayValidation).GetField("freeplay", Static).SetValue(null, freeplay);
        typeof(VanillaFreeplayValidation).GetField("menu", Static).SetValue(null, menu);
        typeof(VanillaFreeplayValidation).GetMethod("Capture", Static).Invoke(null, new object[] { name, 1280, 720, false, .1f });
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (started == 0) started = EditorApplication.timeSinceStartup;
        if (EditorApplication.timeSinceStartup - started < 3) return;
        EditorApplication.update -= Tick;
        bool passed = false;
        string failure = null;
        try
        {
            menu = Object.FindAnyObjectByType<MenuV2>();
            if (VanillaTitleScreen.Active != null)
                typeof(VanillaTitleScreen).GetMethod("CompleteMainMenuTransition", Instance).Invoke(VanillaTitleScreen.Active, null);
            PlayerPrefs.SetString("Freeplay.Character", "bf");
            VanillaFreeplay.RememberDifficulty("Normal");
            freeplay = VanillaFreeplay.Open(menu, true, Path.Combine(Output, "EmptyBundles"));
            freeplay.enabled = false;
            Set("ready", false);
            Call("RebuildList", true);
            Call("Draw", 0f);
            CheckEntranceDetails();
            Advance(.05f);
            CheckEntranceDetails();
            var entranceRow = Field<RectTransform>("list").GetChild(1) as RectTransform;
            Require(entranceRow.anchoredPosition.x < Field<float>("viewportWidth"), "Entrance row did not move onscreen.");
            Require(entranceRow.Find("Details/Title Clip/Title").localScale == new Vector3(.4f, 1.4f, 1),
                "Entrance title squash did not start with the row.");
            Capture("rows-entering.png");
            var entranceIcon = entranceRow.Find("Details/Icon").GetComponent<VanillaFreeplaySprite>();
            entranceIcon.enabled = false;
            bool hiddenIconRejected = false;
            try { CheckEntranceDetails(); }
            catch (InvalidOperationException) { hiddenIconRejected = true; }
            finally { entranceIcon.enabled = true; }
            Require(hiddenIconRejected, "Hidden entrance icon control passed.");
            var entranceTitle = entranceRow.Find("Details/Title Clip").gameObject;
            entranceTitle.SetActive(false);
            bool hiddenTitleRejected = false;
            try { CheckEntranceDetails(); }
            catch (InvalidOperationException) { hiddenTitleRejected = true; }
            finally { entranceTitle.SetActive(true); }
            Require(hiddenTitleRejected, "Hidden entrance title control passed.");
            Advance(.1f);
            CheckEntranceDetails();
            Require(entranceRow.Find("Details/Title Clip/Title").localScale == Vector3.one,
                "Entrance title squash did not finish after three frames.");
            Set("ready", true);
            Advance(2);
            Select("Bopeebo");
            Advance(.8f);
            Require(Row.Find("Details/Title Clip/Title").GetComponent<VanillaFreeplayCapsuleText>() != null, "Native capsule text was not used.");
            var album = Field<VanillaFreeplayAnimate>("album");
            int albumFrame = album.CurrentFrame;
            Select("Fresh");
            Require(album.CurrentFrame == albumFrame, "Same album restarted.");
            Select(Field<List<VanillaFreeplaySong>>("filtered").First(song => song.Album(freeplay.Difficulty) != null
                && song.Album(freeplay.Difficulty) != freeplay.SelectedSong.Album(freeplay.Difficulty)).meta.songName);
            Require(album.CurrentLabel == "switch", "Different album did not switch.");
            Require(Field<VanillaFreeplaySprite>("albumTitle").CurrentFrameName.StartsWith("switch"), "Album title did not switch.");
            Select("Bopeebo");
            freeplay.ChangeFilter(1);
            Require(Field<List<VanillaFreeplaySong>>("filtered").Select(song => song.meta.songName).SequenceEqual(new[] { "Blammed", "Bopeebo" }), "Letter filter order differs.");
            Advance(1f / 24);
            Require(!Field<List<VanillaFreeplayAnimate>>("filterLetters")[0].gameObject.activeSelf, "Filter frame-one hide is missing.");
            Capture("filter-moving.png");
            Advance(.2f);
            freeplay.ChangeFilter(-1);
            Advance(.2f);
            Require(Field<List<VanillaFreeplaySong>>("filtered").FindIndex(song => song.meta.songName == "Bopeebo")
                < Field<List<VanillaFreeplaySong>>("filtered").FindIndex(song => song.meta.songName == "Blammed"), "ALL was alphabetized.");
            freeplay.ChangeDifficulty(1);
            var difficultyLabels = Field<Dictionary<string, Graphic>>("difficultyLabels");
            CheckDifficultyVisibility();
            Require(Field<RectTransform>("difficultyLabelRoot").GetSiblingIndex()
                < Field<VanillaFreeplaySprite>("backing").transform.GetSiblingIndex(), "Difficulty label draws over the backdrop.");
            Capture("difficulty-start.png");
            int difficultySelection = freeplay.SelectedIndex;
            freeplay.MoveSelection(1);
            Require(freeplay.Busy && freeplay.SelectedIndex == difficultySelection, "Difficulty transition did not lock input.");
            Advance(.1f);
            CheckDifficultyVisibility();
            Require(Mathf.Abs(Field<Dictionary<string, Graphic>>("difficultyLabels")["Hard"].rectTransform.anchoredPosition.x - 295) < 1,
                "Difficulty midpoint differs.");
            Capture("difficulty-moving.png");
            Advance(.2f);
            CheckDifficultyVisibility();
            difficultyLabels["Normal"].gameObject.SetActive(true);
            bool staleDifficultyRejected = false;
            try { CheckDifficultyVisibility(); }
            catch (InvalidOperationException) { staleDifficultyRejected = true; }
            finally { difficultyLabels["Normal"].gameObject.SetActive(false); }
            Require(staleDifficultyRejected, "Stale difficulty label control passed.");
            foreach (int direction in new[] { -1, 1 })
            {
                freeplay.ChangeDifficulty(direction);
                CheckDifficultyVisibility();
                Advance(.1f);
                CheckDifficultyVisibility();
                float midpoint = direction > 0 ? 295 : -115;
                Require(Mathf.Abs(difficultyLabels[freeplay.Difficulty].rectTransform.anchoredPosition.x - midpoint) < 1,
                    "Directional difficulty midpoint differs.");
                Capture(direction > 0 ? "difficulty-right.png" : "difficulty-left.png");
                Set("lastUpdateTime", Time.realtimeSinceStartupAsDouble - .25);
                Call("Update");
                Require(Field<float>("difficultyAge") >= .2f, "Slow frame stretched the difficulty transition.");
                CheckDifficultyVisibility();
                Require(Mathf.Abs(difficultyLabels[freeplay.Difficulty].rectTransform.anchoredPosition.x - 90) < .01f,
                    "Slow frame left the difficulty label displaced.");
            }
            Select("Monster");
            freeplay.ChangeDifficulty(1);
            Require(freeplay.SelectedSong.meta.songName == "South", "Missing difficulty did not select nearest song.");
            Advance(.3f);
            freeplay.ChangeDifficulty(-1);
            Advance(.3f);
            Select("Bopeebo");
            var song = freeplay.SelectedSong;
            PlayerPrefs.SetInt(song.FavoriteKey, 0);
            Call("RebuildList", false);
            freeplay.ToggleFavorite();
            int favoriteSelection = freeplay.SelectedIndex;
            freeplay.MoveSelection(1);
            Require(freeplay.Busy && freeplay.SelectedIndex == favoriteSelection, "Favorite transition did not lock input.");
            Require(Row.Find("Details/Favorite").GetComponent<VanillaFreeplaySprite>().FrameIndex == 0, "Favorite did not start.");
            float favoriteY = ((RectTransform)Row).anchoredPosition.y;
            Advance(.1f);
            Require(Mathf.Abs(((RectTransform)Row).anchoredPosition.y - favoriteY - 5) < .1f, "Favorite did not move upward by five pixels.");
            Capture("favorite-bob.png");
            Advance(.095f);
            Require(((RectTransform)Row).anchoredPosition.y < favoriteY, "Favorite return tween omitted its overshoot.");
            Advance(.105f);
            freeplay.ToggleFavorite();
            Require(Row.Find("Details/Favorite").gameObject.activeSelf, "Favorite vanished before reverse animation.");
            Advance(.21f);
            Require(!Row.Find("Details/Favorite").gameObject.activeSelf, "Favorite stayed visible after reverse animation.");
            freeplay.ToggleFavorite();
            Require(Row.Find("Details/Favorite").GetComponent<VanillaFreeplaySprite>().FrameIndex == 0, "Favorite did not replay.");
            Advance(.3f);
            freeplay.ChangeFilter(-1);
            freeplay.ToggleFavorite();
            Advance(.21f);
            Require(freeplay.SelectedSong == null || freeplay.SelectedSong.Favorite, "Favorites filter kept removed song.");
            freeplay.ChangeFilter(1);
            Advance(.8f);
            var spaghetti = Field<List<VanillaFreeplaySong>>("songs").Single(item => item.Album("Hard") == "spaghetti");
            foreach (string difficulty in new[] { "Easy", "Normal", "Hard" }) PlayerPrefs.DeleteKey("Freeplay.Rank." + spaghetti.ScoreKey(difficulty, 1));
            Call("RebuildList", false);
            Select(spaghetti.meta.songName);
            Require(spaghetti.week == "SP. COLLAB 1" && Row.Find("Details/New").gameObject.activeSelf, "SPAGHETTI new state or label differs.");
            Require(Field<List<Text>>("marquees")[0].text.StartsWith("SPECIAL"), "SPAGHETTI backing text did not change.");
            Advance(.8f);
            Capture("spaghetti-new.png");
            PlayerPrefs.SetInt("Freeplay.Rank." + spaghetti.ScoreKey("Normal", 1), 2);
            Call("RebuildList", false);
            Require(!Row.Find("Details/New").gameObject.activeSelf, "Cleared SPAGHETTI still shows NEW.");
            Select("Bopeebo");
            Require(Field<List<Text>>("marquees")[0].text.StartsWith("HOT BLOODED"), "Normal backing text was not restored.");
            Advance(.8f);
            string scoreKey = freeplay.SelectedSong.ScoreKey(freeplay.Difficulty, freeplay.Mode);
            int savedScore = PlayerPrefs.GetInt(scoreKey);
            PlayerPrefs.SetInt(scoreKey, 1000);
            Set("displayedScore", 0f);
            Call("Draw", .1f);
            Require(Mathf.Abs(Field<float>("displayedScore") - 900) < .01f, "Score precision duration differs.");
            Set("displayedScore", 12.9f);
            Call("Draw", 0f);
            Require(Field<List<VanillaFreeplaySprite>>("scoreDigits")[6].CurrentFrameName.StartsWith("TWO DIGITAL"), "Score rounded its fractional display value.");
            PlayerPrefs.SetInt(scoreKey, savedScore);
            Set("displayedClear", 0f);
            Set("intendedClear", .8f);
            Call("DrawCompletion", .25f);
            Require(Mathf.Abs(Field<float>("displayedClear") - .72f) < .001f, "Clear percentage did not interpolate.");
            Require(Mathf.Abs(freeplay.ConfirmationDelay - 1) < .001f, "BF confirm delay differs.");
            Set("bfGlowAge", 0f);
            Call("DrawBackingEffects", 0f);
            Require(Field<VanillaFreeplaySprite>("bfGlow").gameObject.activeSelf && Mathf.Abs(Field<VanillaFreeplaySprite>("bfGlow").color.a - .8f) < .001f,
                "BF beat glow is missing.");
            Capture("bf-beat.png");
            Field<VanillaFreeplaySprite>("bfGlow").gameObject.SetActive(false);
            Capture("bf-no-beat-control.png");
            Set("confirmAge", .4f);
            Call("Draw", 0f);
            Require(Field<VanillaFreeplaySprite>("confirmFlash").gameObject.activeSelf && Field<VanillaFreeplayAnimate>("backingYeah").gameObject.activeSelf,
                "BF confirmation layers are missing.");
            Capture("bf-confirm.png");
            Set("confirmAge", -1f);
            PlayerPrefs.SetString("Freeplay.Character", "pico");
            Require(Mathf.Abs(freeplay.ConfirmationDelay - 1.45f) < .001f, "Pico uses BF confirm delay.");
            Object.DestroyImmediate(freeplay.gameObject);
            freeplay = VanillaFreeplay.Open(menu, true, Path.Combine(Output, "EmptyBundles"));
            freeplay.enabled = false;
            Advance(2);
            Require(freeplay.IsPico && Field<VanillaFreeplaySprite>("bfGlow") == null, "Pico inherited BF backing effects.");
            freeplay.MoveSelection(2 - freeplay.SelectedIndex);
            Advance(.8f);
            Capture("pico-idle.png");
            Call("ConfirmInstrumental", freeplay.SelectedSong.Instrumentals(freeplay.Difficulty)[0]);
            freeplay.StopAllCoroutines();
            Set("confirmAge", 0f);
            Advance(1.2f);
            Capture("pico-confirm.png");
            RenderTitles();
            CheckBlackFade();
            passed = errors == 0;
        }
        catch (Exception exception) { failure = exception.ToString(); Debug.LogException(exception); }
        if (failure != null)
        {
            try { RenderTitles(); }
            catch (Exception exception) { failure += "\n" + exception; }
        }
        SessionState.SetBool("FreeplayParity.Active", false);
        File.WriteAllText(Path.Combine(Output, "parity-result.json"), new JObject
        {
            ["passed"] = passed, ["assertions"] = assertions, ["errors"] = errors, ["failure"] = failure
        }.ToString());
        EditorApplication.Exit(passed ? 0 : 1);
    }

    public static void RenderTitles()
    {
        Directory.CreateDirectory(Output);
        var root = new GameObject("Title Capture", typeof(RectTransform), typeof(Canvas));
        var canvas = root.GetComponent<Canvas>();
        var cameraObject = new GameObject("Capture", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 60;
        camera.transform.position = new Vector3(0, 0, -1000);
        camera.farClipPlane = 2000;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(18, 24, 36, 255);
        camera.cullingMask = 1 << 31;
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        var target = new RenderTexture(640, 120, 24);
        camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1000;
        var host = new GameObject("Title", typeof(RectTransform));
        var rect = host.GetComponent<RectTransform>();
        rect.SetParent(root.transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(20, -35);
        rect.sizeDelta = new Vector2(620, 50);
        var title = host.AddComponent<VanillaFreeplayCapsuleText>();
        title.font = Resources.Load<Font>("VanillaFreeplay/5by7");
        title.fontSize = 32;
        title.raycastTarget = false;
        var clipHost = new GameObject("Title Clip", typeof(RectTransform), typeof(RectMask2D));
        var clip = clipHost.GetComponent<RectTransform>();
        clip.SetParent(root.transform, false);
        clip.anchorMin = clip.anchorMax = clip.pivot = new Vector2(0, 1);
        clip.sizeDelta = new Vector2(91, 42);
        foreach (var item in root.GetComponentsInChildren<Transform>()) item.gameObject.layer = 31;
        var features = AssetDatabase.FindAssets("t:UniversalRendererData")
            .Select(id => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(id)))
            .SelectMany(data => data.rendererFeatures).Where(feature => feature != null && feature.isActive).Distinct().ToArray();
        foreach (var feature in features) feature.SetActive(false);
        int index = 0;
        try
        {
            foreach (float scale in new[] { 1f, 1.5f })
                foreach (bool fractional in new[] { false, true })
                {
                    camera.targetTexture = null;
                    Object.DestroyImmediate(target);
                    target = new RenderTexture(Mathf.RoundToInt(640 * scale), Mathf.RoundToInt(120 * scale), 24);
                    camera.targetTexture = target;
                    canvas.scaleFactor = scale;
                    rect.anchoredPosition = fractional ? new Vector2(20.37f, -35.6f) : new Vector2(20, -35);
                    foreach (string text in new[] { "Tutorial", "DadBattle", "Winter Horrorland", "SPAGHETTI (feat. j-hope)" })
                        foreach (bool pico in new[] { false, true })
                            foreach (bool selected in new[] { true, false })
                            {
                                title.text = text;
                                title.color = new Color(1, 1, 1, selected ? 1 : .6f);
                                title.Present(pico ? new Color32(204, 102, 0, 255) : new Color32(0, 204, 255, 255), selected, false, false);
                                SaveTitle("title-" + index++);
                            }
                    foreach (bool pico in new[] { false, true })
                        foreach (string phase in new[] { "first", "white", "dim" })
                        {
                            title.text = "Tutorial";
                            title.color = phase == "white" ? Color.white : new Color32(221, 221, 221, 255);
                            title.Present(pico ? new Color32(204, 102, 0, 255) : new Color32(0, 204, 255, 255), true,
                                phase != "first", phase == "white", phase != "white");
                            SaveTitle("title-" + index++);
                        }
                    clip.anchoredPosition = rect.anchoredPosition;
                    rect.SetParent(clip, false);
                    rect.anchoredPosition = Vector2.zero;
                    foreach (bool pico in new[] { false, true })
                        foreach (bool selected in new[] { true, false })
                        {
                            title.text = "Winter Horrorland";
                            title.color = new Color(1, 1, 1, selected ? 1 : .6f);
                            title.Present(pico ? new Color32(204, 102, 0, 255) : new Color32(0, 204, 255, 255), selected, false, false);
                            SaveTitle("title-" + index++);
                        }
                    rect.SetParent(root.transform, false);
                }
            camera.targetTexture = null;
            Object.DestroyImmediate(target);
            target = new RenderTexture(640, 120, 24);
            camera.targetTexture = target;
            canvas.scaleFactor = 1;
            rect.anchoredPosition = new Vector2(20, -35);
            title.text = "Tutorial";
            Texture cachedTitle = title.mainTexture;
            title.text = "Fresh";
            Require(title.mainTexture != cachedTitle, "Different titles shared a stale texture.");
            title.text = "Tutorial";
            Require(title.mainTexture == cachedTitle, "Returning to a title repeated texture generation.");
            title.color = Color.white;
            title.Present(Color.clear, false, false, false);
            SaveTitle("title-flat-control");
            title.gameObject.SetActive(false);
            SaveTitle("title-blank-control");
        }
        finally
        {
            foreach (var feature in features) feature.SetActive(true);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(target);
        }

        void SaveTitle(string name)
        {
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
    }

    private static void CheckEntranceDetails()
    {
        foreach (Transform row in Field<RectTransform>("list"))
        {
            if (!row.gameObject.activeSelf) continue;
            Require(row.Find("Details/Title Clip").gameObject.activeSelf, "Entrance hid the song title.");
            var icon = row.Find("Details/Icon");
            if (icon != null && icon.gameObject.activeSelf)
                Require(icon.GetComponent<VanillaFreeplaySprite>().enabled, "Entrance hid the song icon.");
        }
    }

    private static void CheckDifficultyVisibility()
    {
        foreach (var pair in Field<Dictionary<string, Graphic>>("difficultyLabels"))
            Require(pair.Value.gameObject.activeSelf == (pair.Key == freeplay.Difficulty), "Stale difficulty label: " + pair.Key);
    }

    private static void CheckBlackFade()
    {
        var loader = LoadingTransition.instance;
        var routine = typeof(LoadingTransition).GetMethod("LoadSceneRoutine", Instance);
        var start = typeof(LoadingTransition).GetField("shownAt", Instance);
        var visibility = (CanvasGroup)typeof(LoadingTransition).GetField("visibility", Instance).GetValue(loader);
        foreach (bool linear in new[] { true, false })
        {
            start.SetValue(loader, Time.realtimeSinceStartup - .1f);
            var sequence = (System.Collections.IEnumerator)routine.Invoke(loader, new object[] { "Game_Backup3", null, true, linear ? .2f : .35f, linear });
            Require(sequence.MoveNext(), "Black fade did not yield.");
            Require(linear ? Mathf.Abs(visibility.alpha - .5f) < .035f : visibility.alpha < .25f,
                "Black fade midpoint or unchanged default control differs.");
            (sequence as IDisposable)?.Dispose();
        }
    }
}
