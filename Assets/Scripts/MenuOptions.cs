using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
//using UnityEngine.Rendering.Universal;

public class MenuOptions : MonoBehaviour
{
    
    
    
    [Header("Volume")]
    public float volumeLvl;
    public GameObject VolumeCurrentValue;
    public Slider volumeSlider; 
    
    [Header("Brightness")]
    public float brightnessLvl;
    public GameObject BrightnessCurrentValue;
    public Slider brightnessSlider;
    
    [Header("Sensitivity")]
    public float sensitivityLvl;
    public Slider sensitivitySlider;
    public GameObject SensitivityCurrentValue;
    
    [Header("Ascii")]
    public float AsciiAffector;
    public Slider AsciiSlider;
    public GameObject AsciiCurrentValue;


    [Header("Fullscreen")]
    public bool isFullscreen;

    [Header("Popups")]
    [Tooltip("Toggle that turns interact/tutorial popups on or off. Its On Value Changed should call SetPopupsEnabled (Dynamic bool).")]
    public Toggle popupsToggle;

    [Header("References")]
    public GameObject menuObject;
    public PlayerCam playerCam;
    [Tooltip("The ASCII post-process material (Materials/Ascii).")]
    public Material asciiMaterial;

    // ---- Sensitivity ----
    // Slider position is stored as 0..1 with 0.5 = the PlayerCam's current sensX/sensY.
    // Mapping is exponential (each half of the slider halves/doubles twice), which is standard
    // practice because perceived sensitivity is relative: 0.25x at the far left, 4x at the far right.
    private const string SensitivityPrefKey = "Settings.Sensitivity";
    private const float SensitivityRangeFactor = 4f;

    public static float SavedSensitivity => PlayerPrefs.GetFloat(SensitivityPrefKey, 0.5f);

    public static float SensitivityMultiplier(float slider01)
    {
        return Mathf.Pow(SensitivityRangeFactor, (Mathf.Clamp01(slider01) - 0.5f) * 2f);
    }

    // ---- Volume ----
    // Slider position is stored as 0..1 (works whatever min/max the slider uses) and drives
    // AudioListener.volume, i.e. every sound in the game. Squared because hearing is roughly
    // logarithmic: a linear mapping makes the top half of the slider feel like it does nothing.
    private const string VolumePrefKey = "Settings.Volume";

    public static float SavedVolume => PlayerPrefs.GetFloat(VolumePrefKey, 1f);

    public static void ApplyVolume(float slider01)
    {
        float v = Mathf.Clamp01(slider01);
        AudioListener.volume = v * v;
    }

    // Apply the saved volume as soon as the game starts, in every scene, even if no MenuOptions exists yet.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplySavedVolumeOnStartup()
    {
        ApplyVolume(SavedVolume);
    }

    // ---- ASCII ----
    // The "Screen Effect" slider drives the ASCII shader's luminance gamma.
    private const string AsciiGammaPrefKey = "Settings.AsciiGamma";
    private const float AsciiGammaMin = 0f;
    private const float AsciiGammaMax = 0.5f;
    private static readonly int GammaId = Shader.PropertyToID("_Gamma");
    private bool initializingSliders;

    private void Awake()
    {
        ApplySavedAsciiGamma();
    }

    // Also called by PlayerCam on level start, because this object starts inactive and its Awake
    // doesn't run until the settings menu is first opened.
    public void ApplySavedAsciiGamma()
    {
        if (asciiMaterial == null) return;

#if UNITY_EDITOR
        RestoreAsciiMaterialAfterPlayMode(asciiMaterial);
#endif
        float fallback = Mathf.Clamp(asciiMaterial.GetFloat(GammaId), AsciiGammaMin, AsciiGammaMax);
        AsciiAffector = Mathf.Clamp(PlayerPrefs.GetFloat(AsciiGammaPrefKey, fallback), AsciiGammaMin, AsciiGammaMax);
        asciiMaterial.SetFloat(GammaId, AsciiAffector);
    }

#if UNITY_EDITOR
    // In the Editor, runtime SetFloat writes straight into the Ascii.mat asset.
    // Put the original back so playing doesn't leave the material modified in git.
    private static Material restoringMaterial;

    private static void RestoreAsciiMaterialAfterPlayMode(Material material)
    {
        if (restoringMaterial == material) return;
        restoringMaterial = material;

        float original = material.GetFloat(GammaId);
        void Restore(UnityEditor.PlayModeStateChange change)
        {
            if (change != UnityEditor.PlayModeStateChange.ExitingPlayMode) return;
            material.SetFloat(GammaId, original);
            restoringMaterial = null;
            UnityEditor.EditorApplication.playModeStateChanged -= Restore;
        }
        UnityEditor.EditorApplication.playModeStateChanged += Restore;
    }
#endif

    private void Start()
    {
        // Changing a slider's range can fire On Value Changed, which would overwrite saved settings.
        initializingSliders = true;

        // Configure and fill the sliders without firing SliderValueChanged.
        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(Mathf.Lerp(volumeSlider.minValue, volumeSlider.maxValue, SavedVolume));
            volumeLvl = SavedVolume;
            if (VolumeCurrentValue != null) VolumeCurrentValue.GetComponent<TextMeshProUGUI>().text = (volumeLvl * 100).ToString("0");
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.SetValueWithoutNotify(Mathf.Lerp(sensitivitySlider.minValue, sensitivitySlider.maxValue, SavedSensitivity));
        }

        if (AsciiSlider != null)
        {
            AsciiSlider.wholeNumbers = false;
            AsciiSlider.minValue = AsciiGammaMin;
            AsciiSlider.maxValue = AsciiGammaMax;
            AsciiSlider.SetValueWithoutNotify(AsciiAffector);
            if (AsciiCurrentValue != null) AsciiCurrentValue.GetComponent<TextMeshProUGUI>().text = AsciiAffector.ToString("0.00");
        }

        if (popupsToggle != null) popupsToggle.SetIsOnWithoutNotify(PromptPopup.PromptsEnabled);

        initializingSliders = false;
    }

    // Hook the popups Toggle's On Value Changed up to this (pick it from the "Dynamic bool" list).
    public void SetPopupsEnabled(bool enabled)
    {
        PromptPopup.PromptsEnabled = enabled;
    }

    public void SliderValueChanged(){
        if (initializingSliders) return;

        // Called by every settings slider's On Value Changed. Each one is optional.
        if (volumeSlider != null)
        {
            volumeLvl = Mathf.InverseLerp(volumeSlider.minValue, volumeSlider.maxValue, volumeSlider.value);
            PlayerPrefs.SetFloat(VolumePrefKey, volumeLvl);
            ApplyVolume(volumeLvl);
        }
        if (brightnessSlider != null) { brightnessLvl = brightnessSlider.value; }

        if (sensitivitySlider != null)
        {
            sensitivityLvl = sensitivitySlider.value;
            PlayerPrefs.SetFloat(SensitivityPrefKey, sensitivityLvl);

            if (playerCam == null) playerCam = FindAnyObjectByType<PlayerCam>();
            if (playerCam != null) playerCam.sensitivityMultiplier = SensitivityMultiplier(sensitivityLvl);
        }

        if (AsciiSlider != null)
        {
            AsciiAffector = AsciiSlider.value;
            PlayerPrefs.SetFloat(AsciiGammaPrefKey, AsciiAffector);
            if (asciiMaterial != null) asciiMaterial.SetFloat(GammaId, AsciiAffector);
        }

        VolumeCurrentValue.GetComponent<TextMeshProUGUI>().text = (volumeLvl * 100).ToString("0");
        //BrightnessCurrentValue.GetComponent<TextMeshProUGUI>().text = brightnessLvl.ToString("0");
        SensitivityCurrentValue.GetComponent<TextMeshProUGUI>().text = (sensitivityLvl*100).ToString("0");
        AsciiCurrentValue.GetComponent<TextMeshProUGUI>().text = AsciiAffector.ToString("0.00");


    }
       
    public void ExitToMenu()
    {
        //load next scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(0); // check that the scene is the correct number
        //onValueChanged();
    }

    public void ToggleFullscreen()
    {
        // Toggle the fullscreen mode
        isFullscreen = !isFullscreen;
        Screen.fullScreen = isFullscreen;
    }

    // True while the settings menu has the game frozen. Other scripts (e.g. PlayerCam) check this.
    public static bool GamePaused { get; private set; }

    // Called by the "Menu" spell. Freezes everything driven by scaled time: the monster's
    // NavMeshAgent, physics, leg IK / tentacle / idle motion, and WaitForSeconds timers.
    // UI still works because the EventSystem runs on unscaled time.
    public void PauseGame()
    {
        if (GamePaused) return;
        GamePaused = true;

        if (playerCam == null) playerCam = FindAnyObjectByType<PlayerCam>();

        // Let the spellbook finish its close animation even though time is frozen.
        if (playerCam != null && playerCam.animator != null)
        {
            playerCam.animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (menuObject != null) menuObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Hook the settings menu's Resume button up to this.
    public void resumeGame()
    {
        GamePaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (menuObject != null) menuObject.SetActive(false);

        if (playerCam == null) playerCam = FindAnyObjectByType<PlayerCam>();
        if (playerCam != null)
        {
            if (playerCam.animator != null) playerCam.animator.updateMode = AnimatorUpdateMode.Normal;
            playerCam.paused = false;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Safety net: never leave the game frozen if this object is destroyed or the scene changes.
    private void OnDestroy()
    {
        PlayerPrefs.Save();

        if (GamePaused)
        {
            GamePaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }


    
    
}
