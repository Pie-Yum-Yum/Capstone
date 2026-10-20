using UnityEngine;

// Attach this to the player (same object as PlayerMovement). Plays a footstep clip every time the
// player covers one "step" of ground, so steps naturally speed up when sprinting and slow down
// when crouching. Distance-based (not timer-based) so sliding to a stop doesn't keep stepping.
// This is real audio only — the monster's hearing is handled separately by PlayerSoundEmitter.
[RequireComponent(typeof(Rigidbody))]
public class PlayerFootsteps : MonoBehaviour
{
    [Header("Clips")]
    [Tooltip("Individual single-step sounds. Use several (4+) so it doesn't sound like a loop.")]
    public AudioClip[] footstepClips;
    [Tooltip("Plays the footsteps. Leave empty to create one on this object.")]
    public AudioSource audioSource;

    [Header("Step Distance (metres per footstep)")]
    public float walkStepDistance = 1.8f;
    public float sprintStepDistance = 2.4f;
    public float crouchStepDistance = 1.3f;

    [Header("Volume")]
    [Range(0f, 1f)] public float walkVolume = 0.6f;
    [Range(0f, 1f)] public float sprintVolume = 0.9f;
    [Range(0f, 1f)] public float crouchVolume = 0.25f;

    [Header("Variation")]
    [Tooltip("Random pitch range applied per step. Small ranges sound most natural.")]
    public Vector2 pitchRange = new Vector2(0.92f, 1.08f);
    [Tooltip("Each step's volume is multiplied by a random value in this range.")]
    public Vector2 volumeJitter = new Vector2(0.85f, 1f);

    [Header("Detection")]
    [Tooltip("Horizontal speed below which the player counts as standing still.")]
    public float minMoveSpeed = 0.3f;
    [Tooltip("How far below the collider's bottom we still count as grounded.")]
    public float groundCheckExtra = 0.3f;
    [Tooltip("How much of a step is already 'walked' when you start moving, so the first step comes quickly (0..1).")]
    [Range(0f, 1f)] public float firstStepHead = 0.6f;

    private PlayerMovement movement;
    private Rigidbody rb;
    private Collider col;
    private float distanceSinceStep;
    private int lastClipIndex = -1;
    private bool wasMoving;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // the player's own feet: always "under" the listener
        }
    }

    private void Update()
    {
        if (footstepClips == null || footstepClips.Length == 0) return;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        float speed = velocity.magnitude;

        bool moving = speed > minMoveSpeed && IsGrounded();
        if (!moving)
        {
            wasMoving = false;
            return;
        }

        float stepDistance = CurrentStepDistance();
        if (!wasMoving)
        {
            distanceSinceStep = stepDistance * firstStepHead;
            wasMoving = true;
        }

        distanceSinceStep += speed * Time.deltaTime;
        if (distanceSinceStep >= stepDistance)
        {
            distanceSinceStep = 0f;
            PlayStep();
        }
    }

    private float CurrentStepDistance()
    {
        if (movement == null) return walkStepDistance;
        if (movement.isCrouching) return crouchStepDistance;
        if (movement.IsSprinting) return sprintStepDistance;
        return walkStepDistance;
    }

    private float CurrentVolume()
    {
        if (movement == null) return walkVolume;
        if (movement.isCrouching) return crouchVolume;
        if (movement.IsSprinting) return sprintVolume;
        return walkVolume;
    }

    private void PlayStep()
    {
        // Pick a random clip, but never the same one twice in a row.
        int index = Random.Range(0, footstepClips.Length);
        if (footstepClips.Length > 1 && index == lastClipIndex)
        {
            index = (index + Random.Range(1, footstepClips.Length)) % footstepClips.Length;
        }
        lastClipIndex = index;

        AudioClip clip = footstepClips[index];
        if (clip == null) return;

        audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        audioSource.PlayOneShot(clip, CurrentVolume() * Random.Range(volumeJitter.x, volumeJitter.y));
    }

    private bool IsGrounded()
    {
        if (col == null) return true;
        Bounds b = col.bounds;
        return Physics.Raycast(b.center, Vector3.down, b.extents.y + groundCheckExtra, ~0, QueryTriggerInteraction.Ignore);
    }
}
