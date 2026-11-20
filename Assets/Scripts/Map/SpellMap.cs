using UnityEngine;
using UnityEngine.UI;

// The Eye spell's map. Open() (called by SpellCaster when Eye is cast) raises the hands and inks the map in
// from the player outwards; Close() (Esc, via PlayerCam) lowers the hands and smudges it away. While it's
// open the player moves at crouch speed and can't sprint. No time limit.
//
// SETUP: on the overlay Canvas (the one above the ASCII filter) add a square RawImage, give it a material that
// uses the "UI/Spell Map" shader, and add this. Keep the object active - the image hides itself.
// The Player needs a MapExplorer with the level's MapLayout.
[RequireComponent(typeof(RawImage))]
public class SpellMap : MonoBehaviour
{
    [Header("View")]
    [Tooltip("How many metres from the player to the edge of the map circle.")]
    public float viewRadius = 15f;

    [Header("Timing")]
    [Tooltip("Seconds for the ink to spread from the player to the edge.")]
    public float drawDuration = 0.75f;
    [Tooltip("Seconds for the map to smudge away after closing.")]
    public float smudgeDuration = 0.6f;

    [Header("Hands")]
    [Tooltip("Animator for the hands. Optional.")]
    public Animator handsAnimator;
    [Tooltip("Trigger fired when the map opens. Leave empty until the animation exists.")]
    public string raiseTrigger = "";
    [Tooltip("Trigger fired when the map closes. Leave empty until the animation exists.")]
    public string lowerTrigger = "";
    [Tooltip("Other triggers on the hands animator (cast, open/close book...). Cleared whenever the map raises or lowers so they can't fight it.")]
    public string[] otherHandTriggers = { "Trigger1", "Trigger2", "Trigger3" };

    [Header("Sound")]
    [Tooltip("Optional. Played when the map starts drawing.")]
    public AudioClip openClip;
    [Tooltip("Optional. Played when the map smudges away.")]
    public AudioClip closeClip;
    [Range(0f, 1f)] public float volume = 1f;

    [Header("References")]
    [Tooltip("Auto-found if left empty.")]
    public PlayerCam playerCam;
    [Tooltip("Auto-found if left empty.")]
    public PlayerMovement playerMovement;

    private static readonly int RevealTexId = Shader.PropertyToID("_RevealTex");
    private static readonly int PlayerPosId = Shader.PropertyToID("_PlayerPos");
    private static readonly int YawId = Shader.PropertyToID("_Yaw");
    private static readonly int ViewRadiusId = Shader.PropertyToID("_ViewRadius");
    private static readonly int WorldMinId = Shader.PropertyToID("_WorldMin");
    private static readonly int WorldSizeId = Shader.PropertyToID("_WorldSize");
    private static readonly int DrawId = Shader.PropertyToID("_Draw");
    private static readonly int SmudgeId = Shader.PropertyToID("_Smudge");

    private RawImage image;
    private Material material;
    private AudioSource audioSource;
    private MapExplorer explorer;
    private bool isOpen;
    private float draw;
    private float smudge = 1f;

    public bool IsOpen => isOpen;

    /// <summary>True when opening plays its own hands animation, so the caster shouldn't fire the normal cast one.</summary>
    public bool HasRaiseAnimation => handsAnimator != null && !string.IsNullOrEmpty(raiseTrigger);

    /// <summary>
    /// Clears any raise/lower trigger that hasn't been used yet. A trigger stays set until a transition consumes
    /// it, so a leftover one would hijack the next animation (e.g. the normal cast).
    /// </summary>
    public void ClearHandTriggers()
    {
        if (handsAnimator == null) return;
        if (!string.IsNullOrEmpty(raiseTrigger)) handsAnimator.ResetTrigger(raiseTrigger);
        if (!string.IsNullOrEmpty(lowerTrigger)) handsAnimator.ResetTrigger(lowerTrigger);
    }

    private void Awake()
    {
        image = GetComponent<RawImage>();
        image.raycastTarget = false;

        // Work on a copy so playing never edits the material asset.
        Material source = image.material;
        if (source == null || source.shader == null || source.shader.name != "UI/Spell Map")
        {
            Debug.LogWarning("SpellMap: assign a material using the 'UI/Spell Map' shader to this RawImage.");
            Shader shader = Shader.Find("UI/Spell Map");
            if (shader == null)
            {
                Debug.LogError("SpellMap: 'UI/Spell Map' shader not found - disabling the map.");
                enabled = false;
                return;
            }
            source = new Material(shader);
        }
        material = new Material(source);
        image.material = material;
        image.enabled = false;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void Start()
    {
        if (playerCam == null) playerCam = FindAnyObjectByType<PlayerCam>();
        if (playerMovement == null) playerMovement = FindAnyObjectByType<PlayerMovement>();
        explorer = MapExplorer.Instance;

        if (explorer != null && explorer.Layout != null)
        {
            MapLayout layout = explorer.Layout;
            image.texture = layout.texture;
            material.SetTexture(RevealTexId, explorer.RevealTexture);
            material.SetVector(WorldMinId, new Vector4(layout.worldMin.x, layout.worldMin.y, 0f, 0f));
            material.SetVector(WorldSizeId, new Vector4(layout.worldSize.x, layout.worldSize.y, 0f, 0f));
        }
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }

    /// <summary>Raises the hands and draws the map. Returns false if there's no map for this level.</summary>
    public bool Open()
    {
        if (isOpen) return true;
        if (explorer == null || explorer.Layout == null || explorer.RevealTexture == null)
        {
            Debug.LogWarning("SpellMap: Eye cast but this level has no MapExplorer/MapLayout set up.");
            return false;
        }

        isOpen = true;
        draw = 0f;
        smudge = 0f;
        FireTrigger(raiseTrigger);
        PlayClip(openClip);
        if (playerMovement != null) playerMovement.forceCrouchSpeed = true;
        return true;
    }

    /// <summary>Lowers the hands and smudges the map away.</summary>
    public void Close()
    {
        if (!isOpen) return;

        isOpen = false;
        FireTrigger(lowerTrigger);
        PlayClip(closeClip);
        if (playerMovement != null) playerMovement.forceCrouchSpeed = false;
    }

    private void Update()
    {
        // Scaled time, so it freezes along with everything else in the settings menu.
        float dt = Time.deltaTime;
        if (isOpen)
        {
            draw = Mathf.MoveTowards(draw, 1f, dt / Mathf.Max(drawDuration, 0.0001f));
        }
        else if (smudge < 1f)
        {
            smudge = Mathf.MoveTowards(smudge, 1f, dt / Mathf.Max(smudgeDuration, 0.0001f));
        }

        bool visible = (isOpen || smudge < 1f) && !MenuOptions.GamePaused && !DeathScreen.IsShowing;
        image.enabled = visible;
        if (!visible) return;

        Vector3 position = explorer.transform.position;
        float yaw = playerCam != null ? playerCam.transform.eulerAngles.y : 0f;

        material.SetVector(PlayerPosId, new Vector4(position.x, position.z, 0f, 0f));
        material.SetFloat(YawId, yaw * Mathf.Deg2Rad);
        material.SetFloat(ViewRadiusId, viewRadius);
        material.SetFloat(DrawId, draw);
        material.SetFloat(SmudgeId, smudge);
    }

    private void OnDisable()
    {
        // Never leave the player stuck at crouch speed.
        if (isOpen && playerMovement != null) playerMovement.forceCrouchSpeed = false;
        isOpen = false;
    }

    private void FireTrigger(string trigger)
    {
        if (handsAnimator == null || string.IsNullOrEmpty(trigger)) return;
        // Only one hands animation may be queued at a time, or they fight over which plays.
        ClearHandTriggers();
        if (otherHandTriggers != null)
        {
            foreach (string other in otherHandTriggers) ResetIfExists(other);
        }
        handsAnimator.SetTrigger(trigger);
    }

    // Skips names the animator doesn't have, so a different hands rig doesn't spam warnings.
    private void ResetIfExists(string trigger)
    {
        if (string.IsNullOrEmpty(trigger)) return;
        foreach (AnimatorControllerParameter parameter in handsAnimator.parameters)
        {
            if (parameter.name == trigger) { handsAnimator.ResetTrigger(trigger); return; }
        }
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip != null) audioSource.PlayOneShot(clip, volume);
    }
}
