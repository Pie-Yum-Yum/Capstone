using SpellSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Shown when the monster catches the player. Freezes the game where it is (time, sound, all player input)
// and offers three buttons: restart from the last checkpoint, restart the level, or quit to the main menu.
//
// SETUP: on the overlay Canvas, make a full-screen panel (an Image with Raycast Target on, so clicks can't
// reach anything under it) as the LAST child so it draws on top, with the three Buttons inside it.
// Put this script on an object that stays active (e.g. the Canvas) and drag the panel into Panel.
// Hook the buttons' On Click to RestartFromCheckpoint, RestartLevel and QuitToMenu.
public class DeathScreen : MonoBehaviour
{
    public static DeathScreen Instance { get; private set; }
    public static bool IsShowing { get; private set; }

    // The click that presses a button is also a game click; keep input blocked for the rest of that frame
    // so it can't, say, throw a primed fireball the moment time resumes.
    private static int hiddenFrame = -1;
    /// <summary>True while dead (and for the frame the screen closes). Input scripts check this.</summary>
    public static bool InputBlocked => IsShowing || Time.frameCount <= hiddenFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        IsShowing = false;
        hiddenFrame = -1;
    }

    [Header("UI")]
    [Tooltip("The death screen panel. Hidden until the player dies.")]
    public GameObject panel;
    [Tooltip("Greyed out when there's no checkpoint save to go back to.")]
    public Button checkpointButton;
    [Tooltip("Seconds the screen takes to fade in.")]
    public float fadeInDuration = 0.75f;

    [Header("Scenes")]
    public string menuSceneName = "MainMenu";

    [Header("Sound")]
    [Tooltip("Optional. Plays when the screen appears, even though game sound is paused.")]
    public AudioClip deathClip;
    [Range(0f, 1f)] public float deathVolume = 1f;

    [Header("References")]
    [Tooltip("Auto-found if left empty.")]
    public PlayerCam playerCam;

    private CanvasGroup canvasGroup;
    private AudioSource audioSource;
    private Transform player;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("More than one DeathScreen in the scene - only the first one is used.");
            return;
        }
        Instance = this;

        if (panel != null)
        {
            canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = panel.AddComponent<CanvasGroup>();
            panel.SetActive(false);
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;
    }

    private void Start()
    {
        if (playerCam == null) playerCam = FindAnyObjectByType<PlayerCam>();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        // Never leave the game frozen if the scene goes away some other way.
        if (IsShowing) Unfreeze();
    }

    private void Update()
    {
        if (!IsShowing || canvasGroup == null || canvasGroup.alpha >= 1f) return;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1f, Time.unscaledDeltaTime / Mathf.Max(fadeInDuration, 0.0001f));
    }

    /// <summary>Called by the monster when it catches the player.</summary>
    public void Show(Transform caughtPlayer)
    {
        if (IsShowing) return;
        IsShowing = true;
        player = caughtPlayer;

        // Tidy up so a checkpoint restart starts clean.
        if (playerCam != null) playerCam.ForceCloseBook();
        SpellMap map = FindAnyObjectByType<SpellMap>();
        if (map != null) map.Close();
        SpellCaster caster = FindAnyObjectByType<SpellCaster>();
        if (caster != null) caster.CancelPrimedSpell();

        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (checkpointButton != null)
        {
            checkpointButton.interactable = GameSaveManager.Instance != null && GameSaveManager.Instance.HasSave();
        }

        if (panel != null)
        {
            panel.SetActive(true);
            canvasGroup.alpha = fadeInDuration > 0f ? 0f : 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        else
        {
            Debug.LogWarning("DeathScreen: no Panel assigned - reloading the last checkpoint instead.");
            RestartFromCheckpoint();
            return;
        }

        if (deathClip != null) audioSource.PlayOneShot(deathClip, deathVolume);
    }

    // ---- Buttons ----

    public void RestartFromCheckpoint()
    {
        Unfreeze();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (GameSaveManager.Instance != null && player != null && GameSaveManager.Instance.TryLoadLatestSave(player)) return;
        LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void RestartLevel()
    {
        Unfreeze();
        LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitToMenu()
    {
        Unfreeze();
        // The level locks the cursor and the main menu doesn't undo that itself.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(menuSceneName);
    }

    private static void LoadScene(int buildIndex)
    {
        SceneManager.LoadScene(buildIndex);
    }

    private void Unfreeze()
    {
        IsShowing = false;
        hiddenFrame = Time.frameCount;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (panel != null) panel.SetActive(false);
    }
}
