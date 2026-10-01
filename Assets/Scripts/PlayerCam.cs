using SpellSystem;
using UnityEngine;


public class PlayerCam : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public float sensX;
    public float sensY;

    public Transform orientation;

    float xRotation;
    float yRotation;
    public bool paused;
    public Animator animator;
    public SpellDrawingController SpellDrawingController;



    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
    }

    // Update is called once per frame
    void Update()
    {
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
        if(!paused){
            float mouseX = Input.GetAxisRaw("Mouse X") * Time.deltaTime * sensX;
            float mousey = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * sensY;

            yRotation += mouseX;
            xRotation -= mousey;
        }
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); 

        transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        orientation.rotation = Quaternion.Euler(0, yRotation, 0);

    }
}
