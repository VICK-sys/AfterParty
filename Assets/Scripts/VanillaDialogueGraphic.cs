using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class VanillaDialogueGraphic : MaskableGraphic
{
    public Vector2 Size { get; private set; }
    public float DrawScale = 1;
    public bool Finished { get; private set; }
    private JObject data;
    private JToken clip;
    private Texture2D atlas;
    private float age;
    public override Texture mainTexture => atlas;

    public void Load(string directory)
    {
        if (atlas != null) Destroy(atlas);
        data = JObject.Parse(File.ReadAllText(Path.Combine(directory, "graphic.json")));
        Size = new Vector2((float)data["bounds"][2], (float)data["bounds"][3]);
        string image = (string)data["frames"].SelectMany(frame => frame).First()["image"];
        atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        atlas.LoadImage(File.ReadAllBytes(Path.Combine(directory, image)));
        atlas.filterMode = FilterMode.Point;
        atlas.wrapMode = TextureWrapMode.Clamp;
        raycastTarget = false;
        Play(((JObject)data["animations"]).Properties().First().Name);
        SetMaterialDirty();
    }

    public void Play(string animation)
    {
        clip = data["animations"][animation] ?? data["animations"]["idle"] ?? ((JObject)data["animations"]).Properties().First().Value;
        age = 0;
        Finished = false;
        SetVerticesDirty();
    }

    private void Update()
    {
        if (clip == null) return;
        age += Time.deltaTime;
        Finished = age * (float)clip["fps"] >= clip["frames"].Count();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (clip == null) return;
        int count = clip["frames"].Count();
        int index = (int)(age * (float)clip["fps"]);
        index = (bool)clip["loop"] ? index % count : Mathf.Min(index, count - 1);
        foreach (JToken quad in data["frames"][(int)clip["frames"][index]])
        {
            var rect = quad["rect"];
            float x = (float)rect[0] / atlas.width;
            float y = 1 - (float)rect[1] / atlas.height;
            float w = (float)rect[2] / atlas.width;
            float h = (float)rect[3] / atlas.height;
            Vector2[] uv = { new Vector2(x,y), new Vector2(x+w,y), new Vector2(x+w,y-h), new Vector2(x,y-h) };
            int first = mesh.currentVertCount;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 position = new Vector3((float)quad["xy"][corner * 2] - (float)clip["offset"][0],
                    -(float)quad["xy"][corner * 2 + 1] + (float)clip["offset"][1]) * DrawScale;
                mesh.AddVert(position, color, uv[(corner + ((bool)quad["rotated"] ? 1 : 0)) % 4]);
            }
            mesh.AddTriangle(first,first+1,first+2);
            mesh.AddTriangle(first+2,first+3,first);
        }
    }

    protected override void OnDestroy()
    {
        if (atlas != null) Destroy(atlas);
        base.OnDestroy();
    }
}
