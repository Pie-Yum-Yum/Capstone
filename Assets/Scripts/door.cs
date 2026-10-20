using System;
using SaveSystem;
using UnityEngine;
using UnityEngine.AI;

public class door : MonoBehaviour, ICheckpointSaveable
{
    [Serializable]
    public class DoorCheckpointState
    {
        public string doorName;
        public bool isOpen;
        public bool isLocked;
        public bool isBroken;
        public Vector3 hingeLocalRotation;
    }
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

    [Header("Audio")]
    [Tooltip("3D source on the door. Leave empty to create one.")]
    public AudioSource audioSource;
    [Tooltip("Plays the moment the door starts swinging open (handle + creak).")]
    public AudioClip openClip;
    [Tooltip("Plays when the door finishes swinging shut, so the latch 'clunk' lines up with it hitting the frame.")]
    public AudioClip closeClip;
    [Range(0f, 1f)] public float doorVolume = 0.8f;
    public Vector2 doorPitchRange = new Vector2(0.95f, 1.05f);

    [Header("Optional")]
    public bool invertOpenDirection = false;

    private Quaternion closedRotation;
    private Quaternion targetRotation;
    private bool isOpen;
    public bool lookingAtDoor = false;
    private bool isMoving;
    public GameObject lockObject;
    public bool isLocked;

    public bool IsOpen => isOpen;
    public bool IsLocked => isLocked;
    public Vector3 HingeLocalEuler => hinge != null ? hinge.localEulerAngles : transform.localEulerAngles;
    public string SaveId => CheckpointSaveIdUtility.GetStableSaveId(this);
    private bool suppressCollisionForLoad = false;

    private MaterialPropertyBlock propertyBlock;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private bool hasSpellHighlight;
    private Color spellHighlightColor = Color.black;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // fully 3D: you hear where the door is
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 1.5f;
            audioSource.maxDistance = 20f;
        }
    }

    private void PlayDoorSound(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.pitch = UnityEngine.Random.Range(doorPitchRange.x, doorPitchRange.y);
        audioSource.PlayOneShot(clip, doorVolume);
    }

    void Start()
    {
        if (hinge == null) hinge = transform;
        if (doorRenderer == null) doorRenderer = GetComponentInChildren<Renderer>();

        closedRotation = hinge.localRotation;
        targetRotation = closedRotation;

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        if (doorRenderer != null && doorRenderer.sharedMaterial != null)
        {
            doorRenderer.sharedMaterial.EnableKeyword("_EMISSION");
        }
        if (playerCamera == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                playerCamera = mainCamera.transform;
            }
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
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        doorRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(EmissionColorId, color * glowIntensity);
        doorRenderer.SetPropertyBlock(propertyBlock);
    }

    public void HighlightDoor(Color color, bool glow)
    {
        if (doorRenderer == null) return;
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

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
                OpenDoorAwayFromObject(playerCamera);
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

                if (!isOpen) PlayDoorSound(closeClip);
            }
        }
    }

    private void OpenDoorAwayFromObject(Transform Opener)
    {
        if (isOpen)
        {
            targetRotation = closedRotation;
            isOpen = false;
            isMoving = true;
            
            GetComponent<NavMeshObstacle>().enabled = false;
            return;
        }
        
        GetComponent<NavMeshObstacle>().enabled = true;

        Vector3 toPlayer = Opener.transform.position - hinge.position;
        float side = Vector3.Dot(hinge.forward, toPlayer);
        float direction = side >= 0f ? 1f : -1f;

        if (invertOpenDirection)
            direction *= -1f;

        targetRotation = closedRotation * Quaternion.AngleAxis(openAngle * direction, Vector3.up);
        isOpen = true;
        isMoving = true;
        PlayDoorSound(openClip);
    }

    public object CaptureState()
    {
        Baracade barrier = GetComponentInParent<Baracade>();
        return new DoorCheckpointState
        {
            doorName = name,
            isOpen = isOpen,
            isLocked = isLocked,
            isBroken = barrier != null && barrier.IsBroken,
            hingeLocalRotation = HingeLocalEuler
        };
    }

    public void RestoreState(object state)
    {
        if (!(state is DoorCheckpointState doorState))
        {
            return;
        }

        ApplySavedState(doorState.isOpen, doorState.isLocked, doorState.hingeLocalRotation, doorState.isBroken);
    }

    public void ApplySavedState(bool open, bool locked, Vector3 hingeLocalEuler, bool brokenState)
    {
        isOpen = open;
        isLocked = locked;
        isMoving = false;

        if (hinge != null)
        {
            Quaternion savedRotation = Quaternion.Euler(hingeLocalEuler);
            hinge.localRotation = savedRotation;
            closedRotation = savedRotation;
            targetRotation = savedRotation;
        }

        if (lockObject != null)
        {
            lockObject.SetActive(locked);
        }

        NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();
        if (obstacle != null)
        {
            obstacle.enabled = isLocked || isOpen;
        }

        if (brokenState && !isLocked && !isOpen)
        {
            isOpen = true;
            if (hinge != null)
            {
                targetRotation = closedRotation * Quaternion.AngleAxis(openAngle * (invertOpenDirection ? -1f : 1f), Vector3.up);
                hinge.localRotation = targetRotation;
            }
        }

        suppressCollisionForLoad = true;
        StartCoroutine(ResumeCollisionAfterLoad());
    }

    public void lockDoor() // door lock for the lock spell !!
    {
        Debug.Log($"Locking the door: {gameObject.name}");
        if (lockObject == null) return;
        isLocked = true;

        lockObject.SetActive(true);
        GetComponent<NavMeshObstacle>().enabled = true;
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
        
        GetComponent<NavMeshObstacle>().enabled = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (suppressCollisionForLoad)
        {
            return;
        }

        if (collision.gameObject.CompareTag("Monster") && !isLocked && !isOpen)
        {
            OpenDoorAwayFromObject(collision.gameObject.transform);
        }
    }

    private System.Collections.IEnumerator ResumeCollisionAfterLoad()
    {
        yield return new WaitForSeconds(0.2f);
        suppressCollisionForLoad = false;
    }
}