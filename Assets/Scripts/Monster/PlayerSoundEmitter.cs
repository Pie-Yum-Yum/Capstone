using UnityEngine;

// Attach this to the player. It watches how far/fast the player moves each frame and emits
// a SoundEvent (via SoundManager) that the monster can "hear" — no real audio required.
public class PlayerSoundEmitter : MonoBehaviour
{
    [Header("Movement Sound")]
    [Tooltip("Minimum distance the player must move before a new sound is emitted.")]
    public float moveDistanceThreshold = 0.5f;
    [Tooltip("Minimum time between emitted sounds, so we don't spam SoundManager every frame.")]
    public float emitCooldown = 0.3f;

    [Header("Walking")]
    public float walkSoundRadius = 5f;
    public float walkSoundLifetime = 2f;

    [Header("Running")]
    [Tooltip("Speed (units/sec) above which movement counts as 'running' and makes a louder sound.")]
    public float runSpeedThreshold = 4f;
    public float runSoundRadius = 10f;
    public float runSoundLifetime = 2.5f;

    private Vector3 lastEmitPosition;
    private float lastEmitTime;

    private void Start()
    {
        lastEmitPosition = transform.position;
        lastEmitTime = -999f;
    }

    private void Update()
    {
        float distanceMoved = Vector3.Distance(transform.position, lastEmitPosition);

        if (distanceMoved >= moveDistanceThreshold && Time.time - lastEmitTime >= emitCooldown)
        {
            float speed = distanceMoved / Mathf.Max(Time.time - lastEmitTime, 0.0001f);
            bool isRunning = speed >= runSpeedThreshold;

            float radius = isRunning ? runSoundRadius : walkSoundRadius;
            float lifetime = isRunning ? runSoundLifetime : walkSoundLifetime;

            SoundManager.Instance?.EmitSound(transform.position, radius, lifetime);

            lastEmitPosition = transform.position;
            lastEmitTime = Time.time;
        }
    }

    // Call this from other systems (e.g. a loud spell being cast) to emit a one-off, louder sound.
    public void EmitCustomSound(float radius, float lifetime)
    {
        SoundManager.Instance?.EmitSound(transform.position, radius, lifetime);
        lastEmitPosition = transform.position;
        lastEmitTime = Time.time;
    }
}
