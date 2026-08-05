using UnityEngine;

public class PlayerMovement : MonoBehaviour
{

    public float moveSpeed;
    public float crouchSpeedMultiplier = 0.6f;
    public float sprintSpeedMultiplier = 1.35f;
    public float speedLerpTime = 8f;

    public GameObject CameraPos;

    public float drag;
    public Transform oreintation;
    
    float horizontalInput;
    float verticalInput;
    public bool allowMovement = true;
    float baseMoveSpeed;
    float targetMoveSpeed;
    public bool isCrouching;
    bool isSprinting;
    public bool hidingSpot;
    

    Vector3 moveDirection;
    Rigidbody rb;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        baseMoveSpeed = moveSpeed;
        targetMoveSpeed = moveSpeed;
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
        isCrouching = Input.GetKey(KeyCode.LeftControl);
        isSprinting = Input.GetKey(KeyCode.LeftShift) && !isCrouching;
        if (hidingSpot)
        {
            isCrouching = true;
        }

        Vector3 targetCameraPos = new Vector3(0, 0.8f, 0);
        if (isCrouching && !hidingSpot)
        {
            targetCameraPos = new Vector3(0, 0.4f, 0);
        }
        if(hidingSpot)
        {
            targetCameraPos = new Vector3(0, 0.05f, 0);
        }
        CameraPos.transform.localPosition = Vector3.Lerp(CameraPos.transform.localPosition, targetCameraPos, Time.deltaTime * speedLerpTime);

        float targetMultiplier = 1f;
        if (isCrouching)
        {
            targetMultiplier = crouchSpeedMultiplier;
        }
        else if (isSprinting)
        {
            targetMultiplier = sprintSpeedMultiplier;
        }

        targetMoveSpeed = baseMoveSpeed * targetMultiplier;
        moveSpeed = Mathf.Lerp(moveSpeed, targetMoveSpeed, Time.deltaTime * speedLerpTime);
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

    // private void OnTriggerEnter(Collider other){
    //     if(other.CompareTag("HidingSpot")){
    //         isCrouching = true;
    //         hidingSpot = true;
    //     }
    // }
    private void OnTriggerExit(Collider other){
        if(other.CompareTag("HidingSpot")){
            isCrouching = false;
            hidingSpot = false;
        }
    }
    private void OnTriggerStay(Collider other){
        if(other.CompareTag("HidingSpot")){
            isCrouching = true;
            hidingSpot = true;
        }
    }
}
