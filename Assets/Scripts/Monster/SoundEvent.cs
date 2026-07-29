using UnityEngine;

// A single "sound" in the world (footstep, spellcast, etc.) that monsters can hear.
[System.Serializable]
public struct SoundEvent
{
    public Vector3 position;
    public float radius;
    public float timeCreated;
    public float lifetime;

    public bool IsExpired => Time.time - timeCreated > lifetime;
}
