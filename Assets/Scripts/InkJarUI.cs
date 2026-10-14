using SpellSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the HUD ink jar: fill level from InkManager, a subtle slosh from the player's movement and
/// camera turning, and the number above the jar.
///
/// SETUP: put this on the "Ink" Image that sits BEHIND the jar sprite (same size/position).
///  - Image Source Image = the jar's interior mask sprite (white inside the hollow).
///  - Image Material = a material using the "UI/Ink Jar" shader.
///  - Set the interior bottom/top rows to match your mask.
/// </summary>
[RequireComponent(typeof(Image))]
public class InkJarUI : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public Rigidbody playerBody;
    public Transform cameraTransform;
    [Tooltip("Optional number shown above the jar.")]
    public TMP_Text inkText;

    [Header("Pixel art")]
    [Tooltip("Resolution of the ink's pixel grid. 32 matches a 32x32 jar sprite.")]
    [Range(16, 128)] public int pixelGrid = 32;
    [Tooltip("Lowest row of the hollow, counted in mask pixels FROM THE BOTTOM (row 0 = bottom row). " +
             "Note: most pixel editors count from the top, so flip it: rowFromBottom = height - 1 - rowFromTop.")]
    public int interiorBottomRow = 3;
    [Tooltip("Highest row the ink can reach when full, counted from the bottom.")]
    public int interiorTopRow = 24;

    [Header("Colors")]
    public Color inkColor = new Color(0.08f, 0.06f, 0.18f, 1f);
    public Color surfaceColor = new Color(0.3f, 0.25f, 0.55f, 1f);

    [Header("Fill")]
    [Tooltip("How quickly the shown level catches up to the real ink amount.")]
    public float fillSmoothing = 8f;

    [Header("Slosh")]
    [Tooltip("How strongly speeding up / slowing down sideways tips the ink.")]
    public float moveInfluence = 0.004f;
    [Tooltip("How strongly turning the camera tips the ink. Flip the sign if it sloshes the wrong way.")]
    public float turnInfluence = 0.0004f;
    [Tooltip("How fast the ink swings back (spring stiffness).")]
    public float stiffness = 60f;
    [Tooltip("How quickly the sloshing settles.")]
    public float damping = 6f;
    [Tooltip("Max tilt across the jar's width, as a fraction of jar height. 0.1 = about 3 pixels on a 32px jar.")]
    public float maxTilt = 0.1f;

    [Header("Waves")]
    [Tooltip("Ripple height while still. 0 = flat when resting.")]
    public float idleWave = 0.004f;
    [Tooltip("Extra ripple height added by sloshing.")]
    public float waveFromMotion = 0.05f;
    public float maxWave = 0.02f;
    public float waveFrequency = 1.5f;
    public float waveSpeed = 3f;

    private static readonly int PixelGridId = Shader.PropertyToID("_PixelGrid");
    private static readonly int FillId = Shader.PropertyToID("_Fill");
    private static readonly int FillBottomId = Shader.PropertyToID("_FillBottom");
    private static readonly int FillTopId = Shader.PropertyToID("_FillTop");
    private static readonly int TiltId = Shader.PropertyToID("_Tilt");
    private static readonly int WaveAmpId = Shader.PropertyToID("_WaveAmp");
    private static readonly int WaveFreqId = Shader.PropertyToID("_WaveFreq");
    private static readonly int WavePhaseId = Shader.PropertyToID("_WavePhase");
    private static readonly int InkColorId = Shader.PropertyToID("_InkColor");
    private static readonly int SurfaceColorId = Shader.PropertyToID("_SurfaceColor");

    private Image image;
    private Material material;

    private float shownFill = 1f;
    private float tilt;
    private float tiltVelocity;
    private float wavePhase;
    private Vector3 lastVelocity;
    private float lastYaw;
    private bool hasLastFrame;
    private InkManager subscribedInk;

    private void Awake()
    {
        image = GetComponent<Image>();

        // Work on a copy so playing never edits the material asset.
        Material source = image.material;
        if (source == null || source.shader == null || source.shader.name != "UI/Ink Jar")
        {
            Debug.LogWarning("InkJarUI: assign a material using the 'UI/Ink Jar' shader to this Image.");
            Shader shader = Shader.Find("UI/Ink Jar");
            if (shader == null)
            {
                Debug.LogError("InkJarUI: 'UI/Ink Jar' shader not found - disabling the ink jar.");
                enabled = false;
                return;
            }
            source = new Material(shader);
        }
        material = new Material(source);
        image.material = material;
    }

    private void Start()
    {
        if (playerBody == null)
        {
            PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
            if (player != null) playerBody = player.GetComponent<Rigidbody>();
        }
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

        if (InkManager.Instance != null) shownFill = InkManager.Instance.Normalized;
        TrySubscribe();
        ApplyStaticProperties(material);
    }

    private void OnDestroy()
    {
        if (subscribedInk != null) subscribedInk.OnInkChanged -= HandleInkChanged;
        if (material != null) Destroy(material);
    }

    private void TrySubscribe()
    {
        if (subscribedInk != null || InkManager.Instance == null) return;
        subscribedInk = InkManager.Instance;
        subscribedInk.OnInkChanged += HandleInkChanged;
        HandleInkChanged(subscribedInk.CurrentInk, subscribedInk.MaxInk);
    }

    private void HandleInkChanged(float current, float max)
    {
        if (inkText != null) inkText.text = Mathf.CeilToInt(current).ToString();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // game paused: freeze the ink where it is

        TrySubscribe();

        // --- Fill: ease toward the real ink amount ---
        float targetFill = InkManager.Instance != null ? InkManager.Instance.Normalized : 0f;
        shownFill = Mathf.Lerp(shownFill, targetFill, 1f - Mathf.Exp(-fillSmoothing * dt));

        // --- Slosh: damped spring pushed by sideways acceleration and camera turning ---
        float push = 0f;
        if (playerBody != null && cameraTransform != null)
        {
            Vector3 velocity = playerBody.linearVelocity;
            float yaw = cameraTransform.eulerAngles.y;

            if (hasLastFrame)
            {
                Vector3 acceleration = (velocity - lastVelocity) / dt;
                acceleration = Vector3.ClampMagnitude(acceleration, 50f); // ignore teleports (checkpoint loads)
                float sideways = Vector3.Dot(acceleration, cameraTransform.right);

                float yawRate = Mathf.Clamp(Mathf.DeltaAngle(lastYaw, yaw) / dt, -720f, 720f);

                // Liquid lags behind: speeding up to the right piles ink up on the left.
                push = -sideways * moveInfluence - yawRate * turnInfluence;
            }

            lastVelocity = velocity;
            lastYaw = yaw;
            hasLastFrame = true;
        }

        float springForce = push * stiffness - stiffness * tilt - damping * tiltVelocity;
        tiltVelocity += springForce * dt;
        tilt += tiltVelocity * dt;
        if (Mathf.Abs(tilt) > maxTilt)
        {
            tilt = Mathf.Sign(tilt) * maxTilt;
            tiltVelocity = 0f;
        }

        float waveAmp = Mathf.Min(idleWave + Mathf.Abs(tiltVelocity) * waveFromMotion, maxWave);
        wavePhase += waveSpeed * dt;

        // --- Push to the shader ---
        material.SetFloat(FillId, shownFill);
        material.SetFloat(TiltId, tilt);
        material.SetFloat(WaveAmpId, waveAmp);
        material.SetFloat(WavePhaseId, wavePhase);
        ApplyStaticProperties(material);
    }

    // Values that only change when you edit them in the Inspector.
    private void ApplyStaticProperties(Material target)
    {
        if (target == null) return;

        float height = 32f;
        if (image != null && image.sprite != null) height = image.sprite.rect.height;

        target.SetFloat(PixelGridId, pixelGrid);
        target.SetFloat(FillBottomId, Mathf.Clamp01(interiorBottomRow / height));
        target.SetFloat(FillTopId, Mathf.Clamp01((interiorTopRow + 1) / height));
        target.SetFloat(WaveFreqId, waveFrequency);
        target.SetColor(InkColorId, inkColor);
        target.SetColor(SurfaceColorId, surfaceColor);
    }

#if UNITY_EDITOR
    // Live preview in Edit mode: shows the jar full so you can line up the interior rows.
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        if (image == null) image = GetComponent<Image>();
        Material shared = image != null ? image.material : null;
        if (shared == null || shared.shader == null || shared.shader.name != "UI/Ink Jar") return;

        ApplyStaticProperties(shared);
        shared.SetFloat(FillId, 1f);
        shared.SetFloat(TiltId, 0f);
        shared.SetFloat(WaveAmpId, 0f);
    }
#endif
}
