 using System;
 using System.Collections;
 using System.Collections.Generic;
 using System.Diagnostics;
 using System.IO;
 using System.Linq;
 using FridayNightFunkin;
 using Newtonsoft.Json;
 using QFSW.MOP2;
 using SimpleSpriteAnimator;
 using Slowsharp;
 using TMPro;
 using UnityEngine;
 using UnityEngine.SceneManagement;
 using UnityEngine.Serialization;
 using UnityEngine.UI;
 using Debug = UnityEngine.Debug;
 using Random = UnityEngine.Random;

 // ReSharper disable IdentifierTypo
// ReSharper disable PossibleNullReferenceException

public partial class Song : MonoBehaviour
{

    #region Variables

    public AudioSource soundSource;
    public AudioClip startSound;
    [Space] public AudioSource[] musicSources;
    public AudioSource vocalSource;
    public AudioSource oopsSource;
    public AudioClip musicClip;
    public AudioClip vocalClip;
    public AudioClip menuClip;
    public AudioClip[] noteMissClip;
    public bool hasVoiceLoaded;    
    public HybInstance modInstance;


    [Space] public bool songSetupDone;
        
    [Space] public GameObject[] defaultSceneObjects;

    [Space] public GameObject ratingObject;
    public GameObject liteRatingObjectP1;
    public GameObject liteRatingObjectP2;
    public Sprite sickSprite;
    public Sprite goodSprite;
    public Sprite badSprite;
    public Sprite shitSprite;
    [Header("Player 1 Stats")]
    public GameObject playerOneScoringObject;
    public TMP_Text playerOneScoringText;
    public Image playerOneCornerImage;
    public TMP_Text playerOneComboText;
    public float playerOneDisplayComboTimer;
    public float playerOneComboColorLerpSpeed;
    [Header("Player 2 Stats")]
    public GameObject playerTwoScoringObject;
    public TMP_Text playerTwoScoringText;
    public Image playerTwoCornerImage;
    public TMP_Text playerTwoComboText;
    public float playerTwoDisplayComboTimer;
    public float playerTwoComboColorLerpSpeed;
    [Space]
    public float ratingLayerTimer;
    private float _ratingLayerDefaultTime = 2.2f;
    private int _currentRatingLayer;
    public PlayerStat playerOneStats;
    public PlayerStat playerTwoStats;

    public Stopwatch stopwatch;
    public Stopwatch beatStopwatch;
    [Space] public Camera mainCamera;
    public Camera uiCamera;
    public float cameraBopLerpSpeed;
    public float portraitBopLerpSpeed;
    private float _defaultZoom;
    public float defaultGameZoom;
    

    [Space, TextArea(2, 12)] public string jsonDir;
    public float notesOffset;
    public float noteDelay;
    [Range(-1f, 1f)] public float speedDifference;

    [Space] public Canvas battleCanvas;
    public Canvas menuCanvas;
    public GameObject generatingSongMsg;
    public GameObject songListScreen;
    
    [Space] public GameObject menuScreen;

    [Header("Death Mechanic")] public Camera deadCamera;
    public GameObject deadBoyfriend;
    public Animator deadBoyfriendAnimator;
    public AudioClip deadNoise;
    public AudioClip deadTheme;
    public AudioClip deadConfirm;
    public Image deathBlackout;
    public bool isDead;
    public bool respawning;

    

   
    
    private float _currentInterval;
    
    [Space] public Transform player1Notes;
    public SpriteRenderer[] player1NoteSprites;
    public List<List<NoteObject>> player1NotesObjects;
    public Animator[] player1NotesAnimators;
    public Transform player1Left;
    public Transform player1Down;
    public Transform player1Up;
    public Transform player1Right;
    [Space] public Transform player2Notes;
    public SpriteRenderer[] player2NoteSprites;
    public List<List<NoteObject>> player2NotesObjects;
    public Animator[] player2NotesAnimators;
    public Transform player2Left;
    public Transform player2Down;
    public Transform player2Up;
    public Transform player2Right;
    private List<NoteBehaviour> _noteBehaviours = new List<NoteBehaviour>();
    private readonly NoteSchedule _noteSchedule = new NoteSchedule();
    
    [Header("Prefabs")] public GameObject leftArrow;
    public GameObject downArrow;
    public GameObject upArrow;
    public GameObject rightArrow;
    [Space] public GameObject holdNote;
    public Sprite holdNoteEnd;
    public Sprite holdNoteSprite;
    [Header("Object Pools")] 
    public ObjectPool leftNotesPool;
    public ObjectPool rightNotesPool;
    public ObjectPool downNotesPool;
    public ObjectPool upNotesPool;
    public ObjectPool holdNotesPool;
    
    [Header("Characters")]
    public string[] characterNames;
    public Character[] characters;
    public Dictionary<string, Character> charactersDictionary;
    public Character defaultEnemy;
    [Space] public GameObject girlfriendObject;
    public SpriteAnimator girlfriendAnimator;
    public bool altDance;

    [FormerlySerializedAs("enemyObj")] [Header("Enemy")] public GameObject opponentObject;
    public Character enemy;
    public string enemyName;
    [FormerlySerializedAs("enemyAnimator")] public SpriteAnimator opponentAnimator;
    public float enemyIdleTimer = .3f;
    private float _currentEnemyIdleTimer;
    public float enemyNoteTimer = .25f;
    private Vector3 _enemyDefaultPos;
    private readonly float[] _currentEnemyNoteTimers = {0, 0, 0, 0};
    private readonly float[] _currentDemoNoteTimers = {0, 0, 0, 0};
    private LTDescr _enemyFloat;



    [FormerlySerializedAs("bfObj")] [Header("Boyfriend")] public GameObject boyfriendObject;
    public SpriteAnimator boyfriendAnimator;
    public float boyfriendIdleTimer = .3f;
    public Sprite boyfriendPortraitNormal;
    public Sprite boyfriendPortraitDead;
    private float _currentBoyfriendIdleTimer;

    private FNFSong _song;

    public static Song instance;

    [Header("Scenes")] public Dictionary<string, SceneData> scenes;

    [Header("Health")] public float health = 100;

    private const float MAXHealth = 200;
    public float healthLerpSpeed;
    public GameObject healthBar;
    public RectTransform boyfriendHealthIconRect;
    public Image boyfriendHealthIcon;
    public Image boyfriendHealthBar;
    public RectTransform enemyHealthIconRect;
    public Image enemyHealthIcon;
    public Image enemyHealthBar;

    [Space] public GameObject songDurationObject;
    public TMP_Text songDurationText;
    public Image songDurationBar;

    [Space] public GameObject startSongTooltip;
    
    [Space] public NoteObject lastNote;
    public float stepCrochet;
    public float beatsPerSecond;
    public int currentBeat;
    public bool beat;

    private float _bfRandomDanceTimer;
    private float _enemyRandomDanceTimer;

    private bool _portraitsZooming;
    private bool _cameraZooming;

    public string songsFolder;
    public string selectedSongDir;
    public string selectedInstrumentalPath;
    public string selectedVocalsPath;
    public string selectedVanillaPath;

    public static SongMetaV2 currentSongMeta;
    public static string difficulty;
    public static int modeOfPlay = PlayModes.Boyfriend;
    public VanillaSongPlayback vanillaPlayback;

    [HideInInspector] public SongListObject selectedSong;


    public bool songStarted;

    [Header("Subtitles")]

    public SubtitleDisplayer subtitleDisplayer;
    public bool usingSubtitles;

    #endregion

    private void Start()
    {
        /*
         * To allow other scripts to access the Song script without needing the
         * script to be found or referenced, we set a static variable within the
         * Song script itself that can be used at anytime to access the singular used Song
         * script instance.
         */
        instance = this;

        /*
         * Sets the "songs folder" to %localappdata%/Rei/FridayNight/Songs.
         * This is used to find and load any found songs.
         *
         * This can only be changed within the editor itself and not in-game,
         * though it would not be hard to make that possible.
         */
        songsFolder = Application.persistentDataPath + "/Songs";

        /*
         * Makes sure the UI for the song gameplay is disabled upon load.
         *
         * This disables the notes for both players and the UI for the gameplay.
         */
        player1Notes.gameObject.SetActive(false);
        player2Notes.gameObject.SetActive(false);
        playerOneScoringText.enabled = false;
        playerTwoScoringText.enabled = false;
        playerOneComboText.alpha = 0;
        playerTwoComboText.alpha = 0;
        battleCanvas.enabled = true;
        healthBar.SetActive(false);
        songDurationObject.SetActive(false);

        mainCamera = Camera.main;
        
        
        /*
        * Initialize LeanTween
        */
        
       LeanTween.init(9999);
        
        /*
         * Grabs the subtitle displayer.
         */
        subtitleDisplayer = GetComponent<SubtitleDisplayer>();

        if (OptionsV2.DesperateMode)
        {
            boyfriendObject.SetActive(false);
            opponentObject.SetActive(false);

            boyfriendHealthIcon.enabled = false;
            enemyHealthIcon.enabled = false;
        }

        if (OptionsV2.LiteMode)
        {
            girlfriendObject.SetActive(false);
        }

        
        /*
         * In case we want to reset the enemy position later on,
         * we will save their current position.
         */
        _enemyDefaultPos = opponentObject.transform.position;

        /*
         * We'll make a dictionary of characters via the two arrays of character names
         * and character classes.
         *
         * This is later on used to load a character based on their name for "Player2"
         * in an FNF chart.
         */
        charactersDictionary = new Dictionary<string, Character>();
        for (int i = 0; i < characters.Length; i++)
        {
            charactersDictionary.Add(characterNames[i], characters[i]);
        }


        _defaultZoom = uiCamera.orthographicSize;

        modeOfPlay = PlayModes.Normalize(modeOfPlay);
        bool doAuto = modeOfPlay == PlayModes.Autoplay;
        Player.playAsEnemy = modeOfPlay == PlayModes.Opponent;
        
        PlaySong(doAuto, difficulty,currentSongMeta.songPath);
    }

    #region Song Gameplay

    public void PlaySong(bool auto, string difficulty = "", string directory = "")
    {
        /*
         * If the player wants the song to play itself,
         * we'll set the Player script to be on demo mode.
         */
        Player.demoMode = auto;
        
        /*
         * We'll reset any stats then update the UI based on it.
         */
        _currentRatingLayer = 0;
        playerOneStats = new PlayerStat();
        playerTwoStats = new PlayerStat();
        currentBeat = 0;

        UpdateScoringInfo();

        /*
         * Grabs the current song's directory and saves it to a variable.
         *
         * We'll then use it to grab the chart file.
         */
        selectedSongDir = string.IsNullOrWhiteSpace(directory) ? selectedSong.directory : directory;
        SongMetaV2 assetMeta = currentSongMeta != null && currentSongMeta.songPath == selectedSongDir
            ? currentSongMeta : new SongMetaV2 { songPath = selectedSongDir };
        selectedInstrumentalPath = assetMeta.freeplayInstrumentalPath ?? assetMeta.AssetPath("Inst.ogg", difficulty);
        selectedVocalsPath = assetMeta.AssetPath("Voices.ogg", difficulty);
        selectedVanillaPath = assetMeta.AssetPath("Vanilla.json", difficulty);
        
        jsonDir = selectedSongDir + $"/Chart-{difficulty.ToLower()}.json";

        /*
         * We'll enable the gameplay UI.
         *
         * We'll also hide the Menu UI but also reset it
         * so we can instantly go back to the menu
         */
        battleCanvas.enabled = true;
        bool skipLoadingScreen = Pause.ConsumeLoadingScreenSkip(selectedSongDir);
        generatingSongMsg.SetActive(!skipLoadingScreen && !LoadingTransition.instance.HoldingStoryFrame);

        menuCanvas.enabled = false;
        songListScreen.SetActive(false);
        
        /*
         * We'll check and load subtitltes.
         */
        if(File.Exists(selectedSongDir+"/Subtitles.txt"))
        {
            TextAsset textAsset = new TextAsset(File.ReadAllText(selectedSongDir+"/Subtitles.txt"));
            subtitleDisplayer.Subtitle = textAsset;
            usingSubtitles = true;
        }

        if (File.Exists(selectedSongDir + "/ModScript.csx"))
        {
            modInstance = CScript.CreateRunner(File.ReadAllText(selectedSongDir + "/ModScript.csx")).Instantiate("ModScript");
            modInstance?.Invoke("OnSongStarting");

            if (File.Exists(selectedSongDir + "/Events.json"))
            {
                string eventsData = File.ReadAllText(selectedSong + "/Events.json");
                SongEvents events = JsonConvert.DeserializeObject<SongEvents>(eventsData);
                SongEventsHandler.instance.songEvents = events.events;
            }

        }

        /*
         * Now we start the song setup.
         *
         * This is a Coroutine so we can make use
         * of the functions to pause it for certain time.
         */
        StartCoroutine(nameof(SetupSong));

    }

    private string songLoadError;

    IEnumerator SetupSong()
    {
        songLoadError = null;
        hasVoiceLoaded = false;
        SongLoadingDiagnostics.Begin(selectedSongDir, difficulty);
        yield return VanillaFreeplayAnimate.ReleaseCachedAssets();
        SongLoadingDiagnostics.Record("menu assets released");
        yield return LoadSplitVocals();
        if (songLoadError != null)
        {
            FailSongLoad(songLoadError);
            yield break;
        }
        var instrumental = new SongAudioLoader.Result();
        yield return SongAudioLoader.Load(selectedInstrumentalPath, instrumental);
        if (instrumental.Error != null)
        {
            FailSongLoad(selectedInstrumentalPath + ": " + instrumental.Error);
            yield break;
        }
        musicClip = instrumental.Clip;
        string voicesPath = SplitPlayerVocalsPath ?? selectedVocalsPath;
        if (File.Exists(voicesPath))
        {
            var voices = new SongAudioLoader.Result();
            yield return SongAudioLoader.Load(voicesPath, voices);
            if (voices.Error != null)
            {
                FailSongLoad(voicesPath + ": " + voices.Error);
                yield break;
            }
            vocalClip = voices.Clip;
            hasVoiceLoaded = true;
        }
        try
        {
            SongLoadingDiagnostics.Record("generate song");
            GenerateSong();
            SongLoadingDiagnostics.Record("song generated");
        }
        catch (Exception exception)
        {
            FailSongLoad(exception.ToString());
        }
    }

    private void FailSongLoad(string error)
    {
        SongLoadingDiagnostics.Record("failed: " + error);
        songSetupDone = false;
        songStarted = false;
        hasVoiceLoaded = false;
        foreach (var source in musicSources) if (source != null) source.Stop();
        if (musicClip != null) Destroy(musicClip);
        if (vocalClip != null) Destroy(vocalClip);
        musicClip = vocalClip = null;
        Debug.LogError("Song loading failed: " + selectedSongDir + ": " + error);
        LoadingTransition.instance.ShowFailure("Could not load this song.", () =>
        {
            VanillaStoryCampaign.ReturnToMenu();
            VanillaFreeplay.ReturnToFreeplay = true;
            if (DiscordController.instance != null) DiscordController.instance.EnableGameStateLoop = false;
            SceneManager.LoadScene("Title");
            LoadingTransition.instance.Hide();
        });
    }

    public void GenerateSong()
    {

        var customization = JsonConvert.DeserializeObject<NoteCustomization>(PlayerPrefs.GetString("Note Customization", "{}"));
        Color[] noteColors = customization?.savedColors;
        if (noteColors == null || noteColors.Length != 4)
            noteColors = NoteCustomization.defaultFnfColors;
        for (int i = 0; i < 4; i++)
        {
            player1NoteSprites[i].color = noteColors[i];
            player2NoteSprites[i].color = noteColors[i];
        }

        /*
         * Set the health the half of the max so it's smack dead in the
         * middle.
         */

        health = MAXHealth / 2;

        /*
         * Special thanks to KadeDev for creating the .NET FNF Song parsing library.
         *
         * With it, we can load the song as a whole class full of chart information
         * via the chart file.
         */
        _song = new FNFSong(jsonDir);
        ChartScrollSpeed = ReadChartScrollSpeed(jsonDir);
        _noteBehaviours.Clear();
        ResetFunkinScore();
        Player.instance.ResetSong();
        vanillaPlayback = VanillaSongPlayback.Attach(this, ChartScrollSpeed);

        /*
         * We grab the BPM to calculate the BPS and the Step Crochet.
         */
        beatsPerSecond = 60 / (float) _song.Bpm;

        stepCrochet = (60 / (float) _song.Bpm * 1000 / 4);

        /*
         * Just in case, we'll force player 1 and player 2 notes to be wiped to a
         * clean slate.
         */

        if (player1NotesObjects != null)
        {
            foreach (List<NoteObject> list in player1NotesObjects)
            {

                list.Clear();
            }
            


            player1NotesObjects.Clear();
        }

        if (player2NotesObjects != null)
        {
            foreach (List<NoteObject> list in player2NotesObjects)
            {

                list.Clear();
            }

            player2NotesObjects.Clear();
        }

        leftNotesPool.ReleaseAll();
        downNotesPool.ReleaseAll();
        upNotesPool.ReleaseAll();
        rightNotesPool.ReleaseAll();
        holdNotesPool.ReleaseAll();
        //GENERATE PLAYER ONE NOTES
        player1NotesObjects = new List<List<NoteObject>>
        {
            new List<NoteObject>(), new List<NoteObject>(), new List<NoteObject>(), new List<NoteObject>()
        };

        //GENERATE PLAYER TWO NOTES
        player2NotesObjects = new List<List<NoteObject>>
        {
            new List<NoteObject>(), new List<NoteObject>(), new List<NoteObject>(), new List<NoteObject>()
        };

        /*
         * If somehow we fucked up, we stop the song generation process entirely.
         *
         * If we didn't, we keep going.
         */

        if (_song == null)
        {
            Debug.LogError("Error with song data");
            return;
        }
        
        /*
         * Shift the UI for downscroll or not
         */
        if (OptionsV2.Downscroll)
        {
            healthBar.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 140f);

            uiCamera.transform.position = new Vector3(0, 7,-10);

            playerOneScoringObject.GetComponent<RectTransform>().anchoredPosition = new Vector3(-160, 315, 0);
            playerTwoScoringObject.GetComponent<RectTransform>().anchoredPosition = new Vector3(160, 315, 0);
        }
        else
        {
            healthBar.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -140f);

            uiCamera.transform.position = new Vector3(0, 2,-10);

            playerOneScoringObject.GetComponent<RectTransform>().anchoredPosition = new Vector3(-160, 45, 0);
            playerTwoScoringObject.GetComponent<RectTransform>().anchoredPosition = new Vector3(160, 45, 0);
        }

        /*
         * Shift the UI or not for Middlescroll
         */
        
        if(OptionsV2.Middlescroll)
        {
            if (Player.playAsEnemy)
            {
                foreach (SpriteRenderer sprite in player1NoteSprites)
                {
                    sprite.enabled = false;
                }
                foreach (SpriteRenderer sprite in player2NoteSprites)
                {
                    sprite.enabled = true;
                }

                player2Notes.transform.position = new Vector3(0f, 4.45f, 15);
            }
            else
            {
                foreach (SpriteRenderer sprite in player1NoteSprites)
                {
                    sprite.enabled = true;
                }
                foreach (SpriteRenderer sprite in player2NoteSprites)
                {
                    sprite.enabled = false;
                }

                player1Notes.transform.position = new Vector3(0f, 4.45f, 15);
            }
        }
        else
        {
            foreach (SpriteRenderer sprite in player2NoteSprites)
            {
                sprite.enabled = true;
            }
            
            foreach (SpriteRenderer sprite in player1NoteSprites)
            {
                sprite.enabled = true;
            }
            
            player2Notes.transform.position = new Vector3(-3.6f, 4.45f, 15);
            player1Notes.transform.position = new Vector3(3.6f, 4.45f, 15);

        }
        
        /*
         * Disable or enable each player's scoring objects depending on
         * the mode of play.
         */
        playerOneScoringObject.SetActive(false);
        playerTwoScoringObject.SetActive(false);
        
        if (Player.playAsEnemy || Player.demoMode)
        {
            playerTwoScoringObject.SetActive(true);
        }
        
        if(!Player.playAsEnemy || Player.demoMode)
        {
            playerOneScoringObject.SetActive(true);
        }

        /*
         * "foreach" allows us to go through each and every single section in the
         * chart. Then a nested "foreach" allows to go through every notes in that
         * specific section.
         */
        foreach ( FNFSong.FNFSection section in _song.Sections ) {
            foreach ( var noteData in section.Notes ) {
                _noteBehaviours.Add( new NoteBehaviour( section, noteData ) );
            }
        }
        _noteSchedule.Reset(_noteBehaviours);
        /*
         * Charts tend to not have organized notes, so we have to sort notes
         * for the game so inputs do not get screwed up.
         *
         * The notes for each player are sorted in ascending order based on strum time.
         */

        for (int i = 0; i < 4; i++)
        {
            player1NotesObjects[i] = player1NotesObjects[i].OrderBy(s => s.strumTime).ToList();
            player2NotesObjects[i] = player2NotesObjects[i].OrderBy(s => s.strumTime).ToList();
        }

        /*foreach (List<NoteObject> nte in player1NotesObjects)
        {
            foreach (NoteObject nte2 in nte)
            {
                print(nte2.transform.position);
            }
        }*/

        /*
         * We now enable the notes UI for both players and disable the
         * generating song message UI.
         */
        player1Notes.gameObject.SetActive(true);
        player2Notes.gameObject.SetActive(true);

        generatingSongMsg.SetActive(false);

        healthBar.SetActive(true);
        playerOneScoringText.enabled = true;
        playerTwoScoringText.enabled = true;

        /*
         * Tells the entire script and other attached scripts that the song
         * started to play but has not fully started.
         */
        songSetupDone = true;
        songStarted = false;
        
        /*
         * Reset the stopwatch entirely.
         */
        stopwatch = new Stopwatch();

        /*
         * Stops any current music playing and sets it to not loop.
         */
        musicSources[0].loop = false;
        musicSources[0].volume = OptionsV2.instVolume;
        vocalSource.volume = OptionsV2.voicesVolume;
        vocalSource.mute = false;
        if (OpponentVocals != null)
        {
            OpponentVocals.volume = OptionsV2.voicesVolume;
            OpponentVocals.mute = false;
        }
        musicSources[0].Stop();

       /*
        * Initialize combo texts.
        */
       playerOneComboText.alpha = 0;
       playerTwoComboText.alpha = 0;

        /*
         * Disable the entire Menu UI and enable the entire Gameplay UI.
         */
        menuCanvas.enabled = false;
        battleCanvas.enabled = true;
        
        if (!OptionsV2.SongDuration)
            songDurationObject.SetActive(false);
        else
        {
            songDurationObject.SetActive(true);
            
            if (OptionsV2.Downscroll)
            {
                RectTransform rect = songDurationObject.GetComponent<RectTransform>();
                
                rect.anchoredPosition = new Vector3(0,-165,0);
            }
        }


        /*
         * If the player 2 in the chart exists in this engine,
         * we'll change player 2 to the correct character.
         *
         * If not, keep any existing character we selected.
         */
        if (!OptionsV2.DesperateMode)
        {
            LoadOpponent();
            LoadScene();
            vanillaPlayback?.SetupStage();
        }

        

        if (isDead)
        {
            isDead = false;
            respawning = false;

            deadCamera.enabled = false;

            
        }
        if(OptionsV2.SongDuration)
        {
            float time = musicClip.length - musicSources[0].time;

            int seconds = (int) (time % 60); // return the remainder of the seconds divide by 60 as an int
            time /= 60; // divide current time y 60 to get minutes
            int minutes = (int) (time % 60); //return the remainder of the minutes divide by 60 as an int

            songDurationText.text = minutes + ":" + seconds.ToString("00");

            songDurationBar.fillAmount = 0;
        }

        mainCamera.enabled = true;
        uiCamera.enabled = true;
        /*
         * Now we can fully start the song in a coroutine.
         */
        InitializeFunkinStrums();
        InitializeFunkinHud();
        StartCoroutine(nameof(SongStart), startSound.length);
    }

    void LoadOpponent()
    {
        if (!Cache.cachedOpponents.ContainsKey(_song.Player2) && charactersDictionary.TryGetValue(_song.Player2, out Character builtInCharacter))
            Cache.cachedOpponents.Add(_song.Player2, builtInCharacter);
        if (!OptionsV2.DesperateMode)
        {
            print("Checking for and applying " + _song.Player2 + ". Result is " +
                  Cache.cachedOpponents.ContainsKey(_song.Player2));
            if (Cache.cachedOpponents.ContainsKey(_song.Player2))
            {
                enemy = Cache.cachedOpponents[_song.Player2];
                opponentAnimator.spriteAnimations = enemy.animations;

                /*
                 * Yes, opponents can float if enabled in their
                 * configuration file.
                 */
                if (enemy.doesFloat)
                {
                    _enemyFloat = LeanTween.moveLocalY(opponentObject, enemy.floatToOffset, enemy.floatSpeed)
                        .setEaseInOutExpo()
                        .setLoopPingPong();
                }
                else
                {
                    /*
                     * In case any previous enemy floated before and this new one does not,
                     * we reset their position and cancel the floating tween.
                     */
                    if (_enemyFloat != null && LeanTween.isTweening(_enemyFloat.id))
                    {
                        LeanTween.cancel(_enemyFloat.id);
                        opponentObject.transform.position = _enemyDefaultPos;
                    }
                }

                enemyHealthIcon.sprite = enemy.portrait;
                enemyHealthIconRect.sizeDelta = enemy.portraitSize;
                enemyHealthBar.color = enemy.healthColor;
                playerTwoCornerImage.color = enemy.healthColor;

                Vector3 offset = enemy.cameraOffset;
                offset.z = -10;
                enemy.cameraOffset = offset;

                EnemyPlayAnimation("Idle");

                opponentAnimator.transform.localScale = new Vector2(enemy.scale, enemy.scale);

                CameraMovement.instance.playerTwoOffset = enemy.cameraOffset;
            }
            else
            {
                string charDir = selectedSongDir + "/Opponent";
                Dictionary<string, List<Sprite>> CharacterAnimations = new Dictionary<string, List<Sprite>>();

                if (Directory.Exists(charDir))
                {
                    opponentAnimator.spriteAnimations = new List<SpriteAnimation>();

                    // BEGIN ANIMATIONS IMPORT

                    var charMetaPath = charDir + "/char-meta.json";
                    var currentMeta = File.Exists(charMetaPath)
                        ? JsonConvert.DeserializeObject<CharacterMeta>(File.ReadAllText(charMetaPath))
                        : null;

                    foreach (string directoryPath in Directory.GetDirectories(charDir))
                    {
                        DirectoryInfo directoryInfo = new DirectoryInfo(directoryPath);

                        var files = directoryInfo.GetFiles("*.png");

                        List<Sprite> sprites = new List<Sprite>();

                        foreach (var file in files)
                        {
                            byte[] imageData = File.ReadAllBytes(file.ToString());

                            Texture2D imageTexture = new Texture2D(2, 2);
                            imageTexture.LoadImage(imageData);

                            var sprite = Sprite.Create(imageTexture,
                                new Rect(0, 0, imageTexture.width, imageTexture.height), new Vector2(0.5f, 0.0f), 100);
                            sprites.Add(sprite);
                        }

                        CharacterAnimations.Add(directoryInfo.Name, sprites);
                    }

                    foreach (string animationName in CharacterAnimations.Keys)
                    {
                        List<Vector2> offsets = new List<Vector2>();
                        SpriteAnimation newAnimation = ScriptableObject.CreateInstance<SpriteAnimation>();
                        List<SpriteAnimationFrame> frames = new List<SpriteAnimationFrame>();
                        for (var index = 0; index < CharacterAnimations[animationName].Count; index++)
                        {
                            Sprite sprite = CharacterAnimations[animationName][index];
                            Vector2 animationOffset = Vector2.zero;
                            if (currentMeta != null)
                            {
                                if (currentMeta.Offsets.ContainsKey(animationName))
                                {
                                    animationOffset = currentMeta.Offsets[animationName][index];
                                }
                            }
                            else
                            {
                                offsets.Add(animationOffset);
                            }

                            SpriteAnimationFrame newFrame = new SpriteAnimationFrame
                            {
                                Sprite = sprite,
                                Offset = animationOffset
                            };

                            frames.Add(newFrame);
                        }

                        newAnimation.Frames = frames;
                        newAnimation.Name = animationName;
                        newAnimation.FPS = 24;
                        newAnimation.SpriteAnimationType = SpriteAnimationType.PlayOnce;

                        opponentAnimator.spriteAnimations.Add(newAnimation);
                    }

                    Character newCharacter = ScriptableObject.CreateInstance<Character>();
                    newCharacter = currentMeta.Character;
                    if (File.Exists(charDir + "/Portrait.png"))
                    {
                        byte[] portraitFile = File.ReadAllBytes(charDir + "/Portrait.png");
                        Texture2D newTexture = new Texture2D(5, 5);
                        newTexture.LoadImage(portraitFile);
                        newCharacter.portrait = Sprite.Create(newTexture,
                            new Rect(0, 0, newTexture.width, newTexture.height),
                            Vector2.zero);
                    }
                    else
                    {
                        newCharacter.portrait = defaultEnemy.portrait;
                    }

                    if (File.Exists(charDir + "/Dead Portrait.png"))
                    {
                        byte[] portraitFile = File.ReadAllBytes(charDir + "/Dead Portrait.png");
                        Texture2D newTexture = new Texture2D(5, 5);
                        newTexture.LoadImage(portraitFile);
                        newCharacter.portraitDead = Sprite.Create(newTexture,
                            new Rect(0, 0, newTexture.width, newTexture.height),
                            Vector2.zero);
                    }
                    else
                    {
                        newCharacter.portraitDead = defaultEnemy.portraitDead;
                    }

                    enemy = newCharacter;


                    opponentAnimator.transform.localScale = new Vector2(newCharacter.scale, newCharacter.scale);

                    enemyHealthIcon.sprite = enemy.portrait;
                    enemyHealthIconRect.sizeDelta = enemy.portraitSize;
                    enemyHealthBar.color = enemy.healthColor;
                    playerTwoCornerImage.color = enemy.healthColor;


                    Vector3 offset = enemy.cameraOffset;
                    offset.z = -10;
                    enemy.cameraOffset = offset;

                    EnemyPlayAnimation("Idle");

                    CameraMovement.instance.playerTwoOffset = enemy.cameraOffset;
                    newCharacter.animations = opponentAnimator.spriteAnimations;
                    Cache.cachedOpponents.Add(_song.Player2, newCharacter);

                }
            }
        }
    }

    void LoadScene()
    {
        string sceneDir = selectedSongDir + "/Scene";
        if (Directory.Exists(sceneDir))
        {
            SceneData data = JsonConvert.DeserializeObject<SceneData>(File.ReadAllText(sceneDir + "/scene.json"));

            if (data != null)
            {
                if (!Cache.cachedScenes.ContainsKey(data.sceneName))
                {
                    string imagesDirectory = sceneDir + "/images";
                    if (Directory.Exists(imagesDirectory))
                    {
                        Dictionary<SceneObject, Sprite> sprites = new Dictionary<SceneObject, Sprite>();
                        foreach (SceneObject sceneObject in data.objects)
                        {
                            string path = imagesDirectory + "/" + sceneObject.fileName;
                            if (File.Exists(path))
                            {
                                byte[] imageData = File.ReadAllBytes(path);


                                Texture2D imageTexture = new Texture2D(2, 2);
                                imageTexture.LoadImage(imageData);

                                GameObject newImage = new GameObject();
                                SpriteRenderer renderer = newImage.AddComponent<SpriteRenderer>();
                                Sprite newSprite = Sprite.Create(imageTexture,
                                    new Rect(0, 0, imageTexture.width, imageTexture.height), Vector2.zero, 100);
                                renderer.sprite = newSprite;
                                renderer.sortingOrder = sceneObject.layer;
                                newImage.name = Path.GetFileName(path);

                                newImage.transform.position = sceneObject.position;
                                newImage.transform.localScale = sceneObject.size;
                                newImage.transform.rotation = sceneObject.rotation;

                                sprites.Add(sceneObject, newSprite);
                            }
                        }

                        Cache.cachedScenes.Add(data.sceneName, sprites);
                    }
                }
                else
                {
                    Dictionary<SceneObject, Sprite> sceneObjectsCache = Cache.cachedScenes[data.sceneName];

                    foreach (SceneObject sceneObject in sceneObjectsCache.Keys)
                    {
                        GameObject newImage = new GameObject();
                        SpriteRenderer renderer = newImage.AddComponent<SpriteRenderer>();
                        renderer.sprite = sceneObjectsCache[sceneObject];
                        renderer.sortingOrder = sceneObject.layer;
                        newImage.name = sceneObject.fileName;

                        newImage.transform.position = sceneObject.position;
                        newImage.transform.localScale = sceneObject.size;
                        newImage.transform.rotation = sceneObject.rotation;
                    }
                }

                foreach (GameObject sceneObject in defaultSceneObjects) Destroy(sceneObject);
            }
        }
    }
    
    IEnumerator SongStart(float delay)
    {
        /*
         * If we are in demo mode, delete any temp charts.
         */
        if (Player.demoMode)
        {
            if(File.Exists(Application.persistentDataPath + "/tmp/ok.json"))
                File.Delete(Application.persistentDataPath + "/tmp/ok.json");
            if(Directory.Exists(Application.persistentDataPath + "/tmp"))
                Directory.Delete(Application.persistentDataPath + "/tmp");
        }

        startSongTooltip.SetActive(false);

        vanillaPlayback?.Presentation?.PrepareIntro(this);
        LoadingTransition.instance.Hide();
        while (LoadingTransition.instance != null && LoadingTransition.instance.toggled) yield return null;

        DiscordController.instance.EnableGameStateLoop = true;

        if (vanillaPlayback == null)
        {
            startSongTooltip.GetComponentInChildren<TMP_Text>().text = $"Press {Player.keybinds.startSongKeyCode} to start the song.";
            startSongTooltip.SetActive(true);
            yield return new WaitUntil(() => Input.GetKeyDown(Player.keybinds.startSongKeyCode) || Player.ControllerConfirmPressed || Player.ControllerPausePressed);
        }
        startSongTooltip.SetActive(false);
        /*
        * Start the countdown audio.
        *
        * Unlike FNF, this does not dynamically change based on BPM.
        */
        if (vanillaPlayback != null && vanillaPlayback.Presentation != null)
        {
            yield return vanillaPlayback.Presentation.Intro(this);
            if (!songSetupDone || Pause.instance != null && Pause.instance.Transitioning) yield break;
            yield return vanillaPlayback.Presentation.Countdown(this);
        }
        else
        {
            BeginFunkinCountdown(delay);
            soundSource.clip = startSound;
            soundSource.Play();
            while (CountdownPosition < 0) yield return null;
        }
        
        /*
         * Wait for the countdown to finish.
         */
        if (!OptionsV2.LiteMode && (vanillaPlayback == null || !vanillaPlayback.UsesSourceCamera)
            && !(vanillaPlayback != null && vanillaPlayback.Presentation != null && vanillaPlayback.Presentation.OwnsCamera))
            mainCamera.orthographicSize = 4;
        
        /*
         * Start the beat stopwatch.
         *
         * This is used to precisely calculate when a beat happens based
         * on the BPM or BPS.
         */
        StartSongAudio();
    }

    private void StartSongAudio()
    {
        if (isDead) return;
        beatStopwatch = new Stopwatch();
        beatStopwatch.Start();


        /*
         * Sets the voices and music audio sources clips to what
         * they should have.
         */
        musicSources[0].clip = musicClip;
        if(hasVoiceLoaded)
            vocalSource.clip = vocalClip;

        /*
         * In case we have more than one audio source,
         * let's tell them all to play.
         */
        if (OpponentVocals != null) OpponentVocals.mute = vanillaPlayback?.Week3Stage?.OpponentExploded == true;
        foreach (AudioSource source in musicSources)
        {
            if (source != musicSources[0] && currentSongMeta?.freeplayInstrumentalStart > 0)
                StartCoroutine(PlayOffsetVocals(source));
            else source.Play();
        }


        /*
         * Plays the vocal audio source then tells this script and other
         * attached scripts that the song fully started.
         */
        if(hasVoiceLoaded)
        {
            if (currentSongMeta?.freeplayInstrumentalStart > 0) StartCoroutine(PlayOffsetVocals(vocalSource));
            else vocalSource.Play();
        }

        IsCountingDown = false;
        songStarted = true;
        
        modInstance?.Invoke("OnSongStarted");


        /*
         * Start subtitles.
         */
        if(usingSubtitles)
        {
            subtitleDisplayer.paused = false;
            subtitleDisplayer.StartSubtitles();
        }
        /*
         * Restart the stopwatch for the song itself.
         */
        stopwatch.Restart();


    }
    
    
    public void GenNote(FNFSong.FNFSection section, List<decimal> data)
    {
        bool mustHit = data[1] > 3 ? !section.MustHitSection : section.MustHitSection;
        int direction = (int)(data[1] % 4);
        var pool = direction == 0 ? leftNotesPool : direction == 1 ? downNotesPool : direction == 2 ? upNotesPool : rightNotesPool;
        NoteObject note = pool.GetObject().GetComponent<NoteObject>();
        note.Initialize(this, (double)data[0], direction, mustHit, (double)data[2], ChartScrollSpeed, section.MustHitSection ? 1 : 2);
        var lane = mustHit ? player1NotesObjects[direction] : player2NotesObjects[direction];
        int index = lane.FindIndex(item => item.strumTime > note.strumTime);
        if (index < 0) lane.Add(note);
        else lane.Insert(index, note);
        Player.instance.Strumlines[mustHit ? 0 : 1].Add(note.State);
        lastNote = note;
    }

    #region Pause Menu
    public void PauseSong()
    {
        Pause.instance.PauseSong();
    }

    public void ContinueSong()
    {
        Pause.instance.ContinueSong();
    }

    public void RestartSong()
    {
        Pause.instance.RestartSong();
    }

    public void CompleteScriptedSong()
    {
        if (!songSetupDone || FreeplayAborted || isDead) return;
        foreach (AudioSource source in musicSources) source.Stop();
        vocalSource.Stop();
        Player.instance?.ClearInput();
        FinishSong(true);
    }

    private bool enteringResults;
    private bool enteringStorySong;
    public bool EnteringResults => enteringResults;

    private void FinishSong(bool scriptedCompletion, bool afterTransition = false)
    {
        if ((enteringResults || enteringStorySong) && !afterTransition) return;
        bool finished = !FreeplayAborted && (scriptedCompletion || musicClip != null
            && stopwatch.Elapsed.TotalMilliseconds >= musicClip.length * 1000 - 100);
        if (!afterTransition && finished && VanillaStoryCampaign.CanTransitionSeamlessly(this))
        {
            enteringStorySong = true;
            songStarted = false;
            stopwatch?.Stop();
            beatStopwatch?.Stop();
            Player.instance?.ClearInput();
            foreach (AudioSource source in musicSources) source.Stop();
            vocalSource.Stop();
            LoadingTransition.instance.HoldStoryFrame(() => FinishSong(scriptedCompletion, true));
            return;
        }
        if (!afterTransition && finished && (!VanillaStoryCampaign.Running || VanillaStoryCampaign.IsLastSong))
        {
            enteringResults = true;
            songSetupDone = false;
            songStarted = false;
            Player.instance?.ClearInput();
            foreach (AudioSource source in musicSources) source.Stop();
            vocalSource.Stop();
            StartCoroutine(VanillaResultsScreen.Enter(() => FinishSong(scriptedCompletion, true)));
            return;
        }
        MenuV2.startPhase = MenuV2.StartPhase.SongList;

        LeanTween.cancelAll();

        stopwatch?.Stop();
        beatStopwatch?.Stop();

        bool completed = !FreeplayAborted && (scriptedCompletion || musicClip != null
            && stopwatch.Elapsed.TotalMilliseconds >= musicClip.length * 1000 - 100);

        if (usingSubtitles)
        {
            subtitleDisplayer.StopSubtitles();
            subtitleDisplayer.paused = false;
            usingSubtitles = false;

        }

        girlfriendAnimator.Play("GF Dance Loop");
        boyfriendAnimator.Play("BF Idle Loop");

        Player.demoMode = false;

        songSetupDone = false;
        songStarted = false;
        foreach (List<NoteObject> noteList in player1NotesObjects.ToList())
        {
            foreach (NoteObject noteObject in noteList.ToList())
            {
                noteList.Remove(noteObject);
            }
        }

        foreach (List<NoteObject> noteList in player2NotesObjects.ToList())
        {
            foreach (NoteObject noteObject in noteList.ToList())
            {
                noteList.Remove(noteObject);

            }
        }

        leftNotesPool.ReleaseAll();
        downNotesPool.ReleaseAll();
        upNotesPool.ReleaseAll();
        rightNotesPool.ReleaseAll();
        holdNotesPool.ReleaseAll();

        battleCanvas.enabled = false;

        player1Notes.gameObject.SetActive(false);
        player2Notes.gameObject.SetActive(false);

        healthBar.SetActive(false);

        menuScreen.SetActive(false);

        string highScoreSave = currentSongMeta.songName + currentSongMeta.bundleMeta.bundleName +
            difficulty.ToLower() +
            modeOfPlay;

        int playerNotes = _noteBehaviours.Count(note => note.noteData.ConvertToNote()[1] > 3 ? !note.section.MustHitSection : note.section.MustHitSection);
        int opponentNotes = _noteBehaviours.Count - playerNotes - (vanillaPlayback?.UnscoredNotes(1) ?? 0);
        playerNotes -= vanillaPlayback?.UnscoredNotes(0) ?? 0;
        var rankChange = VanillaFreeplayCatalog.SaveCompletion(currentSongMeta, difficulty, modeOfPlay, playerOneStats,
            completed && !Pause.PracticeMode, playerNotes);

        int overallScore = 0;

        int currentHighScore = PlayerPrefs.GetInt(highScoreSave, 0);

        switch (modeOfPlay)
        {
            case PlayModes.Boyfriend:
                overallScore = playerOneStats.currentScore;
                break;
            case PlayModes.Opponent:
                overallScore = playerTwoStats.currentScore;
                break;
            case PlayModes.Autoplay:
                overallScore = 0;
                break;
        }

        bool newHighscore = completed && !Pause.PracticeMode && modeOfPlay != PlayModes.Autoplay && overallScore > currentHighScore;
        if (newHighscore)
        {
            PlayerPrefs.SetInt(highScoreSave, overallScore);
            PlayerPrefs.Save();
        }

        bool storyResults = VanillaStoryCampaign.Running;
        int previousWeekScore = storyResults ? VanillaStoryCampaign.HighScore(VanillaStoryCampaign.LevelId, difficulty) : 0;
        var results = VanillaResultsData.Capture(modeOfPlay == PlayModes.Opponent ? playerTwoStats : playerOneStats,
            modeOfPlay == PlayModes.Opponent ? opponentNotes : playerNotes);
        if (modeOfPlay == PlayModes.Autoplay && Player.instance != null)
        {
            results.sick = results.totalNotesHit = Player.instance.Strumlines[0].HeadsHit;
            results.missed = Math.Max(0, playerNotes - results.totalNotesHit);
            results.maxCombo = results.totalNotesHit;
        }
        string resultTitle = currentSongMeta.GetVariation(difficulty)?.songName ?? currentSongMeta.songName;
        var resultCredits = currentSongMeta.GetVariation(difficulty)?.credits ?? currentSongMeta.credits;
        if (resultCredits != null && resultCredits.TryGetValue("Composer", out string resultArtist)) resultTitle += " by " + resultArtist;
        string resultDifficulty = difficulty;
        string resultCharacter = vanillaPlayback?.PlayerId ?? _song.Player1;
        bool nextStorySong = VanillaStoryCampaign.CompleteSong(currentSongMeta, difficulty, modeOfPlay, overallScore,
            completed, !Pause.PracticeMode, results);
        if (storyResults && !nextStorySong && completed)
        {
            results = VanillaStoryCampaign.Results;
            resultTitle = VanillaStoryCatalog.Load().Find(level => level.id == VanillaStoryCampaign.LevelId)?.name ?? resultTitle;
            newHighscore = !Pause.PracticeMode && VanillaStoryCampaign.Score > previousWeekScore
                && VanillaStoryCampaign.HighScore(VanillaStoryCampaign.LevelId, resultDifficulty) > previousWeekScore;
        }
        results.title = resultTitle;
        results.difficulty = resultDifficulty;
        results.characterId = resultCharacter;
        results.storyMode = storyResults;
        results.newHighscore = newHighscore;
        results.rankImproved = rankChange != null && !storyResults;
        Pause.ResetSession();
        if (completed && !nextStorySong)
        {
            if (!storyResults) VanillaFreeplay.PrepareResultsReturn(currentSongMeta, resultDifficulty, modeOfPlay, results.Character, rankChange);
            foreach (AudioSource source in musicSources) source.Stop();
            vocalSource.Stop();
            Player.instance?.ClearInput();
            VanillaResultsScreen.Open(results, () =>
            {
                SceneManager.LoadScene("Title");
                DiscordController.instance.EnableGameStateLoop = false;
            });
            return;
        }
        LoadingTransition.instance.LoadScene(nextStorySong ? "Game_Backup3" : "Title", () => DiscordController.instance.EnableGameStateLoop = false);
    }

    private void HandleManualStartExit(bool pressed)
    {
        if (!pressed || vanillaPlayback != null) return;
        LoadingTransition.instance.LoadScene("Title", () => DiscordController.instance.EnableGameStateLoop = false);
    }

    public void QuitSong()
    {
        Pause.instance.QuitSong();
    }

    #endregion
    #endregion

    public void QuitGame()
    {
        Application.Quit();
    }


    
    #region Animating

    public void EnemyPlayAnimation(string animationName)
    {
        if (enemy == null || enemy.idleOnly || OptionsV2.DesperateMode) return;
        opponentAnimator.Play(animationName);
        _currentEnemyIdleTimer = enemyIdleTimer;
    }

    public void PlayChartAnimation(bool player, string animationName)
    {
        var animator = player ? boyfriendAnimator : opponentAnimator;
        if (!animator.spriteAnimations.Any(animation => animation != null && animation.Name == animationName))
            return;
        animator.Play(animationName);
        if (player)
            _currentBoyfriendIdleTimer = 1.1f;
        else
            _currentEnemyIdleTimer = 1.1f;
    }

    private void BoyfriendPlayAnimation(string animationName)
    {
        if (OptionsV2.DesperateMode) return;
        boyfriendAnimator.Play("BF " + animationName);

        
        _currentBoyfriendIdleTimer = boyfriendIdleTimer;
    }
    
    public void AnimateNote(int player, int type, string animName)
    {
        if (Player.instance == null) return;
        var animation = animName == "Activated" ? FunkinStrumline.Animation.Confirm :
            animName == "Pressed" ? FunkinStrumline.Animation.Press : FunkinStrumline.Animation.Static;
        Player.instance.Strumlines[player - 1].Play(type, animation);
    }

    #endregion

    #region Note & Score Registration

    public enum Rating
    {
        Sick = 1,
        Good = 2,
        Bad = 3,
        Shit = 4
    }

    public void UpdateScoringInfo()
    {
        
        if (!Player.playAsEnemy || Player.demoMode)
        {
            
            float accuracyPercent;
            if(playerOneStats.totalNoteHits != 0)
            {
                float sickScore = playerOneStats.totalSicks * 4;
                float goodScore = playerOneStats.totalGoods * 3;
                float badScore = playerOneStats.totalBads * 2;
                float shitScore = playerOneStats.totalShits;

                float totalAccuracyScore = sickScore + goodScore + badScore + shitScore;

                var accuracy = totalAccuracyScore / (playerOneStats.totalNoteHits * 4);
                
                accuracyPercent = (float) Math.Round(accuracy, 4);
                accuracyPercent *= 100;
            }
            else
            {
                accuracyPercent = 0;
            }

            playerOneScoringText.text =
                $"Score: {playerOneStats.currentScore}\nAccuracy: {accuracyPercent:0.00}%\nMisses: {playerOneStats.missedHits}";
        
            //playerOneScoringText.text = playerOneStats.currentScore.ToString("00000000");
        }
        else
        {
            playerOneScoringText.text = string.Empty;
        }

        if (Player.playAsEnemy || Player.demoMode)
        {
            
            float accuracyPercent;
            if(playerTwoStats.totalNoteHits != 0)
            {
                float sickScore = playerTwoStats.totalSicks * 4;
                float goodScore = playerTwoStats.totalGoods * 3;
                float badScore = playerTwoStats.totalBads * 2;
                float shitScore = playerTwoStats.totalShits;

                float totalAccuracyScore = sickScore + goodScore + badScore + shitScore;

                var accuracy = totalAccuracyScore / (playerTwoStats.totalNoteHits * 4);
                
                accuracyPercent = (float) Math.Round(accuracy, 4);
                accuracyPercent *= 100;
            }
            else
            {
                accuracyPercent = 0;
            }

            playerTwoScoringText.text =
                $"Score: {playerTwoStats.currentScore}\nAccuracy: {accuracyPercent:0.00}%\nMisses: {playerTwoStats.missedHits}";
        
            //playerTwoScoringText.text = playerTwoStats.currentScore.ToString("00000000");

        }
        else
        {
            playerTwoScoringText.text = string.Empty;
        }
    }
    
    public void NoteHit(NoteObject note)
    {
        if (note == null || note.State == null || Player.instance == null) return;
        int side = note.mustHit ? 0 : 1;
        var line = Player.instance.Strumlines[side];
        double position = SongPosition - Player.visualOffset;
        if (vanillaPlayback?.CampaignStage?.Week == 8 && !vanillaPlayback.CampaignStage.CanHitWeekendNote(note.mustHit ? 0 : 1, note.type, note.State.Time)) return;
        line.Hit(note.State, position - note.strumTime - Player.inputOffset, !line.Controlled || Player.demoMode, position);
    }

    public void NoteMiss(NoteObject note)
    {
        if (note == null) return;
        if (note.dummyNote) ApplyFunkinGhost(note.mustHit ? 0 : 1, note.type);
        else if (note.State != null && !note.State.HandledMiss)
        {
            note.State.Missed = note.State.HandledMiss = true;
            note.State.HoldDropped = note.State.Length > 0;
            ApplyFunkinMiss(note);
        }
    }

    #endregion

    


    // Update is called once per frame
    void Update()
    {
        if (Pause.instance != null && (Pause.instance.IsPaused || Pause.instance.Transitioning || Pause.instance.editingVolume)) return;
        if (songSetupDone)
        {
            modInstance?.Invoke("Update");
            
            _noteSchedule.Advance(this);
            
            if (songStarted & musicSources[0].isPlaying)
            {
                
                if(OptionsV2.SongDuration)
                {
                    float t = musicClip.length - musicSources[0].time;

                    int seconds = (int) (t % 60); // return the remainder of the seconds divide by 60 as an int
                    t /= 60; // divide current time y 60 to get minutes
                    int minutes = (int) (t % 60); //return the remainder of the minutes divide by 60 as an int

                    songDurationText.text = minutes + ":" + seconds.ToString("00");

                    songDurationBar.fillAmount = musicSources[0].time / musicClip.length;
                }
                if ((float)beatStopwatch.ElapsedMilliseconds / 1000 >= beatsPerSecond)
                {
                    beatStopwatch.Restart();
                    currentBeat++;
                    
                    modInstance?.Invoke("OnBeat",currentBeat);
                    if (_currentBoyfriendIdleTimer <= 0 & currentBeat % 2 == 0)
                    {
                        boyfriendAnimator.Play("BF Idle");
                    }

                    if (_currentEnemyIdleTimer <= 0 & currentBeat % 2 == 0)
                    {
                        opponentAnimator.Play("Idle");
                    }

                    
                    
                    if (!OptionsV2.LiteMode)
                    {
                        if (altDance)
                        {
                            girlfriendAnimator.Play("GF Dance Left");
                            altDance = false;
                        }
                        else
                        {
                            girlfriendAnimator.Play("GF Dance Right");
                            altDance = true;
                        }
                        
                        if (currentBeat % 4 == 0 && (vanillaPlayback == null || !vanillaPlayback.UsesSourceCamera))
                        {
                            mainCamera.orthographicSize = defaultGameZoom - .1f;
                            uiCamera.orthographicSize = _defaultZoom - .1f;
                        }
                    }
                }

                if (vanillaPlayback == null || !vanillaPlayback.UsesSourceCamera)
                {
                    mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, defaultGameZoom,cameraBopLerpSpeed);
                    uiCamera.orthographicSize = Mathf.Lerp(uiCamera.orthographicSize, _defaultZoom, cameraBopLerpSpeed);
                }
            }
            else if (!songStarted && !IsCountingDown && !musicSources[0].isPlaying)
            {
                HandleManualStartExit(Input.GetKeyDown(Player.keybinds.pauseKeyCode) || Player.ControllerBackPressed);
            }
            
            
            if (health > MAXHealth)
                health = MAXHealth;
            if (health <= 0)
            {
                health = 0;
                if(!Player.playAsEnemy & !Player.demoMode & !Pause.PracticeMode)
                {
                    if (isDead)
                    {
                        if (!respawning)
                        {
                            if (VanillaControls.Pressed("ACCEPT"))
                            {
                                ConfirmRetry();
                            } else if (VanillaControls.Pressed("BACK"))
                            {
                                Pause.instance.QuitSong();
                            }
                        }
                    }
                    else
                    {
                        isDead = true;
                        CancelSongPlayback();
                        Pause.RecordDeath();
                        
                        modInstance?.Invoke("OnDeath");


                        deathBlackout.color = Color.clear;

                        musicSources[0].PlayOneShot(deadNoise);

                        battleCanvas.enabled = false;

                        uiCamera.enabled = false;
                        mainCamera.enabled = false;
                        deadCamera.enabled = true;

                        beatStopwatch?.Reset();
                        stopwatch.Reset();

                        subtitleDisplayer.StopSubtitles();
                        subtitleDisplayer.paused = false;

                        deadBoyfriend.transform.position = boyfriendObject.transform.position;
                        deadBoyfriend.transform.localScale = boyfriendObject.transform.localScale;

                        deadCamera.orthographicSize = mainCamera.orthographicSize;
                        deadCamera.transform.position = mainCamera.transform.position;

                        deadBoyfriendAnimator.Play("Dead Start");

                        Vector3 newPos = deadBoyfriend.transform.position;
                        newPos.y += 2.95f;
                        newPos.z = -10;

                        LeanTween.move(deadCamera.gameObject, newPos, .5f).setEaseOutExpo();
                        vanillaPlayback?.CharacterStage?.BeginDeath();

                        deathLoopTween = LeanTween.delayedCall(vanillaPlayback?.CampaignStage != null ? vanillaPlayback.CampaignStage.DeathDuration : vanillaPlayback?.PlayerId.StartsWith("pico") == true ? 35f / 24 : 2.417f, () =>
                        {
                            if (isDead && !respawning)
                            {
                                musicSources[0].clip = deadTheme;
                                musicSources[0].loop = true;
                                musicSources[0].Play();
                                deadBoyfriendAnimator.Play("Dead Loop");
                                vanillaPlayback?.CharacterStage?.PlayDeath("deathLoop");
                            }
                        }).id;
                    }
                }
            }
            

            if (!musicSources[0].isPlaying & songStarted & !isDead & !respawning & !Pause.instance.pauseScreen.activeSelf & !Pause.instance.editingVolume)
            {
                if (vanillaPlayback != null && vanillaPlayback.Presentation != null && !vanillaPlayback.Presentation.AllowEnd(this)) return;
                FinishSong(false);
            }
        }
        else if (vanillaPlayback?.CharacterStage == null)
        {
            _bfRandomDanceTimer -= Time.deltaTime;
            _enemyRandomDanceTimer -= Time.deltaTime;

            if (_bfRandomDanceTimer <= 0)
            {
                switch (Random.Range(0, 4))
                {
                    case 1:
                        BoyfriendPlayAnimation("Sing Left");
                        break;
                    case 2:
                        BoyfriendPlayAnimation("Sing Down");
                        break;
                    case 3:
                        BoyfriendPlayAnimation("Sing Up");
                        break;
                    case 4:
                        BoyfriendPlayAnimation("Sing Right");
                        break;
                    default:
                        BoyfriendPlayAnimation("Sing Left");
                        break;
                }

                _bfRandomDanceTimer = Random.Range(.5f, 3f);
            }
            if (_enemyRandomDanceTimer <= 0)
            {
                switch (Random.Range(0, 4))
                {
                    case 1:
                        EnemyPlayAnimation("Sing Left");
                        break;
                    case 2:
                        EnemyPlayAnimation("Sing Down");
                        break;
                    case 3:
                        EnemyPlayAnimation("Sing Up");
                        break;
                    case 4:
                        EnemyPlayAnimation("Sing Right");
                        break;
                    default:
                        EnemyPlayAnimation("Sing Left");
                        break;
                }

                _enemyRandomDanceTimer = Random.Range(.5f, 3f);
            }
        }

        if (ratingLayerTimer > 0)
        {
            ratingLayerTimer -= Time.deltaTime;
            if (ratingLayerTimer < 0)
                _currentRatingLayer = 0;
        }
        
        if (OptionsV2.DesperateMode || vanillaPlayback?.CharacterStage != null) return;
        if ((opponentAnimator.CurrentAnimation == null || !opponentAnimator.CurrentAnimation.Name.Contains("Idle")) & !songStarted)
        {
            _currentEnemyIdleTimer -= Time.deltaTime;
            if (_currentEnemyIdleTimer <= 0)
            {
                opponentAnimator.Play("Idle Loop");
                _currentEnemyIdleTimer = enemyIdleTimer;
            }
        }
        else
        {
            _currentEnemyIdleTimer -= Time.deltaTime;
        }

        if ((boyfriendAnimator.CurrentAnimation == null || !boyfriendAnimator.CurrentAnimation.Name.Contains("Idle")) & !songStarted)
        {

            _currentBoyfriendIdleTimer -= Time.deltaTime;
            if (_currentBoyfriendIdleTimer <= 0)
            {
                boyfriendAnimator.Play("BF Idle Loop");
                _currentBoyfriendIdleTimer = boyfriendIdleTimer;
            }
        }
        else
        {
            _currentBoyfriendIdleTimer -= Time.deltaTime;
        }


    }
}
