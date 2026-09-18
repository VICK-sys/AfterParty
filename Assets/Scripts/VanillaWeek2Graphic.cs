using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class VanillaWeek2Graphic : MonoBehaviour
{
    public Vector2 Size { get; private set; }
    public string Animation { get; private set; }
    public bool Finished { get; private set; }
    public int Frame { get; private set; }
    public float Alpha { get; set; } = 1;
    public Vector3 Position { get; set; }
    public Vector3 GlobalOffset { get; set; }
    public Vector2 Scroll { get; set; } = Vector2.one;
    public float Rain { get; set; }
    public bool CompositeAlpha { get; set; }
    public bool FlipX { get; set; }
    public bool PhillyColor { get; set; }
    public Color Tint { get; set; } = Color.white;
    public float BuildingFade { get; set; }
    public Vector4 ColorAdjustment { get; set; }
    public Vector3 Wiggle { get; set; }
    public int FrozenFrame { get; set; } = -1;
    public bool Additive { get; set; }
    private JObject data;
    private JToken clip;
    private MeshFilter filter;
    private MeshRenderer meshRenderer;
    private Material material;
    private Texture2D texture;
    private Texture2D rimMask;
    private Vector2 origin;
    private Rect compositeBounds;
    private float age;
    private RenderTexture composite;
    private Mesh compositeMesh;
    private Material compositeMaterial;
    private int compositeFrame = -1;
    private readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();

    public void Load(string directory, int order)
    {
        data = JObject.Parse(File.ReadAllText(Path.Combine(directory, "graphic.json")));
        JToken bounds = data["bounds"];
        origin = new Vector2((float)bounds[0], (float)bounds[1]);
        Size = new Vector2((float)bounds[2], (float)bounds[3]);
        compositeBounds = new Rect(0, -Size.y / 100, Size.x / 100, Size.y / 100);
        if ((bool?)data["scaledOffsets"] == true)
            foreach (JToken quad in data["frames"].SelectMany(frame => frame))
                for (int corner = 0; corner < 4; corner++)
                {
                    float x = ((float)quad["xy"][corner * 2] - origin.x) / 100;
                    float y = -((float)quad["xy"][corner * 2 + 1] - origin.y) / 100;
                    compositeBounds = Rect.MinMaxRect(Mathf.Min(compositeBounds.xMin, x), Mathf.Min(compositeBounds.yMin, y),
                        Mathf.Max(compositeBounds.xMax, x), Mathf.Max(compositeBounds.yMax, y));
                }
        string[] images = data["frames"].SelectMany(frame => frame).Select(quad => (string)quad["image"]).Distinct().ToArray();
        if (images.Length != 1) throw new InvalidDataException("Source graphic requires one atlas: " + directory);
        texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(File.ReadAllBytes(Path.Combine(directory, images[0])));
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = (bool?)data["pixel"] == true ? FilterMode.Point : FilterMode.Bilinear;
        material = new Material(Resources.Load<Shader>("VanillaSongs/Week2Graphic"));
        material.mainTexture = texture;
        filter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.sortingOrder = order;
        Play(((JObject)data["animations"]).Properties().First().Name);
    }

    public bool Has(string name) => data["animations"][name] != null;

    public void SetRim(string path, float distance, float threshold, Vector4 adjustment)
    {
        rimMask = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        rimMask.LoadImage(File.ReadAllBytes(path));
        rimMask.filterMode = FilterMode.Point;
        rimMask.wrapMode = TextureWrapMode.Clamp;
        material.SetTexture("_RimMask", rimMask);
        material.SetVector("_Rim", new Vector4(distance, threshold, 1, 1));
        material.SetVector("_RimAdjustment", adjustment);
    }

    public bool Play(string name)
    {
        if (!Has(name)) return false;
        Animation = name;
        clip = data["animations"][name];
        age = 0;
        Finished = false;
        SetFrame((int)clip["frames"][0]);
        return true;
    }

    public void Advance(float delta, Vector3 camera, float clock)
    {
        age += delta;
        int index = Mathf.FloorToInt(age * (float)clip["fps"]);
        int count = clip["frames"].Count();
        Finished = !(bool)clip["loop"] && index >= count;
        index = (bool)clip["loop"] ? index % count : Mathf.Min(index, count - 1);
        SetFrame(FrozenFrame >= 0 ? FrozenFrame : (int)clip["frames"][index]);
        Vector3 scrollOrigin = camera - new Vector3(6.4f, -3.6f, camera.z);
        Vector3 offset = GlobalOffset + new Vector3(-(float)clip["offset"][0] / 100, (float)clip["offset"][1] / 100, 0);
        if ((bool?)data["scaledOffsets"] == true)
            offset = Vector3.Scale(offset, new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, 1));
        transform.localPosition = Position + offset + new Vector3(scrollOrigin.x * (1 - Scroll.x), scrollOrigin.y * (1 - Scroll.y), 0);
        if (FlipX)
        {
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, 1);
            transform.localPosition += Vector3.right * Size.x / 100 * Mathf.Abs(transform.localScale.x);
        }
        bool grouped = PhillyColor || ColorAdjustment != Vector4.zero || CompositeAlpha && Alpha > 0 && Alpha < 1;
        material.SetFloat("_Opacity", grouped ? 1 : Alpha);
        material.SetFloat("_DstBlend", (float)(Additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        material.SetColor("_Tint", Tint);
        material.SetFloat("_BuildingFade", BuildingFade);
        material.SetFloat("_Rain", Rain);
        material.SetFloat("_Clock", clock + 1);
        material.SetVector("_Wiggle", Wiggle);
        if (grouped) RenderComposite();
        else
        {
            filter.sharedMesh = meshes[Frame];
            meshRenderer.sharedMaterial = material;
        }
    }

    private void SetFrame(int index)
    {
        Frame = index;
        JToken firstQuad = data["frames"][index].FirstOrDefault();
        if (firstQuad != null)
        {
            JToken rect = firstQuad["rect"];
            material.SetVector("_FrameBounds", new Vector4((float)rect[0] / texture.width, 1 - (float)rect[1] / texture.height,
                (float)rect[2] / texture.width, -(float)rect[3] / texture.height));
        }
        if (!meshes.TryGetValue(index, out Mesh mesh))
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var colors = new List<Color>();
            var additions = new List<Vector4>();
            var triangles = new List<int>();
            foreach (JToken quad in data["frames"][index])
            {
                JToken rect = quad["rect"];
                float x = (float)rect[0] / texture.width;
                float y = 1 - (float)rect[1] / texture.height;
                float w = (float)rect[2] / texture.width;
                float h = (float)rect[3] / texture.height;
                Vector2[] coords = { new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y - h), new Vector2(x, y - h) };
                int first = vertices.Count;
                for (int corner = 0; corner < 4; corner++)
                {
                    vertices.Add(new Vector3(((float)quad["xy"][corner * 2] - origin.x) / 100,
                        -((float)quad["xy"][corner * 2 + 1] - origin.y) / 100, 0));
                    uv.Add(coords[(corner + ((bool)quad["rotated"] ? 1 : 0)) % 4]);
                    colors.Add(quad["tint"] == null ? new Color(1, 1, 1, (float?)quad["alpha"] ?? 1)
                        : new Color((float)quad["tint"][0], (float)quad["tint"][1], (float)quad["tint"][2], (float)quad["tint"][3]));
                    additions.Add(quad["add"] == null ? Vector4.zero
                        : new Vector4((float)quad["add"][0], (float)quad["add"][1], (float)quad["add"][2], (float)quad["add"][3]));
                }
                triangles.AddRange(new[] { first, first + 1, first + 2, first + 2, first + 3, first });
            }
            mesh = new Mesh { name = name + " " + index };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colors);
            mesh.SetUVs(1, additions);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            meshes[index] = mesh;
        }
        filter.sharedMesh = mesh;
    }

    private void RenderComposite()
    {
        if (composite == null)
        {
            composite = new RenderTexture(Mathf.CeilToInt(compositeBounds.width * 100), Mathf.CeilToInt(compositeBounds.height * 100), 0, RenderTextureFormat.ARGB32);
            composite.filterMode = texture.filterMode;
            composite.Create();
            compositeMaterial = new Material(Resources.Load<Shader>("VanillaSongs/Week2Composite"));
            compositeMaterial.mainTexture = composite;
            compositeMesh = new Mesh
            {
                vertices = new[] { new Vector3(compositeBounds.xMin, compositeBounds.yMax), new Vector3(compositeBounds.xMax, compositeBounds.yMax),
                    new Vector3(compositeBounds.xMax, compositeBounds.yMin), new Vector3(compositeBounds.xMin, compositeBounds.yMin) },
                uv = new[] { new Vector2(0, 1), Vector2.one, new Vector2(1, 0), Vector2.zero },
                triangles = new[] { 0, 1, 2, 2, 3, 0 }
            };
        }
        if (compositeFrame != Frame)
        {
            using (var commands = new CommandBuffer { name = "Week 2 character opacity" })
            {
                commands.SetRenderTarget(composite);
                commands.ClearRenderTarget(false, true, Color.clear);
                Matrix4x4 projection = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(compositeBounds.xMin, compositeBounds.xMax,
                    compositeBounds.yMin, compositeBounds.yMax, -1, 1), true);
                commands.SetViewProjectionMatrices(Matrix4x4.identity, projection);
                commands.DrawMesh(meshes[Frame], Matrix4x4.identity, material);
                Graphics.ExecuteCommandBuffer(commands);
            }
            compositeFrame = Frame;
        }
        compositeMaterial.SetFloat("_Opacity", Alpha);
        compositeMaterial.SetFloat("_PhillyColor", PhillyColor ? 1 : 0);
        compositeMaterial.SetVector("_Adjustment", ColorAdjustment);
        filter.sharedMesh = compositeMesh;
        meshRenderer.sharedMaterial = compositeMaterial;
    }

    private void OnDestroy()
    {
        foreach (Mesh mesh in meshes.Values) Release(mesh);
        Release(texture);
        Release(rimMask);
        Release(material);
        Release(composite);
        Release(compositeMesh);
        Release(compositeMaterial);
    }

    private static void Release(UnityEngine.Object item)
    {
        if (item == null) return;
        if (Application.isPlaying) Destroy(item);
        else DestroyImmediate(item);
    }
}
