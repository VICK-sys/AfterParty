using UnityEngine;
using UnityEngine.UI;

public sealed class VanillaIconOutline : Outline
{
    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive()) return;
        int original = vertices.currentIndexCount;
        base.ModifyMesh(vertices);
        UIVertex vertex = default;
        for (int i = 0; i < vertices.currentVertCount - original; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);
            vertex.uv1 = Vector2.one;
            vertices.SetUIVertex(vertex, i);
        }
    }
}
