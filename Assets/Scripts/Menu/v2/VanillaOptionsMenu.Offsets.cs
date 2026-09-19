using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public sealed partial class VanillaOptionsMenu
{
    private sealed class TestNote
    {
        public double beat;
        public int direction;
        public Image image;
    }

    private readonly List<double> differences = new List<double>();
    private readonly List<TestNote> testNotes = new List<TestNote>();
    private readonly List<(int direction, double time)> testPresses = new List<(int,double)>();
    private readonly List<(RawImage image, double beat)> calibrationArrows = new List<(RawImage,double)>();
    private readonly Image[] testReceptors = new Image[4];
    private readonly float[] receptorAges = new float[4];
    private readonly bool[] receptorHits = new bool[4];
    private Image offsetShade;
    private RawImage latencyReceptor;
    private Text jumpText, countText;
    private float pageAge, offsetBlend, scaleModifier=1, appliedOffset, fromOffset, offsetAge;
    private int offsetMode, savedOffset, temporaryOffset, nextDirection;
    private bool calibrating, gotMad;
    private double nextArrowBeat, previousMusicTime;
    public bool Calibrating => offsetMode == 1 && calibrating;
    public bool TestingOffset => offsetMode == 1 && !calibrating;
    public int CalibrationCount => differences.Count;
    public double CalibrationAverage => differences.Count == 0 ? 0 : differences.Average();
    public double CalibrationConsistency => differences.Count == 0 ? 0 : Math.Sqrt(differences.Average(v => Math.Pow(v-CalibrationAverage,2)));
    public int TemporaryOffset => temporaryOffset;
    public int TestNoteCount => testNotes.Count;

    private IEnumerator FadeMusic(float to, float seconds)
    {
        float from = menu.musicSource.volume;
        for (float elapsed=0;elapsed<seconds;elapsed+=Time.unscaledDeltaTime)
        {
            menu.musicSource.volume = Mathf.Lerp(from,to,elapsed/seconds);
            yield return null;
        }
        menu.musicSource.volume = to;
    }

    private IEnumerator EnterOffsets()
    {
        Busy = true;
        yield return FadeMusic(0,.5f);
        menu.musicSource.Stop();
        menu.musicSource.clip = Resources.Load<AudioClip>("VanillaOptions/offsetsLoop");
        menu.musicSource.loop = true;
        double time = AudioSettings.dspTime+.05;
        menu.musicSource.PlayScheduled(time);
        drums.timeSamples = 0;
        drums.volume = 0;
        drums.PlayScheduled(time);
        Busy = false;
        ShowPage(Page.Offsets);
        yield return FadeMusic(OptionsV2.menuVolume,1);
    }

    private IEnumerator LeaveOffsets()
    {
        Busy = true;
        InputSystem.onEvent -= CaptureOffsetInput;
        yield return FadeMusic(0,.5f);
        drums.Stop();
        menu.musicSource.Stop();
        menu.musicSource.clip = menu.menuClip;
        menu.musicSource.Play();
        Busy = false;
        ShowPage(Page.Options);
        yield return FadeMusic(OptionsV2.menuVolume,.5f);
    }

    private void BuildOffsets()
    {
        pageAge = offsetBlend = 0;
        offsetMode = 0;
        previousMusicTime = 0;
        testNotes.Clear(); calibrationArrows.Clear();
        offsetShade = Rect("Offset Shade",content,-25,-25,1330,770).gameObject.AddComponent<Image>();
        offsetShade.color = Color.clear;
        latencyReceptor = OffsetImage("latencyReceptor",content);
        latencyReceptor.color = Color.clear;
        latencyReceptor.rectTransform.pivot = new Vector2(.5f,.5f);
        latencyReceptor.rectTransform.anchoredPosition = new Vector2(640,-360);
        jumpText = Vcr("",content,20,100,1240,160);
        countText = Vcr("",content,20,600,1240,80);
        for (int i=0;i<4;i++)
        {
            var image = Rect("Test Receptor "+i,content,0,0,0,0).gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.rectTransform.pivot = new Vector2(.5f,.5f);
            image.sprite = FunkinNoteSkin.Receptor(i,FunkinStrumline.Animation.Static,0);
            image.SetNativeSize();
            image.rectTransform.localScale = Vector3.one*.7f;
            image.color = Color.clear;
            testReceptors[i] = image;
        }
        var offset = new Row { text=Atlas("Offset (Global)",content,0,0), value=Atlas(Pause.GlobalOffset.ToString(),content,0,0,false) };
        rows.Add(offset);
        rows.Add(new Row { text=Atlas("Reset Offset",content,0,0), action=()=>SetOffset(0) });
        rows.Add(new Row { text=Atlas("Offset Calibration",content,0,0), action=()=>BeginCalibration(true) });
        rows.Add(new Row { text=Atlas("Test",content,0,0), action=()=>BeginCalibration(false) });
        InputSystem.onEvent -= CaptureOffsetInput;
        InputSystem.onEvent += CaptureOffsetInput;
    }

    private static RawImage OffsetImage(string name,Transform parent)
    {
        var image = Rect(name,parent,0,0,0,0).gameObject.AddComponent<RawImage>();
        image.texture = Resources.Load<Texture2D>("VanillaOptions/"+name);
        image.SetNativeSize();
        image.raycastTarget = false;
        return image;
    }

    public void SetOffset(int value)
    {
        PlayerPrefs.SetInt("Funkin.GlobalOffset",Mathf.Clamp(value,-1500,1500));
        PlayerPrefs.Save();
        if (CurrentPage == Page.Offsets && rows.Count > 0) rows[0].value.SetText(Pause.GlobalOffset.ToString());
    }

    public void BeginCalibration(bool calibrate)
    {
        if (offsetMode != 0 || CurrentPage != Page.Offsets) return;
        calibrating = calibrate;
        savedOffset = Pause.GlobalOffset;
        temporaryOffset = 0;
        appliedOffset = fromOffset = offsetAge = 0;
        offsetMode = 1;
        differences.Clear(); testPresses.Clear();
        foreach (TestNote note in testNotes) Destroy(note.image.gameObject);
        testNotes.Clear();
        gotMad = false;
        nextDirection = 0;
        double beat = menu.musicSource.time/.6;
        nextArrowBeat = calibrate ? Math.Floor(beat)+4 : Math.Floor(beat/4)*4+8;
        drums.timeSamples = Math.Min(menu.musicSource.timeSamples,drums.clip.samples-1);
        if (!drums.isPlaying) drums.Play();
        jumpText.text = calibrate ? "Press any key to the beat!" : "Hit the notes as they come in!";
        jumpText.rectTransform.anchoredPosition = new Vector2(20,calibrate?-100:OptionsV2.Downscroll?-295:-350);
        countText.text = "Current Offset: 0ms";
        openedFrame = Time.frameCount;
    }

    public bool AddCalibrationTap(double milliseconds)
    {
        if (!Calibrating) return false;
        if (differences.Count > 8 && CalibrationConsistency > 40)
        {
            jumpText.text = "Try to be a little more consistent with your timing!";
            differences.Clear();
            temporaryOffset = 0;
            appliedOffset = 0;
            gotMad = true;
            return false;
        }
        differences.Add(milliseconds);
        if (differences.Count%4 == 0)
        {
            temporaryOffset = (int)CalibrationAverage;
            fromOffset = appliedOffset;
            offsetAge = 0;
        }
        if (differences.Count >= 30)
        {
            jumpText.text = "Calibration complete!";
            SetOffset(temporaryOffset);
            ExitCalibration(false);
            return true;
        }
        if (!gotMad) jumpText.text = (Math.Abs(milliseconds-temporaryOffset)<45 ? "Great job" : "Nice job")+(differences.Count<8?", keep going!":"!");
        jumpText.text += "\n"+differences.Count+"/30";
        gotMad = false;
        scaleModifier = .75f;
        return false;
    }

    public void ExitCalibration(bool cancel)
    {
        if (offsetMode != 1) return;
        if (cancel && calibrating) SetOffset(savedOffset);
        offsetMode = -1;
        testPresses.Clear();
        Play(cancel ? cancelSound : confirmSound);
    }

    private static float Cube(float t) => t<.5f ? 4*t*t*t : 1-Mathf.Pow(-2*t+2,3)/2;

    private void TickOffsets(float delta)
    {
        pageAge = Mathf.Min(1,pageAge+delta/2);
        offsetBlend = Mathf.Clamp01(offsetBlend+delta*(offsetMode==1?.5f:offsetMode==-1?-1f/3:0));
        if (offsetMode == -1 && offsetBlend == 0) { offsetMode=0; calibrating=false; openedFrame=Time.frameCount; }
        float blend = Cube(offsetBlend);
        offsetShade.color = new Color(0,0,0,.5f*Cube(pageAge));
        drums.volume = Mathf.MoveTowards(drums.volume,offsetMode==1?OptionsV2.menuVolume:0,delta*OptionsV2.menuVolume);
        float y = Mathf.Lerp(-480,100,Cube(pageAge));
        for (int i=0;i<rows.Count;i++)
        {
            Row row = rows[i];
            float width = row.text.TextWidth+(row.value == null?0:row.value.TextWidth+20);
            float x = 1280*blend+(1280-width)/2;
            if (row.value != null)
            {
                row.value.rectTransform.anchoredPosition = new Vector2(x,-y-i*120-30);
                x += row.value.TextWidth+20;
            }
            row.text.rectTransform.anchoredPosition = new Vector2(x,-y-i*120-30);
        }
        jumpText.color = new Color(1,1,1,blend);
        countText.color = latencyReceptor.color = new Color(1,1,1,calibrating?blend:0);
        scaleModifier = Mathf.Min(1,scaleModifier+delta/2);
        latencyReceptor.rectTransform.localScale = Vector3.one*blend*scaleModifier;
        for (int i=0;i<4;i++)
        {
            float receptorY = OptionsV2.Downscroll?570:70;
            var image = testReceptors[i];
            image.rectTransform.anchoredPosition = new Vector2(472+i*112,-receptorY);
            image.color = new Color(1,1,1,calibrating?0:blend);
            receptorAges[i] += delta;
            bool held = VanillaControls.Held(VanillaControls.Bindings[i].id);
            image.sprite = FunkinNoteSkin.Receptor(i,receptorHits[i] && receptorAges[i]<.15f ? FunkinStrumline.Animation.Confirm : held ? FunkinStrumline.Animation.Press : FunkinStrumline.Animation.Static,receptorAges[i]);
            image.SetNativeSize();
        }
        double time = menu.musicSource.time*1000.0;
        if (time < previousMusicTime-50)
        {
            double shift = (time-previousMusicTime)/600;
            foreach (TestNote note in testNotes) Destroy(note.image.gameObject);
            testNotes.Clear();
            foreach (var arrow in calibrationArrows) Destroy(arrow.image.gameObject);
            calibrationArrows.Clear();
            nextArrowBeat = calibrating ? Math.Floor(time/600)+4 : 4;
        }
        previousMusicTime = time;
        if (offsetMode == 1 && Time.frameCount != openedFrame)
        {
            if (VanillaControls.Pressed("BACK")) { ExitCalibration(true); return; }
            if (calibrating)
            {
                offsetAge = Mathf.Min(1,offsetAge+delta*2);
                appliedOffset = Mathf.Lerp(fromOffset,temporaryOffset,offsetAge);
                countText.text = "Current Offset: "+(int)appliedOffset+"ms";
                if (Input.anyKeyDown) AddCalibrationTap((Math.Floor(time/600+.5)-time/600)*600);
                while (time/600 >= nextArrowBeat-1)
                {
                    nextArrowBeat = Math.Floor(nextArrowBeat/2)*2+2;
                    if (differences.Count >= 8) calibrationArrows.Add((OffsetImage("latencyArrow",content),nextArrowBeat));
                }
            }
            else
            {
                double adjusted = time+Pause.GlobalOffset;
                while (adjusted/600 >= nextArrowBeat-2 && adjusted/600 < 124)
                {
                    nextArrowBeat++;
                    CreateTestNote(nextArrowBeat,nextDirection);
                    if ((int)nextArrowBeat%8 == 0) CreateTestNote(nextArrowBeat,2);
                    nextDirection=(nextDirection+1)%4;
                }
                foreach (var input in testPresses)
                {
                    double position = adjusted-(Time.realtimeSinceStartupAsDouble-input.time)*1000;
                    TestNote note = testNotes.FirstOrDefault(n => n.direction==input.direction && Math.Abs(n.beat*600-position)<=160);
                    if (note == null) continue;
                    int diff = (int)(note.beat*600-position);
                    differences.Add(diff);
                    jumpText.text = (diff==0?"Perfect!\n":diff>0?"Early!\n"+diff+"ms":"Late!\n"+diff+"ms")+"\nAvg: "+(int)CalibrationAverage+"ms";
                    receptorAges[input.direction] = 0;
                    receptorHits[input.direction] = true;
                    Destroy(note.image.gameObject);
                    testNotes.Remove(note);
                }
                testPresses.Clear();
            }
        }
        for (int i=calibrationArrows.Count-1;i>=0;i--)
        {
            var arrow = calibrationArrows[i];
            float diff = (float)(arrow.beat*600-appliedOffset-time);
            arrow.image.rectTransform.anchoredPosition = new Vector2(640-arrow.image.texture.width/2f,-360-diff*.9f+arrow.image.texture.height/2f);
            float alpha = offsetMode==1 ? Mathf.Clamp01((diff+380)/200) : arrow.image.color.a-delta*5;
            arrow.image.color = new Color(1,1,1,alpha);
            if (alpha<=0) { Destroy(arrow.image.gameObject); calibrationArrows.RemoveAt(i); }
        }
        for (int i=testNotes.Count-1;i>=0;i--)
        {
            TestNote note = testNotes[i];
            float diff = (float)(note.beat*600-time-Pause.GlobalOffset);
            note.image.rectTransform.anchoredPosition = new Vector2(472+note.direction*112,-(OptionsV2.Downscroll?570:70)-diff*.45f*(OptionsV2.Downscroll?-1:1));
            note.image.color = new Color(1,1,1,blend*(diff < -160?.3f:1));
            if (diff < -1500 || offsetMode==0) { Destroy(note.image.gameObject); testNotes.RemoveAt(i); }
        }
    }

    private void CreateTestNote(double beat,int direction)
    {
        var image = Rect("Test Note",content,0,0,0,0).gameObject.AddComponent<Image>();
        image.rectTransform.pivot = new Vector2(.5f,.5f);
        image.sprite = FunkinNoteSkin.Head(direction);
        image.SetNativeSize();
        image.rectTransform.localScale = Vector3.one*.7f;
        image.raycastTarget = false;
        testNotes.Add(new TestNote { beat=beat,direction=direction,image=image });
    }

    private void CaptureOffsetInput(InputEventPtr input, InputDevice device)
    {
        if (!TestingOffset || !input.IsA<StateEvent>() && !input.IsA<DeltaStateEvent>()) return;
        for (int i=0;i<4;i++)
        {
            var binding = VanillaControls.Bindings[i];
            if (device is Keyboard keyboard)
                foreach (int code in binding.keys)
                {
                    if (code < 0 || !Player.TryConvertKey((KeyCode)code,out Key key)) continue;
                    var control = keyboard[key];
                    if (!control.isPressed && control.ReadValueFromEvent(input,out float value) && value>=.5f)
                        testPresses.Add((i,input.time));
                }
            if (device is Gamepad pad)
                foreach (int code in binding.buttons)
                {
                    var control = VanillaControls.Button(pad,code);
                    if (control != null && !control.isPressed && control.ReadValueFromEvent(input,out float value) && value>=.5f)
                        testPresses.Add((i,input.time));
                }
        }
    }
}
