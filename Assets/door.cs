using UnityEngine;

public class door : MonoBehaviour
{
    [Header("References")]
    public Transform playerCamera;
    public Transform hinge;
    public Renderer doorRenderer;

    [Header("Interaction")]
    public float interactDistance = 3f;
    public float openAngle = 80f;
    public float openSpeed = 180f;
    public KeyCode interactKey = KeyCode.E;

    [Header("Glow")]
    public Color glowColor = new Color(0.8f, 0.8f, 0.2f, 1f);
    public float glowIntensity = 1.5f;

    [Header("Optional")]
    public bool invertOpenDirection = false;

    private Quaternion closedRotation;
    private Quaternion targetRotation;
    private bool isOpen;
    public bool lookingAtDoor = false;
    private bool isMoving;
    public GameObject lockObject;
    private bool isLocked;

    private MaterialPropertyBlock propertyBlock;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private bool hasSpellHighlight;
    private Color spellHighlightColor = Color.black;

    void Start()
    {
        if (hinge == null) hinge = transform;
        if (doorRenderer == null) doorRenderer = GetComponentInChildren<Renderer>();

        closedRotation = hinge.localRotation;
        targetRotation = closedRotation;

        propertyBlock = new MaterialPropertyBlock();
        isLocked = false;

        if (doorRenderer != null && doorRenderer.sharedMaterial != null)
        {
            doorRenderer.sharedMaterial.EnableKeyword("_EMISSION");
        }
    }

    public void SetSpellHighlight(bool enabled, Color color)
    {
        hasSpellHighlight = enabled;
        spellHighlightColor = color;

        if (enabled)
        {
            ApplyGlow(color);
        }
        else
        {
            HighlightDoor(glowColor, false);
        }
    }

    private void ApplyGlow(Color color)
    {
        if (doorRenderer == null) return;

        doorRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(EmissionColorId, color * glowIntensity);
        doorRenderer.SetPropertyBlock(propertyBlock);
    }

    public void HighlightDoor(Color color, bool glow)
    {
        if (doorRenderer == null) return;

        if (glow)
        {
            ApplyGlow(color);
        }
        else
        {
            doorRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorId, Color.black);
            doorRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    void Update()
    {
        if (playerCamera == null || hinge == null || doorRenderer == null)
            return;

        bool withinDistance = Vector3.Distance(playerCamera.position, transform.position) <= interactDistance;

        Ray ray = new Ray(playerCamera.position, playerCamera.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            lookingAtDoor = hit.transform == transform || hit.transform.IsChildOf(transform);
        }
        else
        {
            lookingAtDoor = false;
        }

        bool canInteract = withinDistance && lookingAtDoor && !isMoving && !isLocked;

        if (hasSpellHighlight)
        {
            ApplyGlow(spellHighlightColor);
        }
        else if (canInteract)
        {
            HighlightDoor(glowColor, true);

            if (Input.GetKeyDown(interactKey))
            {
                OpenDoorAwayFromPlayer();
            }
        }
        else if (!lookingAtDoor)
        {
            HighlightDoor(glowColor, false);
        }

        if (isMoving)
        {
            hinge.localRotation = Quaternion.RotateTowards(
                hinge.localRotation,
                targetRotation,
                openSpeed * Time.deltaTime
            );

            if (Quaternion.Angle(hinge.localRotation, targetRotation) < 0.1f)
            {
                hinge.localRotation = targetRotation;
                isMoving = false;
            }
        }
    }

    private void OpenDoorAwayFromPlayer()
    {
        if (isOpen)
        {
            targetRotation = closedRotation;
            isOpen = false;
            isMoving = true;
            return;
        }

        Vector3 toPlayer = playerCamera.position - hinge.position;
        float side = Vector3.Dot(hinge.right, toPlayer);
        float direction = side >= 0f ? -1f : 1f;

        if (invertOpenDirection)
            direction *= -1f;

        targetRotation = closedRotation * Quaternion.AngleAxis(openAngle * direction, Vector3.up);
        isOpen = true;
        isMoving = true;
    }

    public void lockDoor()
    {
        Debug.Log($"Locking the door: {gameObject.name}");
        if (lockObject == null) return;
        isLocked = true;

        lockObject.SetActive(true);
        StartCoroutine(UnlockAfterDelay(10f));
    }

    private System.Collections.IEnumerator UnlockAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (lockObject != null)
        {
            lockObject.SetActive(false);
        }
        isLocked = false;
    }
}