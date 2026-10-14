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

    // ---- ASCII ----
    private const string AsciiPrefKey = "Settings.AsciiCharCount";
    private const int AsciiMinChars = 5;
    private const int AsciiMaxChars = 20;
    private static readonly int CharCountId = Shader.PropertyToID("_CharCount");
    private float originalCharCount;

    private void Awake()
    {
        if (asciiMaterial != null)
        {
            originalCharCount = asciiMaterial.GetFloat(CharCountId);
            float savedChars = PlayerPrefs.GetFloat(AsciiPrefKey, Mathf.Clamp(originalCharCount, AsciiMinChars, AsciiMaxChars));
            asciiMaterial.SetFloat(CharCountId, savedChars);
        }
    }

    private void Start()
    {
        // Configure and fill the sliders without firing SliderValueChanged.
        if (sensitivitySlider != null)
        {
            sensitivitySlider.SetValueWithoutNotify(Mathf.Lerp(sensitivitySlider.minValue, sensitivitySlider.maxValue, SavedSensitivity));
        }

        if (AsciiSlider != null)
        {
            AsciiSlider.minValue = AsciiMinChars;
            AsciiSlider.maxValue = AsciiMaxChars;
            AsciiSlider.wholeNumbers = true;
            if (asciiMaterial != null) AsciiSlider.SetValueWithoutNotify(asciiMaterial.GetFloat(CharCountId));
        }
    }

    public void SliderValueChanged(){
        // Called by every settings slider's On Value Changed. Each one is optional.
        if (volumeSlider != null) { volumeLvl = volumeSlider.value; }
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
            PlayerPrefs.SetFloat(AsciiPrefKey, AsciiAffector);
            if (asciiMaterial != null) asciiMaterial.SetFloat(CharCountId, AsciiAffector);
        }

        VolumeCurrentValue.GetComponent<TextMeshProUGUI>().text = volumeLvl.ToString("0");
        //BrightnessCurrentValue.GetComponent<TextMeshProUGUI>().text = brightnessLvl.ToString("0");
        SensitivityCurrentValue.GetComponent<TextMeshProUGUI>().text = (sensitivityLvl*100).ToString("0");
        AsciiCurrentValue.GetComponent<TextMeshProUGUI>().text = AsciiAffector.ToString("0");


    }
       
    public void ExitToMenu()
    {
        //load next scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(1); // check that the scene is the correct number
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

#if UNITY_EDITOR
        // In the Editor, runtime SetFloat writes straight into the Ascii.mat asset.
        // Put the original back so playing doesn't leave the material modified in git.
        if (asciiMaterial != null) asciiMaterial.SetFloat(CharCountId, originalCharCount);
#endif

        if (GamePaused)
        {
            GamePaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }


    
    
}
