using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

[DefaultExecutionOrder(100)]
public sealed class VanillaErectStage : MonoBehaviour
{
    public float CameraZoom { get; private set; }
    public Vector3[] CameraTargets { get; } = new Vector3[3];
    public int PropCount => props.Count;
    private readonly List<Prop> props = new List<Prop>();
    private readonly List<Object> owned = new List<Object>();
    private Song song;
    private float animationTime;

    private sealed class Prop
    {
        public SpriteRenderer renderer;
        public Vector3 position;
        public Vector2 scroll;
        public Sprite[] frames;
        public float frameRate;
    }

    public static VanillaErectStage Create(Song owner)
    {
        var stage = new GameObject("Main Stage Erect").AddComponent<VanillaErectStage>();
        stage.song = owner;
        stage.Load();
        return stage;
    }

    private void Load()
    {
        string directory = Path.Combine(Application.streamingAssetsPath, "Bundles/Stages/mainStageErect");
        JObject data = JObject.Parse(File.ReadAllText(Path.Combine(directory, "stage.json")));
        CameraZoom = (float)data["cameraZoom"];
        foreach (GameObject item in song.defaultSceneObjects) item.SetActive(false);
        foreach (JToken prop in data["props"])
        {
            string name = (string)prop["name"];
            string asset = (string)prop["assetPath"];
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(transform);
            renderer.sortingLayerID = SortingLayer.NameToID("Default");
            renderer.sortingOrder = (int)prop["zIndex"];
            renderer.transform.localScale = new Vector3((float)prop["scale"][0], (float)prop["scale"][1], 1);
            Sprite[] frames;
            if (asset.StartsWith("#"))
            {
                ColorUtility.TryParseHtmlString(asset, out Color color);
                var texture = new Texture2D(1, 1);
                texture.SetPixel(0, 0, color);
                texture.Apply();
                owned.Add(texture);
                frames = new[] { Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0, 1), 100) };
            }
            else
            {
                string image = Path.Combine(directory, Path.GetFileName(asset));
                var texture = new Texture2D(2, 2);
                texture.LoadImage(File.ReadAllBytes(image + ".png"));
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                owned.Add(texture);
                frames = File.Exists(image + ".xml")
                    ? XDocument.Load(image + ".xml").Root.Elements("SubTexture").OrderBy(e => (string)e.Attribute("name"))
                        .Select(e => Frame(texture, e)).ToArray()
                    : new[] { Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0, 1), 100) };
            }
            owned.AddRange(frames);
            renderer.sprite = frames[0];
            renderer.color = new Color(1, 1, 1, (float?)prop["alpha"] ?? 1);
            bool additive = new[] { "brightLightSmall", "orangeLight", "lightgreen", "lightred", "lightAbove" }.Contains(name);
            renderer.sharedMaterial = Material(0, 0, 0, additive);
            props.Add(new Prop { renderer = renderer, frames = frames,
                position = Point(prop["position"]), scroll = new Vector2((float)prop["scroll"][0], (float)prop["scroll"][1]),
                frameRate = (float?)prop["animations"]?.FirstOrDefault()?["frameRate"] ?? 0 });
        }
        SetCharacter(song.boyfriendObject, data["characters"]["bf"], 0, new Vector2(-18, 36), new Vector2(17, 14), 513, -23, 12, 7);
        SetCharacter(song.opponentObject, data["characters"]["dad"], 1, new Vector2(11, 6), new Vector2(13, 3), 841, -33, -32, -23);
        SetCharacter(song.girlfriendObject, data["characters"]["gf"], 2, new Vector2(12, 8), new Vector2(-12, 4), 674, -30, -9, -4);
        song.mainCamera.transform.position = CameraTargets[2];
    }

    private static Sprite Frame(Texture2D texture, XElement frame)
    {
        float width = (float)frame.Attribute("width");
        float height = (float)frame.Attribute("height");
        float frameX = (float?)frame.Attribute("frameX") ?? 0;
        float frameY = (float?)frame.Attribute("frameY") ?? 0;
        return Sprite.Create(texture, new Rect((float)frame.Attribute("x"), texture.height - (float)frame.Attribute("y") - height, width, height),
            new Vector2(frameX / width, 1 - frameY / height), 100);
    }

    private void SetCharacter(GameObject character, JToken data, int index, Vector2 characterCamera, Vector2 offset, float sourceHeight,
        float brightness, float hue, float contrast)
    {
        SpriteRenderer renderer = character.GetComponent<SpriteRenderer>();
        Sprite sprite = renderer.sprite;
        Vector3 feet = Point(data["position"]);
        Vector3 pivot = new Vector3((sprite.pivot.x - sprite.rect.width / 2) / sprite.pixelsPerUnit,
            sprite.pivot.y / sprite.pixelsPerUnit, 0);
        character.transform.parent.localScale = Vector3.one;
        Vector3 globalOffset = new Vector3(offset.x / 100, -offset.y / 100, 0);
        character.transform.parent.position = feet + pivot + globalOffset;
        renderer.sortingLayerID = SortingLayer.NameToID("Default");
        renderer.sortingOrder = (int)data["zIndex"];
        renderer.sharedMaterial = Material(brightness, hue, contrast, false);
        Vector3 camera = feet + globalOffset + new Vector3(0, sourceHeight / 200, -10);
        camera += Point(data["cameraOffsets"]) + new Vector3(characterCamera.x / 100, -characterCamera.y / 100, 0);
        CameraTargets[index] = camera;
    }

    private Material Material(float brightness, float hue, float contrast, bool additive)
    {
        var material = new Material(Resources.Load<Shader>("VanillaSongs/ErectColor"));
        material.SetFloat("_Brightness", brightness / 255);
        material.SetFloat("_Hue", hue * Mathf.Deg2Rad);
        float amount = 1 + contrast / 100;
        if (amount > 1) amount = 1 + 10 * (0.00852259f * Mathf.Exp(4.76454f * (amount - 1)) * 1.01f - 0.0086078159f);
        material.SetFloat("_Contrast", amount);
        material.SetFloat("_DstBlend", additive ? 1 : 10);
        owned.Add(material);
        return material;
    }

    public static Vector3 Point(JToken value)
    {
        return new Vector3((float)value[0] / 100, -(float)value[1] / 100, 0);
    }

    private void LateUpdate()
    {
        if (song == null) return;
        if (!song.songStarted || song.musicSources[0].isPlaying) animationTime += Time.deltaTime;
        Vector3 camera = song.mainCamera.transform.position;
        Vector3 scrollOrigin = camera - new Vector3(6.4f, -3.6f, camera.z);
        foreach (Prop prop in props)
        {
            prop.renderer.transform.position = prop.position + new Vector3(scrollOrigin.x * (1 - prop.scroll.x), scrollOrigin.y * (1 - prop.scroll.y), 0);
            if (prop.frames.Length > 1)
                prop.renderer.sprite = prop.frames[(int)(animationTime * prop.frameRate) % prop.frames.Length];
        }
    }

    private void OnDestroy()
    {
        foreach (Object item in owned) if (item != null) Destroy(item);
    }
}
