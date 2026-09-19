using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public sealed class VanillaFreeplayRankChange
{
    public string songPath;
    public string difficulty;
    public int mode;
    public int oldRank;
    public int newRank;
}

public sealed partial class VanillaFreeplay
{
    private sealed class RankRecoil
    {
        public Capsule capsule;
        public Vector2 origin;
        public Vector2 offset;
        public float angle;
        public float delay;
        public float intensity;
        public int sample = -1;
    }

    private struct RankTrailFrame
    {
        public Vector2 position;
        public float scale;
        public float angle;
    }

    private static VanillaFreeplayRankChange pendingRank;
    private static string rankReturnPath;
    private static string rankReturnDifficulty;
    private static int rankReturnMode;
    private static readonly string[] RankColors = { "6044FF", "EF8764", "EAF6FF", "FDCB42", "FF58B4", "FFB619" };
    private static readonly string[] RankSounds = { "loss", "good", "great", "excellent", "perfect", "perfect" };
    private readonly List<RankRecoil> rankRecoils = new List<RankRecoil>();
    private readonly List<VanillaFreeplaySprite> rankTrails = new List<VanillaFreeplaySprite>();
    private readonly List<RankTrailFrame> rankTrailFrames = new List<RankTrailFrame>();
    private readonly float[] rankTrailAlphas = new float[15];
    private float rankTrailClock;
    private float rankTrailPreviousTime = 1.6f;
    private float rankTrailAlpha = 1;
    private VanillaFreeplayRankChange rankChange;
    private Capsule rankedCapsule;
    private RectTransform rankLayer;
    private RectTransform rankZoom;
    private RectTransform menuContent;
    private Image rankDim;
    private Image rankFade;
    private VanillaFreeplaySprite oldRankBadge;
    private VanillaFreeplaySprite oldRankGlow;
    private VanillaFreeplaySprite rankSparks;
    private VanillaFreeplaySprite rankSparksAdd;
    private VanillaFreeplaySprite rankVignette;
    private Material rankAdditive;
    private float rankElapsed = -1;
    private bool rankRevealed;
    private bool rankHit;
    private bool rankSound;
    private bool rankSlammed;
    private bool rankReleased;
    private bool rankDjReaction;
    private Vector2 rankHitOffset;
    private int rankHitSample = -1;
    public bool RankAnimationPlaying => rankElapsed >= 0 && !rankReleased;
    public float RankAnimationTime => rankElapsed;

    public static void ArmRankReturn(SongMetaV2 meta, string difficulty, int mode)
    {
        pendingRank = null;
        rankReturnPath = Path.GetFullPath(meta.songPath);
        rankReturnDifficulty = difficulty;
        rankReturnMode = mode;
    }

    public static void QueueRankReturn(VanillaFreeplayRankChange change)
    {
        if (change == null || rankReturnPath == null || string.IsNullOrEmpty(change.songPath)) return;
        if (!string.Equals(rankReturnPath, Path.GetFullPath(change.songPath), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(rankReturnDifficulty, change.difficulty, StringComparison.OrdinalIgnoreCase)
            || rankReturnMode != change.mode || change.mode != PlayModes.Boyfriend
            || change.newRank <= change.oldRank || change.newRank < 0 || change.newRank >= Ranks.Length) return;
        pendingRank = change;
        rankReturnPath = null;
    }

    public static void PrepareResultsReturn(SongMetaV2 meta, string difficulty, int mode, string character, VanillaFreeplayRankChange change)
    {
        rememberedSong = Path.GetFullPath(meta.songPath);
        rememberedDifficulty = difficulty;
        rememberedMode = mode;
        hasRememberedSelection = true;
        ReturnToFreeplay = true;
        PlayerPrefs.SetString("Freeplay.Character", character);
        ArmRankReturn(meta, difficulty, mode);
        QueueRankReturn(change);
    }

    private static void ClearRankReturn()
    {
        pendingRank = null;
        rankReturnPath = null;
        rankReturnDifficulty = null;
    }

    private void ConsumeRankReturn(bool returning)
    {
        VanillaFreeplayRankChange change = pendingRank;
        ClearRankReturn();
        if (!returning || change == null || SelectedSong == null
            || !string.Equals(SelectedSong.id, Path.GetFullPath(change.songPath), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Difficulty, change.difficulty, StringComparison.OrdinalIgnoreCase) || Mode != change.mode) return;
        StartRankAnimation(change);
    }

    private Material RankMaterial
    {
        get
        {
            if (rankAdditive == null) rankAdditive = new Material(Resources.Load<Shader>("VanillaFreeplay/RankAdditive"));
            return rankAdditive;
        }
    }

    private VanillaFreeplaySprite RankBadge(string name, RectTransform parent, int rank)
    {
        var badge = Sprite(name, parent, "freeplay/rankbadges", 420, 41, Ranks[Mathf.Clamp(rank, 0, 5)]);
        badge.drawScale = 0.9f;
        badge.rectTransform.anchoredPosition += new Vector2(-badge.FrameSize.x * 0.05f, badge.FrameSize.y * 0.05f);
        badge.material = RankMaterial;
        badge.loop = false;
        return badge;
    }

    private void StartRankAnimation(VanillaFreeplayRankChange change)
    {
        rankChange = change;
        rankElapsed = 0;
        rankedCapsule = capsules[SelectedIndex];
        menuContent = (RectTransform)list.parent;
        CancelPreview();
        rankLayer = Rect("Rank Celebration", viewport, 0, 0, 1280, 720);
        rankDim = Rect("Dim", rankLayer, 0, 0, 1280, 720).gameObject.AddComponent<Image>();
        rankDim.raycastTarget = false;
        rankZoom = Rect("Rank Camera", rankLayer, 0, 0, 1280, 720);
        rankedCapsule.root.SetParent(rankZoom, false);
        rankedCapsule.rank.gameObject.SetActive(false);
        rankedCapsule.rankGlow.gameObject.SetActive(false);
        if (change.oldRank >= 0)
        {
            oldRankBadge = RankBadge("Previous Rank", (RectTransform)rankedCapsule.detail.transform, change.oldRank);
            oldRankGlow = RankBadge("Previous Rank Glow", (RectTransform)rankedCapsule.detail.transform, change.oldRank);
            rankSparks = Sprite("Rank Sparks", rankZoom, "freeplay/sparks", 517, 134, "sparks");
            rankSparksAdd = Sprite("Rank Sparks Color", rankZoom, "freeplay/sparksadd", 498, 116, "sparks add");
            foreach (var spark in new[] { rankSparks, rankSparksAdd })
            {
                spark.drawScale = 0.5f;
                spark.material = RankMaterial;
                spark.loop = false;
                spark.gameObject.SetActive(false);
            }
            rankSparksAdd.color = Hex(RankColors[change.oldRank]);
        }
        rankVignette = Sprite("Rank Vignette", menuContent, "freeplay/rankVignette", 0, 0);
        rankVignette.centerScale = false;
        rankVignette.drawScale = 2;
        rankVignette.material = RankMaterial;
        rankVignette.color = Color.clear;
        rankFade = Rect("Fade In", rankLayer, 0, 0, 1280, 720).gameObject.AddComponent<Image>();
        rankFade.raycastTarget = false;
        dj.PlayRange(change.newRank == 0 ? "Fist Pump Loss" : "Fist Pump", 0, IsPico && change.newRank == 0 ? 1 : 4, true);
        DrawRankAnimation(0);
    }

    private bool RankControlsCapsule(Capsule capsule)
    {
        if (rankElapsed < 0) return false;
        if (capsule == rankedCapsule && !rankReleased) return true;
        foreach (RankRecoil recoil in rankRecoils)
            if (recoil.capsule == capsule && rankElapsed >= 1.6f + recoil.delay && rankElapsed < 2.2f + recoil.delay) return true;
        return false;
    }

    private void DrawRankAnimation(float delta)
    {
        if (rankDjReaction && dj.Finished && !closing)
        {
            rankDjReaction = false;
            dj.Play("Idle", true);
        }
        if (rankElapsed < 0) return;
        if (closing) { FinishRankAnimation(); return; }
        rankElapsed += delta;
        float t = rankElapsed;
        if (!rankRevealed && t >= 0.5f)
        {
            rankRevealed = true;
            foreach (var badge in new[] { rankedCapsule.rank, rankedCapsule.rankGlow })
            {
                badge.gameObject.SetActive(true);
                badge.TryPlay(Ranks[rankChange.newRank], false);
            }
        }
        if (!rankHit && t >= 0.6f)
        {
            rankHit = true;
            if (oldRankBadge != null)
            {
                oldRankBadge.gameObject.SetActive(false);
                oldRankGlow.gameObject.SetActive(false);
                rankSparks.gameObject.SetActive(true);
                rankSparksAdd.gameObject.SetActive(true);
            }
            Sound("ranks/" + (rankChange.newRank == 0 ? "rankinbad" : rankChange.newRank >= 4 ? "rankinperfect" : "rankinnormal"), 1);
        }
        if (!rankSound && t >= 1.1f)
        {
            rankSound = true;
            Sound("ranks/" + RankSounds[rankChange.newRank], 1);
        }
        if (!rankSlammed && t >= 1.6f)
        {
            rankSlammed = true;
            dj.PlayRange(rankChange.newRank == 0 ? "Fist Pump Loss" : "Fist Pump", IsPico && rankChange.newRank == 0 ? 0 : 4, -1, false);
            rankDjReaction = true;
            foreach (Capsule capsule in capsules)
            {
                int distance = Mathf.Abs(capsule.index - SelectedIndex) - 1;
                if (distance >= 5) continue;
                rankRecoils.Add(new RankRecoil
                {
                    capsule = capsule, origin = ToUI(Target(capsule.index)), delay = Mathf.Max(0, distance / 20f),
                    intensity = distance < 0 ? 0.12f : 0.12f / (distance + 1),
                    angle = distance < 0 ? 0 : Random.Range(-10 + distance * 2, 10 - distance * 2)
                });
            }
            for (int i = 0; i < 15; i++)
            {
                var trail = Sprite("Rank Impact " + i, rankedCapsule.root, "freeplay/freeplayCapsule/capsule/freeplayCapsule", 0, 0, "mp3 capsule w backing0");
                trail.rectTransform.SetAsFirstSibling();
                trail.loop = false;
                trail.FreezeFrame(rankedCapsule.body.FrameIndex);
                trail.material = RankMaterial;
                rankTrailAlphas[i] = Mathf.Clamp01(0.01f - i * 0.069f);
                rankTrails.Add(trail);
            }
        }
        float menuScale = t < 0.6f ? Mathf.Lerp(1.15f, 1.1f, SineIn(t / 0.6f))
            : t < 0.9f ? Mathf.LerpUnclamped(1.1f, 1.05f, ElasticOut((t - 0.6f) / 0.3f))
            : t < 1.6f ? Mathf.Lerp(1.05f, 1, SineIn((t - 0.9f) / 0.8f))
            : Mathf.LerpUnclamped(0.8f, 1, ElasticOut((t - 1.6f) / 0.8f));
        float badgeScale = t < 0.6f ? Mathf.Lerp(1.85f, 1.8f, SineIn(t / 0.6f))
            : t < 0.9f ? Mathf.LerpUnclamped(1.3f, 1.5f, BackInOut((t - 0.6f) / 0.3f))
            : t < 1.6f ? Mathf.LerpUnclamped(1.5f, 1.2f, BackIn((t - 0.9f) / 0.8f))
            : Mathf.LerpUnclamped(0.8f, 1, ElasticOut((t - 1.6f) / 1));
        RankCamera(menuContent, menuScale);
        RankCamera(rankZoom, badgeScale);
        if (t >= 1.6f && t < 1.95f)
            menuContent.anchoredPosition += new Vector2(Random.Range(-5.76f, 5.76f), Random.Range(-3.24f, 3.24f));
        rankFade.color = new Color(0, 0, 0, 1 - Mathf.Clamp01(t / 0.5f));
        rankDim.color = new Color(0, 0, 0, 211f / 255 * (1 - ExpoIn((t - 1.1f) / 0.5f)));
        if (t < 1.6f)
        {
            Vector2 center = new Vector2(334, 294);
            Vector2 position = t < 0.6f ? center : center - new Vector2(10, 20);
            if (t >= 0.6f && t < 0.9f)
            {
                int sample = Mathf.FloorToInt((t - 0.6f) * 30);
                if (sample != rankHitSample)
                {
                    rankHitSample = sample;
                    float intensity = 61.2f * Mathf.Pow(1 - Mathf.Clamp01(sample / 9f), 2);
                    rankHitOffset = sample == 0 ? Vector2.zero : new Vector2(Random.Range(-intensity, intensity), Random.Range(-intensity, intensity));
                }
                position += rankHitOffset;
            }
            if (t >= 0.9f) position = Vector2.Lerp(position, new Vector2(313.488f, 155.6f), Mathf.Pow(Mathf.Clamp01((t - 0.9f) / 1.3f), 4));
            rankedCapsule.root.anchoredPosition = ToUI(position);
            SetRankAngle(rankedCapsule, t < 0.6f ? 0 : -3 * (1 - BackOut((t - 0.6f) / 0.5f)));
        }
        if (!rankReleased)
        {
            foreach (var badge in new[] { rankedCapsule.rank, rankedCapsule.rankGlow })
            {
                badge.drawScale = Mathf.Lerp(20, 0.9f, Mathf.Clamp01((t - 0.5f) / 0.1f));
                badge.SetVerticesDirty();
            }
        }
        if (rankSparks != null && t >= 0.6f + rankSparks.FrameCount / 24f)
        {
            rankSparks.gameObject.SetActive(false);
            rankSparksAdd.gameObject.SetActive(false);
        }
        foreach (RankRecoil recoil in rankRecoils)
        {
            float time = t - 1.6f - recoil.delay;
            if (time < 0 || time > 0.6f + delta || recoil.capsule.root == null) continue;
            int sample = Mathf.FloorToInt(time * 24);
            if (sample != recoil.sample)
            {
                recoil.sample = sample;
                float intensity = 612 * recoil.intensity * Mathf.Pow(1 - Mathf.Clamp01(sample / 14f), 2);
                recoil.offset = sample == 0 ? Vector2.zero : new Vector2(Random.Range(-intensity, intensity), Random.Range(-intensity, intensity));
            }
            recoil.capsule.root.anchoredPosition = recoil.origin + (time >= 0.6f ? Vector2.zero : recoil.offset);
            SetRankAngle(recoil.capsule, recoil.angle * (1 - BackOut(time / 0.5f)));
        }
        Color tint = Hex(RankColors[rankChange.newRank]);
        tint.a = t < 1.6f ? 0 : 1 - ExpoOut((t - 1.6f) / 0.6f);
        rankVignette.color = tint;
        DrawRankTrail(t, tint);
        if (!rankReleased && t >= 2.2f)
        {
            rankReleased = true;
            rankedCapsule.root.SetParent(list, false);
            rankedCapsule.root.SetSiblingIndex(SelectedIndex);
            rankedCapsule.root.anchoredPosition = ToUI(Target(SelectedIndex));
            SetRankAngle(rankedCapsule, 0);
            if (oldRankBadge != null) { Destroy(oldRankBadge.gameObject); Destroy(oldRankGlow.gameObject); }
            foreach (var trail in rankTrails) if (trail != null) Destroy(trail.gameObject);
            rankTrails.Clear();
            StartPreview();
        }
        if (t >= 2.6f) FinishRankAnimation();
    }

    private void FinishRankAnimation()
    {
        RankCamera(menuContent, 1);
        foreach (RankRecoil recoil in rankRecoils)
            if (recoil.capsule.root != null) SetRankAngle(recoil.capsule, 0);
        rankRecoils.Clear();
        Destroy(rankLayer.gameObject);
        Destroy(rankVignette.gameObject);
        rankElapsed = -1;
    }

    private void DrawRankTrail(float time, Color tint)
    {
        if (rankTrails.Count == 0) return;
        rankTrailClock += time - rankTrailPreviousTime;
        rankTrailPreviousTime = time;
        bool sample = rankTrailClock >= 0.03f;
        if (sample)
        {
            rankTrailClock = 0;
            rankTrailFrames.Insert(0, new RankTrailFrame
            {
                position = rankedCapsule.root.anchoredPosition,
                scale = Mathf.Lerp(1, 2.5f, Mathf.Clamp01((time - 1.6f) / 0.5f)),
                angle = rankedCapsule.body.angle
            });
            if (rankTrailFrames.Count > 15) rankTrailFrames.RemoveAt(15);
        }
        float alpha = Mathf.Pow(1 - Mathf.Clamp01((time - 1.6f) / 0.6f), 2);
        float factor = rankTrailAlpha > 0 ? alpha / rankTrailAlpha : 0;
        for (int i = 0; i < rankTrails.Count; i++)
        {
            var trail = rankTrails[i];
            if (alpha != rankTrailAlpha)
                rankTrailAlphas[i] = rankTrailAlphas[i] != 0 || factor == 0
                    ? rankTrailAlphas[i] * factor : Mathf.Clamp01(1 / factor);
            if (sample && i < rankTrailFrames.Count)
            {
                RankTrailFrame frame = rankTrailFrames[i];
                trail.rectTransform.anchoredPosition = frame.position - rankedCapsule.root.anchoredPosition;
                trail.drawScale = frame.scale;
                trail.angle = frame.angle;
            }
            tint.a = i < rankTrailFrames.Count ? rankTrailAlphas[i] : 0;
            trail.color = tint;
            trail.SetVerticesDirty();
        }
        rankTrailAlpha = alpha;
    }

    private static void RankCamera(RectTransform root, float zoom)
    {
        root.localScale = Vector3.one * zoom;
        root.anchoredPosition = new Vector2(640 * (1 - zoom), -360 * (1 - zoom));
    }

    private static void SetRankAngle(Capsule capsule, float angle)
    {
        foreach (var sprite in capsule.root.GetComponentsInChildren<VanillaFreeplaySprite>(true))
        {
            sprite.angle = angle;
            sprite.SetVerticesDirty();
        }
    }

    private void DrawRankSparkle(Capsule capsule, float delta)
    {
        if (capsule.sparkle == null || !capsule.sparkle.gameObject.activeSelf) return;
        capsule.sparkleAge += delta;
        if (capsule.sparkleAge >= capsule.sparkleNext)
        {
            capsule.sparkleAge = 0;
            capsule.sparkleNext = Random.Range(1.2f, 4.5f);
            capsule.sparkle.rectTransform.anchoredPosition = ToUI(new Vector2(Random.Range(400f, 423f), Random.Range(12f, 45f)));
            capsule.sparkle.TryPlay("sparkle Export0", false);
        }
        capsule.sparkle.color = new Color(1, 1, 1, RankAnimationPlaying && capsule == rankedCapsule ? 0 : 0.7f);
    }

    private static float SineIn(float t) => 1 - Mathf.Cos(Mathf.Clamp01(t) * Mathf.PI / 2);
    private static float ExpoIn(float t) => t <= 0 ? 0 : t >= 1 ? 1 : Mathf.Pow(2, 10 * (t - 1));
    private static float ExpoOut(float t) => t <= 0 ? 0 : t >= 1 ? 1 : 1 - Mathf.Pow(2, -10 * t);
    private static float BackIn(float t) { t = Mathf.Clamp01(t); return t * t * (2.70158f * t - 1.70158f); }
    private static float BackOut(float t) { t = Mathf.Clamp01(t) - 1; return 1 + t * t * (2.70158f * t + 1.70158f); }
    private static float BackInOut(float t)
    {
        t = Mathf.Clamp01(t) * 2;
        const float s = 1.70158f * 1.525f;
        if (t < 1) return 0.5f * t * t * ((s + 1) * t - s);
        t -= 2;
        return 0.5f * (t * t * ((s + 1) * t + s) + 2);
    }
    private static float ElasticOut(float t)
    {
        if (t <= 0 || t >= 1) return Mathf.Clamp01(t);
        return Mathf.Pow(2, -10 * t) * Mathf.Sin((t - 0.1f) * 2 * Mathf.PI / 0.4f) + 1;
    }
}
