using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class MenuV2 : MonoBehaviour
{
    public RectTransform mainScreen;
    public VanillaMainMenu vanillaMenu;

    public RectTransform playScreen;
    public RectTransform optionsScreen;
    public Image inputBlocker;

    [Header("Audio")] public AudioSource musicSource;
    public AudioClip menuClip;

    [Header("Background")] public Camera backgroundCamera;

    public SpriteRenderer backgroundSprite;

    public UIGradient backgroundGradient;

    [Header("Song List")] public RectTransform songListRect;

    public GameObject bundleButtonPrefab;

    public GameObject songButtonPrefab;

    public Sprite defaultCoverSprite;

    public bool canChangeSongs = true;
    public GameObject migrateBundlesButton;

    private Dictionary<BundleButtonV2, List<SongButtonV2>> bundles =
        new Dictionary<BundleButtonV2, List<SongButtonV2>>();

    [Header("Song Info")] public Image songCoverImage;
    public TMP_Text songNameText;
    public TMP_Text highScoreText;
    private int _lastScore = 0;
    [FormerlySerializedAs("songCharterText")] public TMP_Text songCreditsText;
    public TMP_Text songDescriptionText;
    public TMP_Dropdown songDifficultiesDropdown;
    public TMP_Dropdown songModeDropdown;
    public GameObject selectSongScreen;
    public GameObject songInfoScreen;
    public GameObject loadingSongScreen;

    [Header("Notifications")] public GameObject notificationObject;
    public RectTransform notificationLists;
    
    private SongMetaV2 _currentMeta;
    private string _songsFolder;
    private string previewPath;
    private string loadedPreviewPath;
    private int previewRequest;
    
    public static MenuV2 Instance;
    public static int lastSelectedBundle;
    public static int lastSelectedSong;

    public static StartPhase startPhase;
    
    // Start is called before the first frame update
    void Start()
    {
        InitializeMenu();
    }

    public enum StartPhase
    {
        Nothing,
        SongList,
        Offset
    }

    public void ReloadSongList()
    {
        selectSongScreen.SetActive(true);
        songInfoScreen.SetActive(false);
        bundles.Clear();
        
        if (songListRect.childCount != 0)
        {
            foreach (RectTransform child in songListRect)
            {
                Destroy(child.gameObject);
            }
        }
        
        if (!Directory.Exists(_songsFolder))
        {
            Directory.CreateDirectory(_songsFolder);
        }

        

        SearchOption option = SearchOption.TopDirectoryOnly;

        List<string> allDirectories = new List<string>();
        string builtInBundles = Path.Combine(Application.streamingAssetsPath, "Bundles");
        if (Directory.Exists(builtInBundles))
            allDirectories.AddRange(Directory.GetDirectories(builtInBundles, "*", option).OrderBy(path => path, StringComparer.Ordinal));
        allDirectories.AddRange(Directory.GetDirectories(_songsFolder, "*", option).OrderBy(path => path, StringComparer.Ordinal));
        
        
        foreach (string dir in allDirectories)
        {
            if (File.Exists(dir + "/bundle-meta.json"))
            {
                BundleMeta bundleMeta =
                    JsonConvert.DeserializeObject<BundleMeta>(File.ReadAllText(dir + "/bundle-meta.json"));

                if (bundleMeta == null)
                {
                    Debug.LogError("Error whilst trying to read JSON file! " + dir + "/bundle-meta.json");
                    break;
                }

                BundleButtonV2 newWeek = Instantiate(bundleButtonPrefab, songListRect).GetComponent<BundleButtonV2>();

                newWeek.Creator = bundleMeta.authorName;
                newWeek.Name = bundleMeta.bundleName;
                newWeek.directory = dir;
                newWeek.SongButtons = new List<SongButtonV2>();
                print("Searching in " + dir);

                List<SongButtonV2> songButtons = new List<SongButtonV2>();

                foreach (string songDir in Directory.GetDirectories(dir, "*", option).OrderBy(path => path, StringComparer.Ordinal))
                {
                    print("We got " + songDir);
                    if (File.Exists(songDir + "/meta.json") & File.Exists(songDir + "/Inst.ogg"))
                    {
                        SongMetaV2 meta = JsonConvert.DeserializeObject<SongMetaV2>(File.ReadAllText(songDir + "/meta.json"));

                        if (meta == null)
                        {
                            Debug.LogError("Error whilst trying to read JSON file! " + songDir + "/meta.json");
                            break;
                        }

                        meta.bundleMeta = bundleMeta;
                        
                        SongButtonV2 newSong = Instantiate(songButtonPrefab,songListRect).GetComponent<SongButtonV2>();
                        
                        newSong.Meta = meta;
                        newSong.Meta.songPath = songDir;
                
                        string coverDir = songDir + "/Cover.png";
                
                        if (File.Exists(coverDir))
                        {
                            byte[] coverData = File.ReadAllBytes(coverDir);

                            Texture2D coverTexture2D = new Texture2D(512,512);
                            coverTexture2D.LoadImage(coverData);

                            newSong.CoverArtSprite = Sprite.Create(coverTexture2D,
                                new Rect(0, 0, coverTexture2D.width, coverTexture2D.height), new Vector2(0, 0), 100);
                            newSong.Meta.songCover = newSong.CoverArtSprite;

                        }
                        else
                        {
                            newSong.CoverArtSprite = defaultCoverSprite;
                            newSong.Meta.songCover = defaultCoverSprite;
                        }

                        newWeek.SongButtons.Add(newSong);

                        newSong.gameObject.SetActive(false);
                        
                        

                        newSong.GetComponent<Button>().onClick.AddListener(() =>
                        {
                            ChangeSong(newSong.Meta);

                            lastSelectedBundle = GetBundleIndex(newWeek);
                            lastSelectedSong = bundles[newWeek].IndexOf(newSong);
                        });

                        songButtons.Add(newSong);
                    }
                    else
                    {
                        Debug.LogError("Failed to find required files in " + songDir);
                    }
                }

                newWeek.UpdateCount();
                bundles.Add(newWeek, songButtons);
            }
            
            
        }

        if (startPhase == StartPhase.SongList)
        {
            startPhase = StartPhase.Nothing;

            LoadingTransition.instance.Hide();

            if (bundles.Count == 0)
                return;
            lastSelectedBundle = Mathf.Clamp(lastSelectedBundle, 0, bundles.Count - 1);
            BundleButtonV2 bundleButton = bundles.Keys.ElementAt(lastSelectedBundle);
            bundleButton.ToggleSongsVisibility();

            musicSource.volume = OptionsV2.menuVolume;
            
            if (bundles[bundleButton].Count > 0)
            {
                lastSelectedSong = Mathf.Clamp(lastSelectedSong, 0, bundles[bundleButton].Count - 1);
                ChangeSong(bundles[bundleButton][lastSelectedSong].Meta);
            }
            
        }
    }

    public void UpdateScoreText()
    {
        
        
        string highScoreSave = _currentMeta.songName + _currentMeta.bundleMeta.bundleName +
                               songDifficultiesDropdown.options[songDifficultiesDropdown.value].text.ToLower() +
                               PlayModes.FromIndex(songModeDropdown.value);
        int highScore = PlayerPrefs.GetInt(highScoreSave, 0);
        print("High Score for " + highScoreSave + " is " + highScore);
        if (PlayModes.FromIndex(songModeDropdown.value) != PlayModes.Autoplay)
        {
            LeanTween.value(_lastScore, highScore, .35f).setOnUpdate(value =>
            {
                highScoreText.text = $"High Score: <color=white>{(int)value}</color>";
            }).setOnComplete(() =>
            {
                _lastScore = highScore;
            });
        }
        else
        {
            highScoreText.text = "High Score not available for AutoPlay.";
        }

        
    }

    public int GetBundleIndex(BundleButtonV2 item)
    {

        for (int i = 0; i < bundles.Keys.Count; i++)
        {
            if (bundles.Keys.ElementAt(i) == item)
            {
                return i;
            }
        }

        return 0;
    }
    
    public void ChangeSong(SongMetaV2 meta)
    {
        print("Checking if we can change songs. It is " + canChangeSongs);
        if (!canChangeSongs) return;
        canChangeSongs = false;
        _currentMeta = null;
        print("Updating info");
        songNameText.text = meta.songName;
        songDescriptionText.text = "<color=yellow>Description:</color> " + meta.songDescription;
        songCoverImage.sprite = meta.songCover;

        songCreditsText.text = string.Empty;
        
        foreach (string role in meta.credits.Keys.ToList())
        {
            string memberName = meta.credits[role];

            songCreditsText.text += $"<color=yellow>{role}:</color> {memberName}\n";
        }
        
        songDifficultiesDropdown.ClearOptions();

        songDifficultiesDropdown.AddOptions(meta.difficulties.Keys.ToList());
        songDifficultiesDropdown.SetValueWithoutNotify(0);
        
        loadingSongScreen.SetActive(true);

        selectSongScreen.SetActive(false);
        songInfoScreen.SetActive(false);

        LeanTween.value(musicSource.gameObject, musicSource.volume, 0, 1f).setOnComplete(() =>
        {
            RefreshSongVariation();
        }).setOnUpdate(value =>
        {
            musicSource.volume = value;
        });
        

        _currentMeta = meta;
    }

    public void RefreshSongVariation()
    {
        if (_currentMeta == null || songDifficultiesDropdown.options.Count == 0) return;
        string difficulty = songDifficultiesDropdown.options[songDifficultiesDropdown.value].text;
        SongVariation variation = _currentMeta.GetVariation(difficulty);
        songNameText.text = variation?.songName ?? _currentMeta.songName;
        songCreditsText.text = string.Empty;
        foreach (var credit in variation?.credits ?? _currentMeta.credits)
            songCreditsText.text += $"<color=yellow>{credit.Key}:</color> {credit.Value}\n";
        string path = _currentMeta.AssetPath("Inst.ogg", difficulty);
        if (path == previewPath)
        {
            if (loadedPreviewPath == path)
            {
                canChangeSongs = true;
                loadingSongScreen.SetActive(false);
                songInfoScreen.SetActive(true);
                musicSource.volume = OptionsV2.instVolume;
                UpdateScoreText();
            }
            return;
        }
        previewPath = path;
        StartCoroutine(LoadSongAudio(path));
    }

    public void PlaySong()
    {
        if (!canChangeSongs) return;
        var difficultiesList = _currentMeta.difficulties.Keys.ToList();
        Song.difficulty = difficultiesList[songDifficultiesDropdown.value];
        Song.modeOfPlay = PlayModes.FromIndex(songModeDropdown.value);
        Song.currentSongMeta = _currentMeta;

        LoadingTransition.instance.Show(() => SceneManager.LoadScene("Game_Backup3"));
    }

    IEnumerator LoadSongAudio(string path)
    {
        int request = ++previewRequest;
        canChangeSongs = false;
        WWW www = new WWW(path);
        yield return www;
        if (request != previewRequest) { www.Dispose(); yield break; }
        if (www.error != null)
        {
            Debug.LogError(www.error);
            previewPath = null;
            canChangeSongs = true;
        }
        else
        {
            canChangeSongs = false;
            AudioClip clip = www.GetAudioClip();
            while (clip.loadState != AudioDataLoadState.Loaded && clip.loadState != AudioDataLoadState.Failed)
                yield return new WaitForSeconds(0.1f);
            if (request != previewRequest) { Destroy(clip); www.Dispose(); yield break; }
            if (clip.loadState == AudioDataLoadState.Failed)
            {
                Debug.LogError("Failed to decode song preview: " + path);
                Destroy(clip);
                previewPath = null;
                canChangeSongs = true;
                www.Dispose();
                yield break;
            }
            AudioClip previous = musicSource.clip;
            musicSource.clip = clip;
            loadedPreviewPath = path;
            if (previous != null && previous != menuClip) Destroy(previous);
            LeanTween.cancel(musicSource.gameObject);
            musicSource.Play();
            LeanTween.value(musicSource.gameObject, musicSource.volume, OptionsV2.instVolume, 1f).setOnUpdate(value =>
            {
                musicSource.volume = value;
            });
            canChangeSongs = true;
            loadingSongScreen.SetActive(false);
            songInfoScreen.SetActive(true);
            UpdateScoreText();
        }
        www.Dispose();
    }

    
    public void InitializeMenu()
    {
        Instance = this;
        bool restoreStory = VanillaStoryCampaign.ReturnToStory;
        bool restoreFreeplay = VanillaFreeplay.ReturnToFreeplay;
        bool showTitle = vanillaMenu != null && !VanillaTitleScreen.EnteredMainMenu
            && !restoreStory && !restoreFreeplay && startPhase == StartPhase.Nothing;
        if (restoreStory || restoreFreeplay) startPhase = StartPhase.Nothing;
        songDifficultiesDropdown.onValueChanged.AddListener(_ => RefreshSongVariation());

        LeanTween.reset();

        LeanTween.init(99999);

        FindObjectOfType<BackgroundScroller>().MoveBackground();

        _songsFolder = Application.persistentDataPath + "/Bundles";
        
        var songsPath = Application.persistentDataPath + "/Songs";
        if (Directory.Exists(songsPath))
        {
            if (Directory.GetDirectories(songsPath, "*", SearchOption.TopDirectoryOnly).Length != 0)
            {
                migrateBundlesButton.SetActive(true);
            }
        }
        
        switch (startPhase)
        {
            case StartPhase.Nothing:
                if (vanillaMenu != null)
                {
                    if (backgroundSprite != null) backgroundSprite.color = Color.white;
                    inputBlocker.enabled = false;
                    mainScreen.gameObject.SetActive(true);
                    LoadingTransition.instance.Hide();
                    break;
                }
                backgroundSprite.color = Color.clear;
                inputBlocker.enabled = true;

                mainScreen.gameObject.SetActive(true);
                mainScreen.LeanMoveY(-720, 0);

                mainScreen.LeanMoveY(0, 1.5f).setDelay(.5f).setEaseOutExpo().setOnComplete(() =>
                {
                    inputBlocker.enabled = false;
                });

                LeanTween.value(gameObject, Color.clear, Color.white, 1.5f).setDelay(.5f).setEaseOutExpo()
                    .setOnUpdate(color => { backgroundSprite.color = color; });
                LoadingTransition.instance.Hide();
                break;
            case StartPhase.SongList:
                canChangeSongs = true;

                mainScreen.gameObject.SetActive(false);
                playScreen.gameObject.SetActive(true);

                playScreen.LeanMoveY(0, 0f);

                ReloadSongList();
                break;
            
            case StartPhase.Offset:
                mainScreen.gameObject.SetActive(false);
                optionsScreen.gameObject.SetActive(true);

                optionsScreen.LeanMoveY(0, 0f).setOnComplete(() =>
                {
                    OptionsV2.instance.LoadNotePrefs();
                    LoadingTransition.instance.Hide();
                });
                break;
        }
        musicSource.clip = menuClip;
        musicSource.volume = OptionsV2.menuVolume;
        if (vanillaMenu != null && mainScreen.gameObject.activeSelf)
            musicSource.volume = 0;
        musicSource.Play();

        DiscordController.instance.SetMenuState("Idle");
        if (showTitle) VanillaTitleScreen.Open(this);
        else if (restoreStory) OpenStoryMode();
        else if (restoreFreeplay) OpenFreeplay(true);
    }

    public void OpenStoryMode()
    {
        VanillaStoryMenu.Open(this);
        DiscordController.instance.SetMenuState("Selecting a Level");
    }

    public void OpenFreeplay(bool skipIntro = false)
    {
        VanillaFreeplay.Open(this, skipIntro);
        DiscordController.instance.SetMenuState("Selecting a Song");
    }

    public void OptionsScreenTransition(bool toOptions)
    {
        if (toOptions)
        {
            TransitionScreen(mainScreen,optionsScreen, () =>
            {
                OptionsV2.instance.LoadVolumeProperties();
                DiscordController.instance.SetMenuState("Editing Options");
            });
        }
        else
        {
            TransitionScreen(optionsScreen, mainScreen, () => DiscordController.instance.SetMenuState("Idle"));
        }
    }
    
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OpenPlayScreenFromMenu()
    {
        TransitionScreen(mainScreen, playScreen, () => DiscordController.instance.SetMenuState("Selecting a Song"));
        canChangeSongs = true;
    }

    public void OpenMenuFromPlayScreen()
    {
        if (!canChangeSongs) return;
        TransitionScreen(playScreen, mainScreen, () => DiscordController.instance.SetMenuState("Idle"));
        if (musicSource.clip != menuClip)
        {
            musicSource.Stop();
            AudioClip previous = musicSource.clip;
            musicSource.clip = menuClip;
            if (previous != null) Destroy(previous);
            previewPath = loadedPreviewPath = null;
            musicSource.volume = OptionsV2.menuVolume;
            musicSource.Play();
        }
    }

    public void TransitionScreen(RectTransform oldScreen, RectTransform newScreen, Action onComplete = null)
    {
        if (vanillaMenu != null && (oldScreen == mainScreen || newScreen == mainScreen))
        {
            oldScreen.gameObject.SetActive(false);
            newScreen.anchoredPosition = Vector2.zero;
            newScreen.gameObject.SetActive(true);
            inputBlocker.enabled = false;
            onComplete?.Invoke();
            return;
        }
        inputBlocker.enabled = true;
        oldScreen.LeanMoveY(-720,1f).setEaseOutExpo().setOnComplete(() =>
        {
            oldScreen.gameObject.SetActive(false);
            newScreen.gameObject.SetActive(true);
            newScreen.LeanMoveY(-720, 0);
            newScreen.LeanMoveY(0, 1f).setEaseOutExpo().setOnComplete(() =>
            {
                inputBlocker.enabled = false;
                onComplete?.Invoke();
            });

        });
    }

    public void DisplayNotification(Color color, string text)
    {
        GameObject notification = Instantiate(notificationObject, notificationLists);
        NotificationObject notificationScript = notification.GetComponent<NotificationObject>();

        notificationScript.notificationText.text = text;
        notificationScript.BackgroundColor = color;

    }
    
    // Update is called once per frame
    void Update()
    {
        
    }
}
