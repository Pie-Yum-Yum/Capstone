using System.Collections.Generic;
using UnityEngine;

// Singleton. Put this on an empty GameObject in your scene (e.g. "GameManager" or "SoundManager").
// Anything that makes noise (player movement, spellcasting, etc.) calls SoundManager.Instance.EmitSound(...).
// Monsters query TryGetLoudestSoundInRange(...) to "hear" the world without any real audio involved.
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Visualization (for testing)")]
    [Tooltip("Optional ring prefab (e.g. an expanding LineRenderer circle) to visualize a sound event in-game.")]
    public GameObject ringVisualPrefab;
    [Tooltip("If true, actually spawns the ring prefab in the game world (player would see it too). " +
             "If false, sounds are only visible via Gizmos in the Scene view for debugging.")]
    public bool spawnVisualRingObject = false;
    public float ringVisualLifetime = 1.5f;

    private readonly List<SoundEvent> activeSounds = new List<SoundEvent>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        activeSounds.RemoveAll(s => s.IsExpired);
    }

    public void EmitSound(Vector3 position, float radius, float lifetime)
    {
        activeSounds.Add(new SoundEvent
        {
            position = position,
            radius = radius,
            timeCreated = Time.time,
            lifetime = lifetime
        });

        if (spawnVisualRingObject && ringVisualPrefab != null)
        {
            GameObject ring = Instantiate(ringVisualPrefab, position, Quaternion.identity);
            Destroy(ring, ringVisualLifetime);
        }
    }

    // Returns the "loudest" (closest / biggest radius) sound audible from a listener position, if any.
    public bool TryGetLoudestSoundInRange(Vector3 listenerPosition, float hearingRadius, out SoundEvent result)
    {
        bool found = false;
        result = default;
        float bestScore = float.NegativeInfinity;

        foreach (SoundEvent sound in activeSounds)
        {
            float distance = Vector3.Distance(listenerPosition, sound.position);
            // Audible if the listener's hearing range plus the sound's own radius covers the distance
            if (distance <= hearingRadius + sound.radius)
            {
                float score = sound.radius - distance;
                if (score > bestScore)
                {
                    bestScore = score;
                    result = sound;
                    found = true;
                }
            }
        }
        return found;
    }

    // Scene-view only debug visualization, always on regardless of spawnVisualRingObject.
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
        foreach (SoundEvent sound in activeSounds)
        {
            Gizmos.DrawWireSphere(sound.position, sound.radius);
        }
    }
}
