using UnityEngine;

// Bobs an object up and down and spins it. Put it on the pickup's model.
public class PickupHover : MonoBehaviour
{
    [Tooltip("How far it moves above and below its starting height.")]
    public float bobHeight = 0.1f;
    [Tooltip("Full up-and-down bobs per second.")]
    public float bobSpeed = 0.5f;
    [Tooltip("Degrees per second around the vertical axis.")]
    public float spinSpeed = 60f;

    private Vector3 startLocalPosition;
    private float phase;

    private void Awake()
    {
        startLocalPosition = transform.localPosition;
        phase = Random.value * Mathf.PI * 2f; // so several pickups don't bob in sync
    }

    private void Update()
    {
        float offset = Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f + phase) * bobHeight;
        transform.localPosition = startLocalPosition + Vector3.up * offset;
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
    }
}
