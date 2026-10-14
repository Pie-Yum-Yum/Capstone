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
    public Animator animator;
    public SpellDrawingController SpellDrawingController;
    public bool flashlightOn = false;
    public GameObject flashlight;



    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        sensitivityMultiplier = MenuOptions.SensitivityMultiplier(MenuOptions.SavedSensitivity);
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape) && MenuOptions.GamePaused){
            // Settings menu is open: Esc acts like its Resume button.
            MenuOptions menuOptions = FindAnyObjectByType<MenuOptions>(FindObjectsInactive.Include);
            if(menuOptions != null) menuOptions.resumeGame();
            return;
        }
        if(Input.GetKeyDown(KeyCode.Escape)){
            paused = !paused;
            if(paused){
                animator.SetTrigger("Trigger1");
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                
            }else{
                animator.SetTrigger("Trigger3");
                SpellDrawingController.ClearPage();
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
            flashlightOn = !flashlightOn;
            flashlight.SetActive(flashlightOn);
        }
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); 

        transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        orientation.rotation = Quaternion.Euler(0, yRotation, 0);

    }
}
