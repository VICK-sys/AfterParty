using UnityEngine;
using UnityEngine.UI;

public sealed class VanillaTitleSkipIndicator : MaskableGraphic
{
    private float amount;

    public void SetAmount(float value)
    {
        amount = value;
        color = new Color(1, 1, 1, value);
        rectTransform.localScale = Vector3.one * Mathf.Lerp(1, 1.3f, value);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        for (int i = 0; i < 96; i++)
        {
            float start = i / 96f;
            float end = (i + 1) / 96f;
            Color tint = start < amount ? color : new Color(0.773f, 0.769f, 0.769f, color.a * 0.541f);
            int first = mesh.currentVertCount;
            foreach (Vector2 corner in new[] { new Vector2(start, 40), new Vector2(end, 40), new Vector2(end, 20), new Vector2(start, 20) })
            {
                float angle = corner.x * Mathf.PI * 2 - Mathf.PI / 2;
                mesh.AddVert(new Vector3(Mathf.Cos(angle) * corner.y, -Mathf.Sin(angle) * corner.y), tint, Vector2.zero);
            }
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
