using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class VanillaResultsBar : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        mesh.AddVert(new Vector3(0, 0), Color.black, Vector2.zero);
        mesh.AddVert(new Vector3(1280, 0), Color.black, Vector2.zero);
        mesh.AddVert(new Vector3(1280, -63.4f), Color.black, Vector2.zero);
        mesh.AddVert(new Vector3(0, -148.3f), Color.black, Vector2.zero);
        mesh.AddTriangle(0, 1, 2);
        mesh.AddTriangle(0, 2, 3);
    }
}
