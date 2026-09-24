using UnityEngine;

public sealed class FunkinStrumlineBackground : MonoBehaviour
{
    private Song song;
    private readonly SpriteRenderer[] backgrounds = new SpriteRenderer[2];

    public void Initialize(Song owner)
    {
        song = owner;
        for (int side=0;side<2;side++)
        {
            var strums = side==0?song.player1NoteSprites:song.player2NoteSprites;
            var host = new GameObject("Strumline Background "+side);
            host.transform.SetParent(transform,false);
            host.layer = strums[0].gameObject.layer;
            var renderer = host.AddComponent<SpriteRenderer>();
            renderer.sprite = FunkinHudAssets.Solid;
            renderer.sharedMaterial = FunkinNoteSkin.NoteMaterial;
            renderer.sortingLayerID = strums[0].sortingLayerID;
            renderer.sortingOrder = strums[0].sortingOrder-10;
            backgrounds[side] = renderer;
        }
    }

    private void LateUpdate()
    {
        if (song == null) return;
        float opacity = VanillaPreferences.Get("StrumlineBackground")/100f;
        for (int side=0;side<2;side++)
        {
            var renderer = backgrounds[side];
            var strums = side==0?song.player1NoteSprites:song.player2NoteSprites;
            renderer.enabled = opacity>0 && song.uiCamera.enabled && song.battleCanvas.enabled && strums[0].enabled && strums[0].gameObject.activeInHierarchy && !song.isDead;
            renderer.color = new Color(0,0,0,opacity);
            float pixel = song.FunkinWorldPixelSize;
            float x = strums[0].transform.position.x+172*pixel;
            renderer.transform.localScale = new Vector3(480*pixel/renderer.sprite.bounds.size.x,720*pixel/renderer.sprite.bounds.size.y,1);
            Vector3 center = new Vector3(x,song.uiCamera.transform.position.y,strums[0].transform.position.z+.01f);
            renderer.transform.position = center-renderer.transform.TransformVector(renderer.sprite.bounds.center);
        }
    }
}
