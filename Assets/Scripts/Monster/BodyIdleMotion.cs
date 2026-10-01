using UnityEngine;

/// <summary>
/// Adds subtle, continuous organic motion to a body segment so it doesn't
/// read as a static object being dragged around by its legs. This is purely
/// additive - it offsets a VISUAL child transform from wherever your actual
/// movement/AI script puts the real body, so it never fights your existing
/// locomotion logic.
///
/// SETUP: don't put this directly on the body segment that your AI/movement
/// script controls. Instead, create a child object (e.g. "VisualRoot") under
/// each body segment, move the actual mesh renderer onto that child, and put
/// this script on the child. That way this script only ever touches the
/// child's LOCAL transform, leaving the parent's real position alone.
/// </summary>
public class BodyIdleMotion : MonoBehaviour
{
    [Header("Breathing (scale pulse)")]
    public bool enableBreathing = true;
    public float breathAmplitude = 0.04f;   // how much bigger/smaller, as a fraction of scale
    public float breathSpeed = 0.6f;        // cycles per second-ish

    [Header("Sway (position drift)")]
    public bool enableSway = true;
    public float swayAmplitude = 0.05f;     // world units of drift
    public float swaySpeed = 0.25f;         // how fast the drift wanders

    [Header("Wobble (rotation)")]
    public bool enableWobble = true;
    public float wobbleAmplitudeDegrees = 3f;
    public float wobbleSpeed = 0.3f;

    [Header("Variation")]
    [Tooltip("If true, picks a random phase offset at Start so multiple segments don't move in perfect sync. " +
             "Turn this off and set seedOffset manually if you want deliberate control instead.")]
    public bool randomizePhase = true;
    public float seedOffset = 0f;

    Vector3 baseLocalPosition;
    Vector3 baseLocalScale;
    Quaternion baseLocalRotation;

    float noiseSeedX, noiseSeedY, noiseSeedZ, noiseSeedRot;

    void Start()
    {
        baseLocalPosition = transform.localPosition;
        baseLocalScale = transform.localScale;
        baseLocalRotation = transform.localRotation;

        float phase = randomizePhase ? Random.Range(0f, 1000f) : seedOffset;
        noiseSeedX = phase;
        noiseSeedY = phase + 37.1f;
        noiseSeedZ = phase + 91.7f;
        noiseSeedRot = phase + 15.3f;
    }

    void Update()
    {
        Vector3 localPos = baseLocalPosition;
        Vector3 localScale = baseLocalScale;
        Quaternion localRot = baseLocalRotation;

        if (enableSway)
        {
            // Perlin noise instead of plain sine so the drift feels organic
            // and non-repeating rather than an obvious back-and-forth loop.
            float t = Time.time * swaySpeed;
            float dx = (Mathf.PerlinNoise(noiseSeedX, t) - 0.5f) * 2f * swayAmplitude;
            float dy = (Mathf.PerlinNoise(noiseSeedY, t) - 0.5f) * 2f * swayAmplitude;
            float dz = (Mathf.PerlinNoise(noiseSeedZ, t) - 0.5f) * 2f * swayAmplitude;
            localPos += new Vector3(dx, dy, dz);
        }

        if (enableBreathing)
        {
            // Simple sine pulse for scale - breathing tends to read better
            // as smooth/rhythmic than as noisy, unlike position drift.
            float pulse = 1f + Mathf.Sin(Time.time * breathSpeed * Mathf.PI * 2f) * breathAmplitude;
            localScale = Vector3.Scale(baseLocalScale, new Vector3(pulse, pulse, pulse));
        }

        if (enableWobble)
        {
            float t = Time.time * wobbleSpeed;
            float pitch = (Mathf.PerlinNoise(noiseSeedRot, t) - 0.5f) * 2f * wobbleAmplitudeDegrees;
            float roll = (Mathf.PerlinNoise(noiseSeedRot + 50f, t) - 0.5f) * 2f * wobbleAmplitudeDegrees;
            localRot = baseLocalRotation * Quaternion.Euler(pitch, 0f, roll);
        }

        transform.localPosition = localPos;
        transform.localScale = localScale;
        transform.localRotation = localRot;
    }
}
