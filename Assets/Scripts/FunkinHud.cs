using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(250)]
public sealed class FunkinHud : MonoBehaviour
{
    private sealed class Popup
    {
        public SpriteRenderer Renderer;
        public FunkinPopupState State;
        public float Scale;
    }

    private sealed class Letter
    {
        public SpriteRenderer Renderer;
        public float X;
        public float Y;
    }

    public double DisplayHealth { get; private set; } = 100;
    public readonly FunkinHealthIconState[] Icons = { new FunkinHealthIconState(), new FunkinHealthIconState() };
    public int PopupCount => popups.Count;
    public string ScoreText { get; private set; }
    private Song song;
    private SpriteRenderer background;
    private SpriteRenderer red;
    private SpriteRenderer green;
    private readonly SpriteRenderer[] iconRenderers = new SpriteRenderer[2];
    private readonly Sprite[][] iconFrames = new Sprite[2][];
    private readonly List<Popup> popups = new List<Popup>();
    private readonly List<SpriteRenderer> popupPool = new List<SpriteRenderer>();
    private readonly List<Letter> letters = new List<Letter>();
    private int previousStep = int.MinValue;
    private bool oldPlayerIcon;
    private bool wasVisible;
    private float pixel;
    private int displayedScore;
    private bool displayedAutoplay;

    public void Initialize(Song owner, string opponent)
    {
        song = owner;
        gameObject.layer = song.player1NoteSprites[0].gameObject.layer;
        ClearPopups();
        DisplayHealth = 100;
        previousStep = int.MinValue;
        oldPlayerIcon = false;
        string imagePrefix = song.vanillaPlayback != null && song.vanillaPlayback.IsPixel ? "Pixel/" : "";
        foreach (string rating in new[] { "sick", "good", "bad", "shit" }) FunkinHudAssets.Image(imagePrefix + rating);
        for (int digit = 0; digit < 10; digit++) FunkinHudAssets.Image(imagePrefix + "num" + digit);
        iconFrames[0] = FunkinHudAssets.Icon(song.vanillaPlayback?.PlayerId ?? "bf");
        iconFrames[1] = FunkinHudAssets.Icon(opponent == "tankman-bloody" ? "tankman" : opponent);
        foreach (var icon in Icons) icon.Reset();
        if (background == null)
        {
            background = CreateRenderer("Health Bar Border", 800);
            background.sprite = FunkinHudAssets.Image("healthBar");
            red = CreateRenderer("Opponent Health", 801);
            red.sprite = FunkinHudAssets.Solid;
            red.color = Color.red;
            green = CreateRenderer("Player Health", 802);
            green.sprite = FunkinHudAssets.Solid;
            green.color = new Color32(102, 255, 51, 255);
            for (int side = 0; side < 2; side++) iconRenderers[side] = CreateRenderer("Health Icon " + side, 850);
        }
        foreach (Graphic graphic in song.healthBar.GetComponentsInChildren<Graphic>(true)) graphic.enabled = false;
        song.playerOneScoringObject.SetActive(false);
        song.playerTwoScoringObject.SetActive(false);
        song.playerOneComboText.enabled = song.playerTwoComboText.enabled = false;
        song.liteRatingObjectP1.SetActive(false);
        song.liteRatingObjectP2.SetActive(false);
        ScoreText = null;
        Render();
    }

    private SpriteRenderer CreateRenderer(string name, int order)
    {
        var child = new GameObject(name);
        child.layer = gameObject.layer;
        child.transform.SetParent(transform, false);
        var sprite = child.AddComponent<SpriteRenderer>();
        sprite.sharedMaterial = FunkinNoteSkin.NoteMaterial;
        sprite.sortingLayerID = song.player1NoteSprites[0].sortingLayerID;
        sprite.sortingOrder = order;
        return sprite;
    }

    private void LateUpdate()
    {
        if (song == null) return;
        bool visible = song.songSetupDone && song.healthBar.activeInHierarchy && song.battleCanvas.enabled && !song.isDead;
        if (visible != wasVisible)
        {
            foreach (Transform child in transform) child.gameObject.SetActive(visible);
            wasVisible = visible;
        }
        if (!visible) return;
        bool paused = Pause.instance != null && Pause.instance.pauseScreen.activeSelf;
        if (!paused && (song.IsCountingDown || song.stopwatch != null && song.stopwatch.IsRunning))
        {
            Advance(Time.deltaTime, song.SongPosition);
            if (Input.GetKeyDown(KeyCode.Alpha9)) TogglePlayerIcon();
        }
        Render();
    }

    public void TogglePlayerIcon()
    {
        oldPlayerIcon = !oldPlayerIcon;
        iconFrames[0] = FunkinHudAssets.Icon(oldPlayerIcon ? "bf-old" : song.vanillaPlayback?.PlayerId ?? "bf");
        Icons[0].Reset();
    }

    public void SetIcon(int side, string id)
    {
        if (side < 0 || side >= iconFrames.Length || string.IsNullOrEmpty(id)) return;
        iconFrames[side] = FunkinHudAssets.Icon(id);
        Icons[side].Reset();
    }

    public void Advance(double elapsed, double position)
    {
        DisplayHealth = FunkinHudRules.SmoothHealth(DisplayHealth, song.health, Player.demoMode);
        for (int side = 0; side < 2; side++)
        {
            Icons[side].Advance(elapsed);
            Icons[side].UpdateFace(side == 0 ? song.health : 200 - song.health, iconFrames[side].Length >= 3);
        }
        int step = song.vanillaPlayback != null && song.vanillaPlayback.UsesSourceCamera
            ? Mathf.FloorToInt(song.vanillaPlayback.BeatAt(position) * 4)
            : (int)System.Math.Floor(position / System.Math.Max(1, song.stepCrochet));
        if (step != previousStep && step % 4 == 0)
            foreach (var icon in Icons) icon.Bop(song.stepCrochet);
        previousStep = step;
        for (int index = popups.Count - 1; index >= 0; index--)
        {
            Popup popup = popups[index];
            popup.State.Advance(elapsed);
            if (!popup.State.Finished) continue;
            popup.Renderer.enabled = false;
            popupPool.Add(popup.Renderer);
            popups.RemoveAt(index);
        }
    }

    public void ClearPopups()
    {
        foreach (Popup popup in popups)
        {
            popup.Renderer.enabled = false;
            popupPool.Add(popup.Renderer);
        }
        popups.Clear();
    }

    private void AddPopup(string image, float scale, FunkinPopupState state)
    {
        SpriteRenderer sprite;
        if (popupPool.Count == 0) sprite = CreateRenderer("Judgement Popup", 900);
        else
        {
            sprite = popupPool[popupPool.Count - 1];
            popupPool.RemoveAt(popupPool.Count - 1);
        }
        sprite.sprite = FunkinHudAssets.Image(image);
        sprite.enabled = true;
        sprite.color = Color.white;
        popups.Add(new Popup { Renderer = sprite, State = state, Scale = scale });
    }

    public void ShowRating(FunkinRules.Judgement judgement)
    {
        if (judgement == FunkinRules.Judgement.Miss) return;
        string image = judgement.ToString().ToLowerInvariant();
        bool isPixel = song.vanillaPlayback != null && song.vanillaPlayback.IsPixel;
        if (isPixel) image = "Pixel/" + image;
        float scale = isPixel ? 4.2f : .65f;
        Sprite sprite = FunkinHudAssets.Image(image);
        AddPopup(image, scale, new FunkinPopupState
        {
            X = 1280 * 0.474 - sprite.rect.width * scale / 2,
            Y = 720 * 0.45 - 60 - sprite.rect.height * scale / 2,
            VelocityY = -Random.Range(140, 176),
            VelocityX = -Random.Range(0, 11),
            Gravity = 550,
            FadeDelay = song.beatsPerSecond
        });
    }

    public void ShowCombo(int combo)
    {
        string digits = combo.ToString("000", CultureInfo.InvariantCulture);
        bool isPixel = song.vanillaPlayback != null && song.vanillaPlayback.IsPixel;
        for (int index = 0; index < digits.Length; index++)
            AddPopup((isPixel ? "Pixel/num" : "num") + digits[digits.Length - index - 1], isPixel ? 4.2f : .45f, new FunkinPopupState
            {
                X = 1280 * 0.507 - 36 * (index + 1) - 65,
                Y = 720 * 0.44,
                VelocityY = -Random.Range(130, 151),
                VelocityX = Random.Range(-5f, 5f),
                Gravity = Random.Range(250, 301),
                FadeDelay = song.beatsPerSecond * 2
            });
    }

    private void Place(SpriteRenderer sprite, double x, double y, double width, double height, bool flip = false)
    {
        sprite.transform.position = new Vector3(song.uiCamera.transform.position.x + ((float)x - 640) * pixel,
            song.uiCamera.transform.position.y + (360 - (float)y) * pixel, song.player1NoteSprites[0].transform.position.z);
        sprite.transform.localRotation = Quaternion.identity;
        Vector3 parent = sprite.transform.parent.lossyScale;
        sprite.transform.localScale = new Vector3((float)width / sprite.sprite.rect.width * 100 * pixel / parent.x,
            (float)height / sprite.sprite.rect.height * 100 * pixel / parent.y, 1);
        sprite.flipX = flip;
        if (flip) sprite.transform.position += new Vector3((float)width * pixel, 0, 0);
    }

    public void Render()
    {
        if (song == null || background == null) return;
        pixel = song.HudReferenceOrthographicSize * 2 / 720;
        double x = FunkinHudRules.BarX;
        double y = FunkinHudRules.BarY(OptionsV2.Downscroll);
        Place(background, x, y, 601, 19);
        Place(red, x + 4, y + 4, 593, 11);
        int fill = FunkinHudRules.FillPixels(DisplayHealth);
        green.enabled = fill > 0;
        Place(green, x + 4 + 593 - fill, y + 4, fill, 11);
        double boundary = FunkinHudRules.Boundary(DisplayHealth);
        for (int side = 0; side < 2; side++)
        {
            int frame = Icons[side].Animation == FunkinHealthIconState.Face.Losing ? 1 :
                Icons[side].Animation == FunkinHealthIconState.Face.Winning ? 2 : 0;
            iconRenderers[side].sprite = iconFrames[side][System.Math.Min(frame, iconFrames[side].Length - 1)];
            double width = Icons[side].Width;
            Place(iconRenderers[side], boundary + (side == 0 ? -26 : 26 - width), y + 4 - width / 2, width, width, side == 0);
        }
        for (int index = 0; index < popups.Count; index++)
        {
            Popup popup = popups[index];
            popup.Renderer.sortingOrder = 900 + index;
            popup.Renderer.color = new Color(1, 1, 1, (float)popup.State.Alpha);
            Place(popup.Renderer, popup.State.X, popup.State.Y, popup.Renderer.sprite.rect.width * popup.Scale,
                popup.Renderer.sprite.rect.height * popup.Scale);
        }
        int score = Player.playAsEnemy ? song.playerTwoStats.currentScore : song.playerOneStats.currentScore;
        if (ScoreText == null || displayedAutoplay != Player.demoMode || !Player.demoMode && displayedScore != score)
        {
            displayedScore = score;
            displayedAutoplay = Player.demoMode;
            BuildScore(Player.demoMode ? "Bot Play Enabled" : "Score: " + score.ToString("N0", CultureInfo.InvariantCulture));
        }
        foreach (Letter letter in letters)
            if (letter.Renderer.enabled)
                Place(letter.Renderer, x + 601 - 190 + letter.X, y + 30 + letter.Y,
                    letter.Renderer.sprite.rect.width, letter.Renderer.sprite.rect.height);
    }

    private void BuildScore(string text)
    {
        ScoreText = text;
        float cursor = FunkinHudAssets.FontOriginX;
        int used = 0;
        foreach (char character in text)
        {
            var glyph = FunkinHudAssets.FontGlyph(character);
            if (glyph == null) continue;
            if (glyph.Sprite != null)
            {
                for (int outlineY = -1; outlineY <= 1; outlineY++)
                    for (int outlineX = -1; outlineX <= 1; outlineX++)
                    {
                        bool center = outlineX == 0 && outlineY == 0;
                        if (used == letters.Count) letters.Add(new Letter { Renderer = CreateRenderer("Score Glyph", 803) });
                        Letter letter = letters[used++];
                        var sprite = letter.Renderer;
                        sprite.sortingOrder = center ? 804 : 803;
                        sprite.enabled = true;
                        sprite.sprite = glyph.Sprite;
                        sprite.color = center ? Color.white : Color.black;
                        letter.X = cursor + glyph.X + outlineX;
                        letter.Y = glyph.Y + outlineY;
                    }
            }
            cursor += glyph.Advance - 1;
        }
        for (int index = used; index < letters.Count; index++) letters[index].Renderer.enabled = false;
    }
}
