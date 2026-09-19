using System;
using UnityEngine;

[DefaultExecutionOrder(200)]
public class NoteObject : MonoBehaviour
{
    private float scrollSpeed;
    private SpriteRenderer sprite;
    private Song song;
    private FunkinHoldMesh hold;
    public float strumTime;
    public bool mustHit;
    public bool susNote;
    public int type;
    public bool dummyNote = true;
    public bool lastSusNote;
    public int layer;
    public bool isAlt;
    public float currentStrumTime;
    public float currentStopwatch;
    public float susLength;
    public FunkinNoteState State { get; private set; }
    public float ScrollSpeed { get => scrollSpeed; set => scrollSpeed = value; }

    public void Initialize(Song owner, double time, int direction, bool player, double length, float speed, int section)
    {
        song = owner;
        strumTime = (float)time;
        type = direction;
        mustHit = player;
        susLength = (float)length;
        scrollSpeed = -speed;
        layer = section;
        dummyNote = susNote = lastSusNote = false;
        State = new FunkinNoteState(time, direction, length) { View = this, Scoreable = owner.vanillaPlayback?.IsScoreable(player ? 0 : 1, direction, time) ?? true };
        sprite = GetComponentInChildren<SpriteRenderer>();
        sprite.enabled = true;
        sprite.sprite = FunkinNoteSkin.Head(direction);
        sprite.sharedMaterial = FunkinNoteSkin.NoteMaterial;
        sprite.drawMode = SpriteDrawMode.Simple;
        sprite.flipX = sprite.flipY = false;
        sprite.color = Color.white;
        sprite.sortingOrder = 30;
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;
        sprite.transform.localPosition = Vector3.zero;
        sprite.transform.localRotation = Quaternion.identity;
        if (length > 0 && hold == null)
        {
            var child = new GameObject("Sustain");
            child.layer = gameObject.layer;
            child.transform.SetParent(transform, false);
            hold = child.AddComponent<FunkinHoldMesh>();
        }
        if (hold != null) hold.gameObject.SetActive(length > 0);
        Render();
    }

    private void LateUpdate()
    {
        if (Pause.instance != null && (Pause.instance.IsPaused || Pause.instance.Transitioning)) return;
        if (!dummyNote && song != null && State != null) Render();
    }

    private void Render()
    {
        double position = song.SongPosition - Player.visualOffset;
        float pixel = song.FunkinWorldPixelSize;
        var receptor = (mustHit ? song.player1NoteSprites : song.player2NoteSprites)[type];
        Vector3 center = receptor.transform.position;
        double distance = FunkinRules.NoteDistance(State.Time, position, song.FunkinScrollSpeed, OptionsV2.Downscroll);
        transform.position = center + new Vector3(-2 * pixel, (float)distance * pixel, 0);
        FunkinNoteSkin.WorldScale(sprite.transform, pixel * 100 * FunkinNoteSkin.Scale);
        sprite.enabled = receptor.enabled && State.HeadVisible;
        if (State.Hit && State.HeadVisible)
        {
            sprite.sharedMaterial = FunkinNoteSkin.DesaturatedMaterial;
            sprite.color = new Color(1, 1, 1, 0.5f);
        }
        float bottom = song.uiCamera.transform.position.y - song.uiCamera.orthographicSize;
        float top = song.uiCamera.transform.position.y + song.uiCamera.orthographicSize;
        bool headOffscreen = position > State.Time && (OptionsV2.Downscroll ? sprite.bounds.max.y < bottom : sprite.bounds.min.y > top);
        if (headOffscreen && (State.Hit || State.Missed)) sprite.enabled = false;
        if (hold != null && hold.gameObject.activeSelf)
        {
            bool clipped = State.Hit && !State.HoldDropped && position > State.Time;
            double remaining = State.HoldDropped || clipped ? State.Remaining : State.Length;
            Vector3 anchor = clipped ? center : center + new Vector3(0, (float)distance * pixel, 0);
            if (State.HoldDropped && State.Hit)
                anchor.y += (float)((State.Length - State.Remaining) * FunkinRules.PixelsPerMillisecond * song.FunkinScrollSpeed * (OptionsV2.Downscroll ? 1 : -1)) * pixel;
            hold.Draw(type, remaining, song.FunkinScrollSpeed, OptionsV2.Downscroll, anchor, pixel,
                receptor.enabled && !State.HoldFinished && (!State.HoldDropped || !State.Hit) && (!clipped || remaining > 10));
        }
        bool holdDone = State.Length <= 0 || State.HoldFinished ||
            (State.HoldDropped && position >= State.Time + State.Length + FunkinRules.HitWindow + song.FunkinRenderDistance / 8);
        if ((State.Hit && !State.HeadVisible || headOffscreen && (State.Hit || State.HandledMiss)) && holdDone)
            song.ReleaseFunkinNote(this);
    }
}
