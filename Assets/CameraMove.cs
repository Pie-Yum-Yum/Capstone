using UnityEngine;

public class CameraMove : MonoBehaviour
{
    public Transform CameraPosition;

    // Update is called once per frame
    void Update()
    {
        transform.position = CameraPosition.position;
    }
}
