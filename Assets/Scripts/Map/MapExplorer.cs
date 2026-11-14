using System;
using SaveSystem;
using UnityEngine;

// Remembers which parts of the level the player has been near, for the Eye spell map. Everything within
// revealRadius of the player gets mapped as they walk, whether or not the map is open. Saved with checkpoints.
//
// SETUP: put on the Player and drag in the level's MapLayout (made by MapLayoutBaker).
public class MapExplorer : MonoBehaviour, ICheckpointSaveable
{
    [Serializable]
    public class MapExplorerCheckpointState
    {
        public int width;
        public int height;
        public string revealed; // base64 bytes, one per cell
    }

    public static MapExplorer Instance { get; private set; }

    [Tooltip("This level's baked layout.")]
    public MapLayout layout;
    [Tooltip("Everything within this many metres of the player gets mapped.")]
    public float revealRadius = 8f;
    [Tooltip("Width of the soft edge at the outside of the radius, in metres.")]
    public float revealSoftness = 2f;
    [Tooltip("Metres per cell of the explored-area record. Doesn't need to be as fine as the layout.")]
    public float cellSize = 0.5f;
    [Tooltip("Seconds between updates.")]
    public float updateInterval = 0.1f;

    private Texture2D revealTexture;
    private byte[] revealed;
    private int width;
    private int height;
    private bool dirty;
    private float timer;
    private Vector3 lastRevealPosition;
    private bool hasRevealed;

    public MapLayout Layout => layout;
    /// <summary>Single-channel texture over the same area as the layout: 0 = never seen, 1 = mapped.</summary>
    public Texture RevealTexture => revealTexture;
    public string SaveId => CheckpointSaveIdUtility.GetStableSaveId(this);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("More than one MapExplorer in the scene - only the first one is used.");
            return;
        }
        Instance = this;

        if (layout == null || layout.worldSize.x <= 0f || layout.worldSize.y <= 0f)
        {
            Debug.LogWarning("MapExplorer: no MapLayout assigned - bake one with MapLayoutBaker. The map won't work.");
            enabled = false;
            return;
        }

        width = Mathf.Max(1, Mathf.CeilToInt(layout.worldSize.x / cellSize));
        height = Mathf.Max(1, Mathf.CeilToInt(layout.worldSize.y / cellSize));
        revealed = new byte[width * height];
        revealTexture = new Texture2D(width, height, TextureFormat.R8, false, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "MapReveal"
        };
        dirty = true;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (revealTexture != null) Destroy(revealTexture);
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = updateInterval;
            Vector3 position = transform.position;
            if (!hasRevealed || (position - lastRevealPosition).sqrMagnitude > 0.04f)
            {
                RevealAround(position);
                lastRevealPosition = position;
                hasRevealed = true;
            }
        }

        if (dirty)
        {
            revealTexture.SetPixelData(revealed, 0);
            revealTexture.Apply(false, false);
            dirty = false;
        }
    }

    private void RevealAround(Vector3 position)
    {
        float cx = (position.x - layout.worldMin.x) / cellSize;
        float cz = (position.z - layout.worldMin.y) / cellSize;
        float radiusCells = revealRadius / cellSize;
        float softCells = Mathf.Max(revealSoftness / cellSize, 0.001f);

        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - radiusCells));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(cx + radiusCells));
        int minZ = Mathf.Max(0, Mathf.FloorToInt(cz - radiusCells));
        int maxZ = Mathf.Min(height - 1, Mathf.CeilToInt(cz + radiusCells));

        for (int z = minZ; z <= maxZ; z++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x + 0.5f - cx;
                float dz = z + 0.5f - cz;
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                byte value = (byte)(Mathf.Clamp01((radiusCells - distance) / softCells) * 255f);

                int index = z * width + x;
                if (value > revealed[index])
                {
                    revealed[index] = value;
                    dirty = true;
                }
            }
        }
    }

    public object CaptureState()
    {
        if (revealed == null) return null;
        return new MapExplorerCheckpointState
        {
            width = width,
            height = height,
            revealed = Convert.ToBase64String(revealed)
        };
    }

    public void RestoreState(object state)
    {
        if (!(state is MapExplorerCheckpointState mapState) || revealed == null) return;
        // A re-baked layout can change the size; an old save just doesn't fit any more.
        if (mapState.width != width || mapState.height != height || string.IsNullOrEmpty(mapState.revealed)) return;

        byte[] saved = Convert.FromBase64String(mapState.revealed);
        if (saved.Length != revealed.Length) return;

        Array.Copy(saved, revealed, revealed.Length);
        dirty = true;
        hasRevealed = false; // re-reveal around wherever the player was loaded
    }
}
