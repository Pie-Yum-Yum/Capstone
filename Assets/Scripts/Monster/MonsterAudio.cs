using UnityEngine;

// Attach to the monster (same object as MonsterAI). Gives it a constant 3D "presence" loop
// (breathing / clicking / skittering) that changes with its state, random vocal calls, and
// one-off stingers when it starts hunting or chasing. Everything is 3D, so the player can tell
// where it is - and how close - by ear. All clips are optional.
[RequireComponent(typeof(MonsterAI))]
public class MonsterAudio : MonoBehaviour
{
    [System.Serializable]
    public class StateSound
    {
        [Range(0f, 1f)] public float loopVolume = 0.5f;
        public float loopPitch = 1f;
        [Tooltip("Random calls (growls, clicks, shrieks) while in this state. Leave empty for none.")]
        public AudioClip[] calls;
        [Tooltip("Min/max seconds between random calls.")]
        public Vector2 callInterval = new Vector2(6f, 14f);
        [Range(0f, 1f)] public float callVolume = 0.8f;
    }

    [Header("Presence Loop")]
    [Tooltip("A LOOPING sound that always plays from the monster (breathing, clicking, skittering).")]
    public AudioClip loopClip;
    [Tooltip("How quickly the loop's volume/pitch blend to the new state's values.")]
    public float stateBlendSpeed = 1.5f;

    [Header("Per-State Sounds")]
    public StateSound patrol = new StateSound { loopVolume = 0.4f, loopPitch = 1f, callInterval = new Vector2(8f, 18f) };
    public StateSound investigate = new StateSound { loopVolume = 0.55f, loopPitch = 1.05f, callInterval = new Vector2(5f, 10f) };
    public StateSound chase = new StateSound { loopVolume = 0.8f, loopPitch = 1.15f, callInterval = new Vector2(2.5f, 5f) };

    [Header("Stingers")]
    [Tooltip("Plays once when the monster spots the player and starts chasing.")]
    public AudioClip chaseStartClip;
    [Tooltip("Plays once when the monster hears something and starts investigating.")]
    public AudioClip investigateStartClip;
    [Range(0f, 1f)] public float stingerVolume = 1f;

    [Header("3D Falloff")]
    [Tooltip("Full volume inside this distance.")]
    public float minDistance = 2f;
    [Tooltip("Silent beyond this distance.")]
    public float maxDistance = 25f;

    private MonsterAI ai;
    private AudioSource loopSource;
    private AudioSource oneShotSource;
    private MonsterAI.State lastState;
    private float callTimer;

    private void Awake()
    {
        ai = GetComponent<MonsterAI>();
        loopSource = CreateSource(true);
        oneShotSource = CreateSource(false);
    }

    private void Start()
    {
        lastState = ai.CurrentState;
        StateSound s = SoundFor(lastState);
        ResetCallTimer(s);

        if (loopClip != null)
        {
            loopSource.clip = loopClip;
            loopSource.volume = s.loopVolume;
            loopSource.pitch = s.loopPitch;
            loopSource.time = Random.Range(0f, loopClip.length);
            loopSource.Play();
        }
    }

    private void Update()
    {
        MonsterAI.State state = ai.CurrentState;
        StateSound s = SoundFor(state);

        if (state != lastState)
        {
            if (state == MonsterAI.State.Chase) PlayOneShot(chaseStartClip, stingerVolume);
            else if (state == MonsterAI.State.Investigate && lastState == MonsterAI.State.Patrol) PlayOneShot(investigateStartClip, stingerVolume);
            lastState = state;
            ResetCallTimer(s);
        }

        // Smoothly blend the loop toward this state's volume/pitch so it swells rather than jumps.
        float t = 1f - Mathf.Exp(-stateBlendSpeed * Time.deltaTime);
        loopSource.volume = Mathf.Lerp(loopSource.volume, s.loopVolume, t);
        loopSource.pitch = Mathf.Lerp(loopSource.pitch, s.loopPitch, t);

        callTimer -= Time.deltaTime;
        if (callTimer <= 0f)
        {
            if (s.calls != null && s.calls.Length > 0)
            {
                PlayOneShot(s.calls[Random.Range(0, s.calls.Length)], s.callVolume);
            }
            ResetCallTimer(s);
        }
    }

    private StateSound SoundFor(MonsterAI.State state)
    {
        switch (state)
        {
            case MonsterAI.State.Chase: return chase;
            case MonsterAI.State.Investigate: return investigate;
            default: return patrol;
        }
    }

    private void ResetCallTimer(StateSound s)
    {
        callTimer = Random.Range(s.callInterval.x, s.callInterval.y);
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null) return;
        oneShotSource.pitch = Random.Range(0.9f, 1.1f);
        oneShotSource.PlayOneShot(clip, volume);
    }

    private AudioSource CreateSource(bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f; // NavMeshAgent speed changes make doppler warble the pitch
        return source;
    }
}
