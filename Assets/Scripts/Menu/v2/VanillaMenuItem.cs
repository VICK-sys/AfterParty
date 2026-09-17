using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public sealed class VanillaMenuItem : MonoBehaviour
{
    [Serializable]
    public struct Frame
    {
        public Rect uv;
        public Vector2 size;
        public Vector2 offset;
    }

    public RawImage image;
    public Frame[] idle;
    public Frame[] selected;
    public int index;
    private bool isSelected;
    private float elapsed;

    public void Select(bool value)
    {
        isSelected = value;
        elapsed = 0;
    }

    public void Draw(float deltaTime, float cameraScroll)
    {
        elapsed += deltaTime;
        Frame[] frames = isSelected ? selected : idle;
        if (frames == null || frames.Length == 0)
            return;
        Frame frame = frames[Mathf.FloorToInt(elapsed * 24) % frames.Length];
        image.uvRect = frame.uv;
        image.rectTransform.sizeDelta = frame.size;
        image.rectTransform.anchoredPosition = new Vector2(frame.offset.x,
            320 - 160 * index + cameraScroll * 0.4f + frame.offset.y);
    }
}
