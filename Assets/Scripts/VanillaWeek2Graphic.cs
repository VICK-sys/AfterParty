using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class VanillaWeek2Graphic : MonoBehaviour
{
    private sealed class Clip
    {
        public int[] frames;
        public float fps;
        public bool loop;
        public Vector3 offset;
    }

    private sealed class Asset
    {
        public JObject data;
        public Texture2D texture;
        public int users;
        public readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();
        public readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>(StringComparer.Ordinal);
        public Vector4[] frameBounds;
        public Vector2[] frameSizes;
    }
    private static readonly Dictionary<string, Asset> Assets = new Dictionary<string, Asset>(StringComparer.OrdinalIgnoreCase);
    public static int TextureLoads { get; private set; }
    private Asset asset;
    private string assetKey;
    public Vector2 Size { get; private set; }
    public Vector2 HitboxSize { get; private set; }
    public string Animation { get; private set; }
    public bool Finished { get; private set; }
    public int Frame { get; private set; } = -1;
    public float Alpha { get; set; } = 1;
    public Vector3 Position { get; set; }
    public Vector3 GlobalOffset { get; set; }
    public bool ApplyAnimationOffsets { get; set; } = true;
    public Vector2 Scroll { get; set; } = Vector2.one;
    public float Rain { get; set; }
    public bool CompositeAlpha { get; set; }
    public bool FlipX { get; set; }
    public bool PhillyColor { get; set; }
    public Color Tint { get; set; } = Color.white;
    public float BuildingFade { get; set; }
    public Vector4 ColorAdjustment { get; set; }
    public Color Lighting { get; set; } = Color.white;
    public Matrix4x4 VertexTransform { get; set; } = Matrix4x4.identity;
    public Vector2 BoundsOrigin => origin;
    public JToken Attachments => data["attachments"]?[Frame];
    public Vector3 Wiggle { get; set; }
    public int FrozenFrame { get; set; } = -1;
    public bool Additive { get; set; }
    public bool Multiply { get; set; }
    public int AnimationFrame => clip == null ? 0 : Mathf.Min((int)(age * clip.fps), clip.frames.Length - 1);

    public void SetAnimationFrame(int frame)
    {
        FrozenFrame = clip.frames[Mathf.Clamp(frame, 0, clip.frames.Length - 1)];
    }
    public void SeekAnimationFrame(int frame)
    {
        FrozenFrame = -1;
        age = Mathf.Max(0, frame) / clip.fps;
    }
    public float Angle { get; set; }
    public float Duration => clip == null ? 0 : clip.frames.Length / clip.fps;
    public Vector2 FrameSize => asset.frameSizes == null ? Size : asset.frameSizes[Frame];
    public bool HasRim { get; private set; }
    private float rimAngle = 90;
    private bool compositeRim;
    private Vector4 rimSettings;
    private Vector4 rimAdjustment;
    private Color rimColor;
    private JObject data;
    private Clip clip;
    private bool scaledOffsets;
    private MeshFilter filter;
    private MeshRenderer meshRenderer;
    private Material material;
    private Texture2D texture;
    private Texture2D rimMask;
    private Texture2D preparedRimMask;
    private string preparedRimMaskPath;
    private Texture2D replacementTexture;
    private Vector2 origin;
    private Rect compositeBounds;
    private float age;
    private bool reversed;
    private RenderTexture composite;
    private Mesh compositeMesh;
    private Material compositeMaterial;
    private CommandBuffer compositeCommands;
    private int compositeFrame = -1;
    private Dictionary<int, Mesh> meshes;

    public void Load(string directory, int order)
    {
        SongLoadingDiagnostics.Record("graphic begin: " + directory);
        assetKey = Path.GetFullPath(directory);
        if (!Assets.TryGetValue(assetKey, out asset))
        {
            var parsed = JObject.Parse(File.ReadAllText(Path.Combine(directory, "graphic.json")));
            string[] images = parsed["frames"].SelectMany(frame => frame).Select(quad => (string)quad["image"]).Distinct().ToArray();
            if (images.Length != 1) throw new InvalidDataException("Source graphic requires one atlas: " + directory);
            Texture2D loadedTexture = LoadTexture(Path.Combine(directory, images[0]), (bool?)parsed["pixel"] == true);
            asset = new Asset { data = parsed, texture = loadedTexture };
            foreach (JProperty animation in ((JObject)parsed["animations"]).Properties())
            {
                JToken value = animation.Value;
                asset.clips.Add(animation.Name, new Clip
                {
                    frames = value["frames"].Values<int>().ToArray(),
                    fps = (float)value["fps"],
                    loop = (bool)value["loop"],
                    offset = new Vector3(-(float)value["offset"][0] / 100, (float)value["offset"][1] / 100, 0)
                });
            }
            var sourceFrames = (JArray)parsed["frames"];
            asset.frameBounds = new Vector4[sourceFrames.Count];
            for (int i = 0; i < sourceFrames.Count; i++)
            {
                JToken rect = sourceFrames[i].First?["rect"];
                if (rect == null) continue;
                asset.frameBounds[i] = new Vector4((float)rect[0] / loadedTexture.width, 1 - (float)rect[1] / loadedTexture.height,
                    (float)rect[2] / loadedTexture.width, -(float)rect[3] / loadedTexture.height);
            }
            if (parsed["frameSizes"] is JArray sizes)
                asset.frameSizes = sizes.Select(size => new Vector2((float)size[0], (float)size[1])).ToArray();
            Assets.Add(assetKey, asset);
            TextureLoads++;
        }
        asset.users++;
        data = asset.data;
        texture = asset.texture;
        meshes = asset.meshes;
        JToken bounds = data["bounds"];
        origin = new Vector2((float)bounds[0], (float)bounds[1]);
        Size = new Vector2((float)bounds[2], (float)bounds[3]);
        HitboxSize = data["hitboxSize"] == null ? Size : new Vector2((float)data["hitboxSize"][0], (float)data["hitboxSize"][1]);
        scaledOffsets = (bool?)data["scaledOffsets"] == true;
        compositeBounds = new Rect(0, -Size.y / 100, Size.x / 100, Size.y / 100);
        if (scaledOffsets)
            foreach (JToken quad in data["frames"].SelectMany(frame => frame))
                for (int corner = 0; corner < 4; corner++)
                {
                    float x = ((float)quad["xy"][corner * 2] - origin.x) / 100;
                    float y = -((float)quad["xy"][corner * 2 + 1] - origin.y) / 100;
                    compositeBounds = Rect.MinMaxRect(Mathf.Min(compositeBounds.xMin, x), Mathf.Min(compositeBounds.yMin, y),
                        Mathf.Max(compositeBounds.xMax, x), Mathf.Max(compositeBounds.yMax, y));
                }
        material = new Material(Resources.Load<Shader>("VanillaSongs/Week2Graphic"));
        material.mainTexture = texture;
        material.SetMatrix("_Affine", Matrix4x4.identity);
        material.SetFloat("_AffineEnabled", 1);
        material.SetColor("_Lighting", Color.white);
        filter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.sortingOrder = order;
        Play(((JObject)data["animations"]).Properties().First().Name);
        SongLoadingDiagnostics.Record("graphic ready: " + directory);
    }

    public bool Has(string name) => asset.clips.ContainsKey(name);

    private static Texture2D LoadTexture(string path, bool pixel)
    {
        SongLoadingDiagnostics.Record("texture begin: " + path);
        var loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!loaded.LoadImage(File.ReadAllBytes(path), pixel && !Application.isEditor))
        {
            Release(loaded);
            throw new InvalidDataException("Could not decode texture: " + path);
        }
        loaded.wrapMode = TextureWrapMode.Clamp;
        loaded.filterMode = pixel ? FilterMode.Point : FilterMode.Bilinear;
        if (pixel || loaded.format == TextureFormat.RGB24)
        {
            if (loaded.isReadable && !Application.isEditor) loaded.Apply(false, true);
            SongLoadingDiagnostics.Record("texture ready: " + path);
            return loaded;
        }
        bool argb = loaded.format == TextureFormat.ARGB32;
        if (!argb && loaded.format != TextureFormat.RGBA32 && loaded.format != TextureFormat.BGRA32)
        {
            string format = loaded.format.ToString();
            Release(loaded);
            throw new InvalidDataException("Unsupported atlas pixel format " + format + ": " + path);
        }
        var pixels = loaded.GetPixelData<Color32>(0);
        int width = loaded.width;
        int height = loaded.height;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                Color32 current = pixels[index];
                if ((argb ? current.r : current.a) != 0) continue;
                int red = 0;
                int green = 0;
                int blue = 0;
                int weight = 0;
                for (int row = Mathf.Max(0, y - 1); row <= Mathf.Min(height - 1, y + 1); row++)
                    for (int column = Mathf.Max(0, x - 1); column <= Mathf.Min(width - 1, x + 1); column++)
                    {
                        Color32 neighbor = pixels[row * width + column];
                        int alpha = argb ? neighbor.r : neighbor.a;
                        red += (argb ? neighbor.g : neighbor.r) * alpha;
                        green += (argb ? neighbor.b : neighbor.g) * alpha;
                        blue += (argb ? neighbor.a : neighbor.b) * alpha;
                        weight += alpha;
                    }
                if (weight > 0) pixels[index] = argb
                    ? new Color32(0, (byte)(red / weight), (byte)(green / weight), (byte)(blue / weight))
                    : new Color32((byte)(red / weight), (byte)(green / weight), (byte)(blue / weight), 0);
            }
        loaded.Apply(false, !Application.isEditor);
        SongLoadingDiagnostics.Record("texture ready: " + path);
        return loaded;
    }

    public void ReplaceTexture(string path)
    {
        Release(replacementTexture);
        replacementTexture = LoadTexture(path, texture.filterMode == FilterMode.Point);
        material.mainTexture = replacementTexture;
        compositeFrame = -1;
    }

    public void WarmFrames()
    {
        int current = Frame;
        Mesh displayed = filter.sharedMesh;
        foreach (Clip animation in asset.clips.Values)
            foreach (int index in animation.frames)
                if (!meshes.ContainsKey(index)) SetFrame(index);
        SetFrame(current);
        filter.sharedMesh = displayed;
    }

    public void SetRim(string path, float distance, float threshold, Vector4 adjustment, Color? color = null, float angle = 90, float maskThreshold = 1, bool composite = false)
    {
        if (path != null)
        {
            PrepareRimMask(path);
            rimMask = preparedRimMask;
            material.SetTexture("_RimMask", rimMask);
        }
        HasRim = true;
        rimAngle = angle;
        compositeRim = composite;
        rimSettings = new Vector4(distance, threshold, maskThreshold, 1);
        rimAdjustment = adjustment;
        rimColor = color ?? new Color(82 / 255f, 53 / 255f, 29 / 255f);
        material.SetVector("_Rim", composite ? Vector4.zero : rimSettings);
        material.SetVector("_RimAdjustment", adjustment);
        material.SetColor("_RimColor", rimColor);
        UpdateRimAngle();
    }

    public void ClearRimMask()
    {
        rimMask = null;
        material.SetTexture("_RimMask", Texture2D.blackTexture);
    }

    public void PrepareRimMask(string path)
    {
        if (preparedRimMask != null && preparedRimMaskPath == path) return;
        Release(preparedRimMask);
        preparedRimMask = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!preparedRimMask.LoadImage(File.ReadAllBytes(path), !Application.isEditor))
            throw new InvalidDataException("Could not decode rim mask: " + path);
        preparedRimMask.filterMode = texture.filterMode;
        preparedRimMask.wrapMode = TextureWrapMode.Clamp;
        preparedRimMaskPath = path;
    }

    private void UpdateRimAngle()
    {
        float radians = rimAngle * Mathf.Deg2Rad;
        material.SetVector("_RimDirection", new Vector4(Mathf.Cos(radians), Mathf.Sin(radians), 0, 0));
    }

    public bool Play(string name, bool reverse = false)
    {
        if (!asset.clips.TryGetValue(name, out Clip next)) return false;
        Animation = name;
        FrozenFrame = -1;
        clip = next;
        age = 0;
        reversed = reverse;
        Finished = false;
        SetFrame(clip.frames[reversed ? clip.frames.Length - 1 : 0]);
        return true;
    }

    public void Advance(float delta, Vector3 camera, float clock)
    {
        age += delta;
        int index = Mathf.FloorToInt(age * clip.fps);
        int count = clip.frames.Length;
        Finished = !clip.loop && index >= count;
        index = clip.loop ? index % count : Mathf.Min(index, count - 1);
        if (reversed) index = count - 1 - index;
        SetFrame(FrozenFrame >= 0 ? FrozenFrame : clip.frames[index]);
        Vector3 scrollOrigin = camera - new Vector3(6.4f, -3.6f, camera.z);
        Vector3 offset = GlobalOffset + (ApplyAnimationOffsets ? clip.offset : Vector3.zero);
        if (scaledOffsets)
            offset = Vector3.Scale(offset, new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, 1));
        transform.localPosition = Position + offset + new Vector3(scrollOrigin.x * (1 - Scroll.x), scrollOrigin.y * (1 - Scroll.y), 0);
        transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x) * (FlipX ? -1 : 1), transform.localScale.y, 1);
        if (FlipX)
        {
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, 1);
            transform.localPosition += Vector3.right * FrameSize.x / 100 * Mathf.Abs(transform.localScale.x);
        }
        transform.localRotation = Quaternion.Euler(0, 0, -Angle);
        if (Angle != 0)
        {
            Vector3 pivot = new Vector3(FrameSize.x * transform.localScale.x / 200, -FrameSize.y * transform.localScale.y / 200, 0);
            transform.localPosition += pivot - transform.localRotation * pivot;
        }
        bool grouped = compositeRim || PhillyColor || ColorAdjustment != Vector4.zero || CompositeAlpha && Alpha > 0 && Alpha < 1;
        material.SetMatrix("_Affine", grouped ? Matrix4x4.identity : VertexTransform);
        material.SetColor("_Lighting", grouped ? Color.white : Lighting);
        material.SetFloat("_Opacity", grouped ? 1 : Alpha);
        material.SetFloat("_DstBlend", (float)(Additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        material.SetFloat("_Multiply", Multiply ? 1 : 0);
        material.SetFloat("_SrcBlend", (float)(Multiply ? BlendMode.DstColor : BlendMode.SrcAlpha));
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
        if (VertexTransform != Matrix4x4.identity)
        {
            Bounds bounds = filter.sharedMesh.bounds;
            Vector3 extents = bounds.extents;
            meshRenderer.localBounds = new Bounds(VertexTransform.MultiplyPoint3x4(bounds.center), new Vector3(
                Mathf.Abs(VertexTransform.m00) * extents.x + Mathf.Abs(VertexTransform.m01) * extents.y,
                Mathf.Abs(VertexTransform.m10) * extents.x + Mathf.Abs(VertexTransform.m11) * extents.y, .1f) * 2);
        }
    }

    private void SetFrame(int index)
    {
        if (Frame == index) return;
        Frame = index;
        material.SetVector("_FrameBounds", asset.frameBounds[index]);
        if (!meshes.TryGetValue(index, out Mesh mesh))
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var colors = new List<Color>();
            var additions = new List<Vector4>();
            var frameBounds = new List<Vector4>();
            var rotations = new List<Vector2>();
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
                    frameBounds.Add(new Vector4(x, y - h, x + w, y));
                    rotations.Add(new Vector2((bool)quad["rotated"] ? 1 : 0, 0));
                }
                triangles.AddRange(new[] { first, first + 1, first + 2, first + 2, first + 3, first });
            }
            mesh = new Mesh { name = name + " " + index };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colors);
            mesh.SetUVs(1, additions);
            mesh.SetUVs(2, frameBounds);
            mesh.SetUVs(3, rotations);
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
            SongLoadingDiagnostics.Record("composite: " + assetKey + " " + compositeBounds.size);
            composite = new RenderTexture(Mathf.CeilToInt(compositeBounds.width * 100), Mathf.CeilToInt(compositeBounds.height * 100), 0, RenderTextureFormat.ARGB32);
            composite.filterMode = texture.filterMode;
            composite.Create();
            compositeMaterial = new Material(Resources.Load<Shader>("VanillaSongs/Week2Composite"));
            compositeCommands = new CommandBuffer { name = "Week 2 character opacity" };
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
            compositeCommands.Clear();
            compositeCommands.SetRenderTarget(composite);
            compositeCommands.ClearRenderTarget(false, true, Color.clear);
            Matrix4x4 projection = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(compositeBounds.xMin, compositeBounds.xMax,
                compositeBounds.yMin, compositeBounds.yMax, -1, 1), true);
            compositeCommands.SetViewProjectionMatrices(Matrix4x4.identity, projection);
            compositeCommands.DrawMesh(meshes[Frame], Matrix4x4.identity, material);
            Graphics.ExecuteCommandBuffer(compositeCommands);
            compositeFrame = Frame;
        }
        compositeMaterial.SetFloat("_Opacity", Alpha);
        compositeMaterial.SetMatrix("_Affine", VertexTransform);
        compositeMaterial.SetFloat("_AffineEnabled", 1);
        compositeMaterial.SetColor("_Lighting", Lighting);
        compositeMaterial.SetFloat("_PhillyColor", PhillyColor ? 1 : 0);
        compositeMaterial.SetVector("_Adjustment", ColorAdjustment);
        compositeMaterial.SetVector("_Rim", compositeRim ? rimSettings : Vector4.zero);
        compositeMaterial.SetTexture("_RimMask", rimMask != null ? rimMask : Texture2D.blackTexture);
        compositeMaterial.SetVector("_RimAdjustment", rimAdjustment);
        compositeMaterial.SetColor("_RimColor", rimColor);
        compositeMaterial.SetVector("_RimDirection", material.GetVector("_RimDirection"));
        filter.sharedMesh = compositeMesh;
        meshRenderer.sharedMaterial = compositeMaterial;
    }

    private void OnDestroy()
    {
        compositeCommands?.Release();
        if (asset != null && --asset.users == 0)
        {
            Assets.Remove(assetKey);
            foreach (Mesh mesh in asset.meshes.Values) Release(mesh);
            Release(asset.texture);
        }
        Release(preparedRimMask);
        Release(replacementTexture);
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
