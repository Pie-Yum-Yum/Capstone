using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Editor tool: bakes the level's room layout into a MapLayout for the Eye spell map.
//
// It takes a horizontal "slice" through the level at about chest height. Anything solid in the slice is a wall,
// and anything with ground under it is floor. Doors, the player and the monster are skipped, so doorways show
// as gaps. Ceilings never get in the way because nothing is rendered from above.
//
// SETUP: put this on an empty GameObject, size the yellow box (Scene view) to cover the whole level with its
// bottom at floor level, then right-click the component header > Bake Map Layout. Re-bake after changing walls.
// Drag the resulting MapLayout asset into the MapExplorer on the Player.
public class MapLayoutBaker : MonoBehaviour
{
    [Tooltip("Size of the area to bake, centred on this object. The bottom of the box should sit at floor level.")]
    public Vector3 size = new Vector3(100f, 6f, 100f);
    [Tooltip("Metres per map pixel. Smaller = sharper map but a bigger texture and a slower bake.")]
    public float cellSize = 0.25f;
    [Tooltip("Height of the slice above the bottom of the box. Around chest height catches walls but not most furniture.")]
    public float sliceHeight = 1.2f;
    [Tooltip("How thick the slice is.")]
    public float sliceThickness = 0.4f;
    [Tooltip("Layers that count as walls and floors.")]
    public LayerMask layers = ~0;
    [Tooltip("The asset to bake into. Left empty, a new one is made in Assets/Map named after the scene.")]
    public MapLayout layout;

    private float SliceY => transform.position.y - size.y * 0.5f + sliceHeight;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, size);
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        Gizmos.DrawWireCube(new Vector3(transform.position.x, SliceY, transform.position.z),
            new Vector3(size.x, sliceThickness, size.z));
    }

#if UNITY_EDITOR
    [ContextMenu("Bake Map Layout")]
    private void Bake()
    {
        int width = Mathf.CeilToInt(size.x / cellSize);
        int height = Mathf.CeilToInt(size.z / cellSize);
        if (cellSize <= 0f || width <= 0 || height <= 0 || (long)width * height > 4096L * 4096L)
        {
            Debug.LogError($"MapLayoutBaker: {width}x{height} pixels is not a usable size. Check Size and Cell Size.");
            return;
        }

        Physics.SyncTransforms();

        Vector2 worldMin = new Vector2(transform.position.x - size.x * 0.5f, transform.position.z - size.z * 0.5f);
        float sliceY = SliceY;
        float floorBottom = transform.position.y - size.y * 0.5f;
        Vector3 halfCell = new Vector3(cellSize * 0.5f, sliceThickness * 0.5f, cellSize * 0.5f);
        Collider[] hits = new Collider[32];
        Color32[] pixels = new Color32[width * height];
        int wallCount = 0, floorCount = 0;

        try
        {
            for (int z = 0; z < height; z++)
            {
                if (z % 16 == 0 && EditorUtility.DisplayCancelableProgressBar("Baking map layout", $"Row {z} of {height}", z / (float)height))
                {
                    Debug.Log("MapLayoutBaker: bake cancelled.");
                    return;
                }

                for (int x = 0; x < width; x++)
                {
                    Vector3 centre = new Vector3(worldMin.x + (x + 0.5f) * cellSize, sliceY, worldMin.y + (z + 0.5f) * cellSize);

                    bool wall = false;
                    int count = Physics.OverlapBoxNonAlloc(centre, halfCell, hits, Quaternion.identity, layers, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < count && !wall; i++) wall = !IsSkipped(hits[i]);

                    bool floor = Physics.Raycast(centre, Vector3.down, sliceY - floorBottom + 0.5f, layers, QueryTriggerInteraction.Ignore);

                    if (wall) wallCount++;
                    if (floor) floorCount++;
                    pixels[z * width + x] = new Color32(floor ? (byte)255 : (byte)0, wall ? (byte)255 : (byte)0, 0, 255);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        SaveLayout(pixels, width, height, worldMin);
        Debug.Log($"MapLayoutBaker: baked {width}x{height} ({wallCount} wall, {floorCount} floor pixels) into {AssetDatabase.GetAssetPath(layout)}.");
    }

    // Things that move or open shouldn't be drawn as walls.
    private static bool IsSkipped(Collider collider)
    {
        return collider.CompareTag("Player")
               || collider.GetComponentInParent<door>() != null
               || collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null;
    }

    private void SaveLayout(Color32[] pixels, int width, int height, Vector2 worldMin)
    {
        string assetPath = layout != null ? AssetDatabase.GetAssetPath(layout) : null;
        if (string.IsNullOrEmpty(assetPath))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Map")) AssetDatabase.CreateFolder("Assets", "Map");
            string sceneName = string.IsNullOrEmpty(gameObject.scene.name) ? "Level" : gameObject.scene.name;
            assetPath = AssetDatabase.GenerateUniqueAssetPath($"Assets/Map/{sceneName}_MapLayout.asset");
        }
        string pngPath = Path.ChangeExtension(assetPath, null) + ".png";

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        File.WriteAllBytes(pngPath, texture.EncodeToPNG());
        DestroyImmediate(texture);
        AssetDatabase.ImportAsset(pngPath);

        // Raw data, not a picture: no colour correction, no compression, no resizing to a power of two.
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 8192;
        importer.SaveAndReimport();

        if (layout == null)
        {
            layout = ScriptableObject.CreateInstance<MapLayout>();
            AssetDatabase.CreateAsset(layout, assetPath);
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }

        layout.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        layout.worldMin = worldMin;
        layout.worldSize = new Vector2(width * cellSize, height * cellSize);
        EditorUtility.SetDirty(layout);
        AssetDatabase.SaveAssets();
    }
#endif
}
