using UnityEngine;

public class PlayerMovement : MonoBehaviour
{

    public float moveSpeed;

    public GameObject CameraPos;

    public float drag;
    public Transform oreintation;
    
    float horizontalInput;
    float verticalInput;
    public bool allowMovement = true;

    Vector3 moveDirection;
    Rigidbody rb;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
    }
    private void myInput(){
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");
    }

    // Update is called once per frame
    void Update()
    {
        myInput();
        rb.linearDamping = drag;
        if(Input.GetKeyDown(KeyCode.LeftControl)){
            CameraPos.transform.localPosition = new Vector3(0, -0.25f, 0);
        }else if(Input.GetKeyUp(KeyCode.LeftControl)){
            CameraPos.transform.localPosition = new Vector3(0, 0.25f, 0);
        }
    }
    private void FixedUpdate()
    {
        if(allowMovement){
            movePlayer();
        }
    }

    private void movePlayer(){
        moveDirection = oreintation.forward * verticalInput + oreintation.right * horizontalInput;
        rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
    }
}
