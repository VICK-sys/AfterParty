using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public sealed class VanillaStoryProp
{
    public readonly VanillaStorySprite sprite;
    private VanillaStoryPropData data;
    private string signature;
    private bool danced;
    private bool confirming;
    public string Animation { get; private set; }
    public float DanceEvery => data?.danceEvery ?? 0;

    public VanillaStoryProp(VanillaStorySprite graphic)
    {
        sprite = graphic;
    }

    public void Apply(VanillaStoryPropData value, int index)
    {
        sprite.gameObject.SetActive(value != null);
        if (value == null) return;
        string next = JsonConvert.SerializeObject(value);
        if (signature != next)
        {
            data = value;
            signature = next;
            confirming = false;
            sprite.scale = data.scale * (data.isPixel ? 6 : 1);
            sprite.color = new Color(1, 1, 1, data.alpha);
            if (!string.IsNullOrEmpty(data.startingAnimation)) Play(data.startingAnimation);
            else if (data.animations.Length > 0)
            {
                Dance();
                sprite.paused = true;
            }
            else sprite.Load(data.assetPath);
            sprite.mainTexture.filterMode = data.isPixel ? FilterMode.Point : FilterMode.Bilinear;
        }
        sprite.rectTransform.anchoredPosition = new Vector2(data.offsets[0] + 320 * index, -data.offsets[1]);
    }

    public void Step(int step)
    {
        if (data == null || !sprite.gameObject.activeSelf || data.danceEvery <= 0) return;
        if (confirming && !sprite.Finished) return;
        if (Mathf.Abs(step % (data.danceEvery * 4)) < 0.001f) Dance();
    }

    public void Dance()
    {
        confirming = false;
        if (data.animations.Any(animation => animation.name == "danceLeft"))
        {
            Play(danced ? "danceRight" : "danceLeft");
            danced = !danced;
        }
        else Play("idle");
    }

    public void Confirm()
    {
        if (data == null || !sprite.gameObject.activeSelf || !data.animations.Any(animation => animation.name == "confirm")) return;
        Play("confirm");
        confirming = true;
    }

    private void Play(string name)
    {
        VanillaStoryAnimation animation = data.animations.FirstOrDefault(value => value.name == name);
        if (animation == null) return;
        Animation = name;
        sprite.Load(data.assetPath, animation.prefix, animation.frameIndices);
        sprite.fps = animation.frameRate;
        sprite.loop = animation.looped;
        sprite.flipX = data.flipX ^ animation.flipX;
        sprite.flipY = data.flipY ^ animation.flipY;
        sprite.animationOffset = new Vector2(animation.offsets[0], animation.offsets[1]);
    }
}
