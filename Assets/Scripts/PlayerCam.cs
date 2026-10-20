using SpellSystem;
using UnityEngine;


public class PlayerCam : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    // Base sensitivity = the 50% setting on the settings slider. Degrees per unit of mouse movement
    // (frame-rate independent). 6.67 matches the old 400 * deltaTime feel at 60 FPS.
    public float sensX = 6.67f;
    public float sensY = 6.67f;
    // Set by MenuOptions from the sensitivity slider (1 = default).
    [HideInInspector] public float sensitivityMultiplier = 1f;

    public Transform orientation;

    float xRotation;
    float yRotation;
    public bool paused;
    [Tooltip("Opens/closes the settings menu directly, without drawing the Menu spell.")]
    public KeyCode settingsMenuKey = KeyCode.Tab;
    public Animator animator;
    public SpellDrawingController SpellDrawingController;
    [Tooltip("Optional. Plays book open/close sounds. Auto-found if left empty.")]
    public SpellbookAudio spellbookAudio;

    [Header("Flashlight")]
    public GameObject flashlight;
    [Tooltip("Whether the flashlight is on when the level starts. The flashlight object is set to match on Start, " +
             "so it no longer matters whether it's active in the scene.")]
    public bool flashlightStartsOn = false;
    [Tooltip("Click when turning on. If the off clip is empty this one is used for both.")]
    public AudioClip flashlightOnClip;
    public AudioClip flashlightOffClip;
    [Range(0f, 1f)] public float flashlightVolume = 0.7f;
    // Not serialized: the old serialized value could disagree with the object's real state,
    // which made the first F press "turn off" a light that was already off.
    [System.NonSerialized] public bool flashlightOn;
    private AudioSource flashlightAudio;



    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        sensitivityMultiplier = MenuOptions.SensitivityMultiplier(MenuOptions.SavedSensitivity);

        if (spellbookAudio == null) spellbookAudio = FindAnyObjectByType<SpellbookAudio>(FindObjectsInactive.Include);

        flashlightOn = flashlightStartsOn;
        if (flashlight != null) flashlight.SetActive(flashlightOn);
        flashlightAudio = gameObject.AddComponent<AudioSource>();
        flashlightAudio.playOnAwake = false;
        flashlightAudio.spatialBlend = 0f;
    }

    private void ToggleFlashlight()
    {
        flashlightOn = !flashlightOn;
        if (flashlight != null) flashlight.SetActive(flashlightOn);

        AudioClip clip = flashlightOn || flashlightOffClip == null ? flashlightOnClip : flashlightOffClip;
        if (clip != null)
        {
            flashlightAudio.pitch = Random.Range(0.95f, 1.05f);
            flashlightAudio.PlayOneShot(clip, flashlightVolume);
        }
    }

    private void CloseBook()
    {
        animator.SetTrigger("Trigger3");
        SpellDrawingController.ClearPage();
        if (spellbookAudio != null) spellbookAudio.PlayClose();
    }

    // Update is called once per frame
    void Update()
    {
        if((Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(settingsMenuKey)) && MenuOptions.GamePaused){
            // Settings menu is open: Esc or the menu key acts like its Resume button.
            MenuOptions menuOptions = FindAnyObjectByType<MenuOptions>(FindObjectsInactive.Include);
            if(menuOptions != null) menuOptions.resumeGame();
            return;
        }
        if(Input.GetKeyDown(settingsMenuKey)){
            // Open the settings menu directly (no spell / ink needed).
            MenuOptions menuOptions = FindAnyObjectByType<MenuOptions>(FindObjectsInactive.Include);
            if(menuOptions != null){
                if(paused){
                    // Close the spellbook first, same as pressing Esc.
                    CloseBook();
                }
                menuOptions.PauseGame();
            }
            return;
        }
        if(Input.GetKeyDown(KeyCode.Escape)){
            paused = !paused;
            if(paused){
                animator.SetTrigger("Trigger1");
                if (spellbookAudio != null) spellbookAudio.PlayOpen();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

            }else{
                CloseBook();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        if(!paused && !MenuOptions.GamePaused){
            // Mouse axes are already "movement since last frame", so no Time.deltaTime here -
            // multiplying by it made sensitivity change with frame rate.
            float mouseX = Input.GetAxisRaw("Mouse X") * sensX * sensitivityMultiplier;
            float mousey = Input.GetAxisRaw("Mouse Y") * sensY * sensitivityMultiplier;

            yRotation += mouseX;
            xRotation -= mousey;
        }
        if (Input.GetKeyDown(KeyCode.F))
        {
            ToggleFlashlight();
        }
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); 

        transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        orientation.rotation = Quaternion.Euler(0, yRotation, 0);

    }
}
