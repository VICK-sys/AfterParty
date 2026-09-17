using UnityEngine;

public sealed class FunkinHoldMesh : MonoBehaviour
{
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private readonly Vector3[] vertices = new Vector3[8];
    private readonly Vector2[] uv = new Vector2[8];
    private static readonly int[] Triangles = { 0, 1, 2, 1, 3, 2, 4, 5, 6, 5, 7, 6 };

    private void Awake()
    {
        mesh = new Mesh { name = "Funkin Sustain" };
        mesh.MarkDynamic();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = FunkinNoteSkin.HoldMaterial;
        meshRenderer.sortingOrder = 20;
    }

    public void Draw(int direction, double remaining, float speed, bool downscroll, Vector3 anchor, float pixel, bool visible)
    {
        meshRenderer.enabled = visible && remaining > 0;
        if (!meshRenderer.enabled) return;
        Texture texture = FunkinNoteSkin.HoldMaterial.mainTexture;
        float width = texture.width / 8f * 0.7f;
        float height = (float)(remaining * FunkinRules.PixelsPerMillisecond * speed);
        float bottomHeight = texture.height * 0.7f * 0.5f;
        float bodyHeight = Mathf.Max(0, height - bottomHeight);
        float endHeight = height + texture.height * 0.7f * 0.4f;
        float sign = downscroll ? 1 : -1;
        vertices[0] = new Vector3(-width / 2, 0);
        vertices[1] = new Vector3(width / 2, 0);
        vertices[2] = new Vector3(-width / 2, sign * bodyHeight);
        vertices[3] = new Vector3(width / 2, sign * bodyHeight);
        vertices[4] = vertices[2];
        vertices[5] = vertices[3];
        vertices[6] = new Vector3(-width / 2, sign * endHeight);
        vertices[7] = new Vector3(width / 2, sign * endHeight);
        float left = direction / 4f;
        float top = 1 + (height - bottomHeight) / texture.height / 0.7f;
        uv[0] = new Vector2(left, top);
        uv[1] = new Vector2(left + 0.125f, top);
        uv[2] = new Vector2(left, 1);
        uv[3] = new Vector2(left + 0.125f, 1);
        float capTop = bodyHeight > 0 ? 1 : 1 - (bottomHeight - height) / texture.height / 0.7f;
        uv[4] = new Vector2(left + 0.125f, capTop);
        uv[5] = new Vector2(left + 0.25f, capTop);
        uv[6] = new Vector2(left + 0.125f, 0.1f);
        uv[7] = new Vector2(left + 0.25f, 0.1f);
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = Triangles;
        mesh.RecalculateBounds();
        transform.position = anchor;
        FunkinNoteSkin.WorldScale(transform, pixel);
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
