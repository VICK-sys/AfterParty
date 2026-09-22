using UnityEngine;

public sealed class VanillaFreeplayDifficultyStars : MonoBehaviour
{
    private readonly VanillaFreeplaySprite[] flames = new VanillaFreeplaySprite[5];
    private readonly float[] flameAges = new float[5];
    private VanillaFreeplayAnimate stars;
    private float age;
    private double lastUpdateTime;
    public int Rating { get; private set; }
    public RectTransform rectTransform => (RectTransform)transform;
    public VanillaFreeplayAnimate Stars => stars;

    public void Initialize()
    {
        for (int i = 0; i < flames.Length; i++)
        {
            var flame = Child("Flame " + i, new Vector2(-37 + 29 * i, 118 - 6 * i)).gameObject.AddComponent<VanillaFreeplaySprite>();
            flame.Load("freeplay/freeplayFlame", "fire loop full instance 1");
            flame.fps = 23 + i % 3;
            flame.FreezeFrame(0);
            flame.gameObject.SetActive(false);
            flames[i] = flame;
        }
        stars = Child("Stars", Vector2.zero).gameObject.AddComponent<VanillaFreeplayAnimate>();
        stars.Initialize("freeplay/freeplayStars", false);
        SetRating(0);
    }

    private RectTransform Child(string title, Vector2 position)
    {
        var rect = new GameObject(title, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position;
        return rect;
    }

    public void SetRating(int value)
    {
        Rating = Mathf.Clamp(value, 0, 15);
        age = 0;
        int pending = 0;
        for (int i = 0; i < flames.Length; i++)
        {
            if (i >= Rating - 10)
            {
                flames[i].gameObject.SetActive(false);
                flameAges[i] = 0;
            }
            else if (!flames[i].gameObject.activeSelf)
                flameAges[i] = -.25f * pending++;
        }
        Tick(0);
    }

    private void OnEnable() => lastUpdateTime = Time.realtimeSinceStartupAsDouble;

    private void Update()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        Tick(VanillaMenuTiming.Clamp((float)(now - lastUpdateTime)));
        lastUpdateTime = now;
    }

    public void Tick(float delta)
    {
        if (stars == null) return;
        age += delta;
        stars.SetFrame(Rating == 0 ? 1500 : (Rating - 1) * 100 + Mathf.FloorToInt(age * stars.FrameRate) % 100);
        for (int i = 0; i < Mathf.Max(0, Rating - 10); i++)
        {
            flameAges[i] += delta;
            if (flameAges[i] < 0) continue;
            var flame = flames[i];
            flame.gameObject.SetActive(true);
            int frame = Mathf.FloorToInt(flameAges[i] * flame.fps);
            if (frame >= flame.FrameCount) frame = 2 + (frame - flame.FrameCount) % (flame.FrameCount - 2);
            flame.FreezeFrame(frame);
        }
    }
}
