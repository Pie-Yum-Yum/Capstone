using System;
using SaveSystem;
using SpellSystem;
using UnityEngine;

public class NoteBookPage : MonoBehaviour, ICheckpointSaveable
{
    [Serializable]
    public class PickupCheckpointState
    {
        public string pickupName;
        public string spellName;
        public bool isCollected;
    }
    public Transform playerCamera;
    public SpellDrawingController spellDrawingController;
    public string SpellName;
    public float interactDistance = 3f;
    public KeyCode interactKey = KeyCode.E;
    private bool isPlayerLookingAtPage = false;
    private Material material;
    private Renderer objectRenderer;
    private bool isCollected = false;

    private void SetPageColor(Color color)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && materials[i].HasProperty("_Albedo"))
                {
                    materials[i].SetColor("_Albedo", color);
                }
            }
        }
    }

    public bool IsCollected => isCollected;
    public string SaveId => CheckpointSaveIdUtility.GetStableSaveId(this);

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        EnsureRendererAndMaterial();
        ResetVisualState();
        if (gameObject.activeSelf && isCollected)
        {
            gameObject.SetActive(false);
        }
    }

    private void EnsureRendererAndMaterial()
    {
        if (objectRenderer == null)
        {
            objectRenderer = GetComponent<Renderer>();
        }

        if (objectRenderer != null)
        {
            material = objectRenderer.material;
        }
    }

    public object CaptureState()
    {
        return new PickupCheckpointState
        {
            pickupName = name,
            spellName = SpellName,
            isCollected = isCollected
        };
    }

    public void RestoreState(object state)
    {
        if (!(state is PickupCheckpointState pickupState))
        {
            return;
        }

        ApplySavedState(pickupState.isCollected, pickupState.spellName);
    }

    public void ApplySavedState(bool collected, string savedSpellName)
    {
        isCollected = collected;

        if (!string.IsNullOrEmpty(savedSpellName))
        {
            SpellName = savedSpellName;
        }

        DissolveObject dissolveObject = GetComponent<DissolveObject>();
        if (dissolveObject != null)
        {
            dissolveObject.ApplySavedState(collected);
            if (!collected && dissolveObject.ParentObject != null)
            {
                dissolveObject.ParentObject.SetActive(true);
            }
        }

        if (collected)
        {
            if (dissolveObject != null && dissolveObject.ParentObject != null)
            {
                dissolveObject.ParentObject.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
            return;
        }

        if (dissolveObject != null && dissolveObject.ParentObject != null)
        {
            dissolveObject.ParentObject.SetActive(true);
        }

        gameObject.SetActive(true);
        SetPageColor(Color.white);
        ResetVisualState();
    }

    public void ResetVisualState()
    {
        EnsureRendererAndMaterial();
        SetPageColor(Color.white);

        DissolveObject dissolveObject = GetComponent<DissolveObject>();
        if (dissolveObject != null)
        {
            dissolveObject.ResetToOriginalCutoff();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (isCollected || playerCamera == null)
        {
            return;
        }

        bool withinDistance = Vector3.Distance(playerCamera.position, transform.position) <= interactDistance;
        if (withinDistance)
        {
            Ray ray = new Ray(playerCamera.position, playerCamera.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
            {
                isPlayerLookingAtPage = hit.transform == transform || hit.transform.IsChildOf(transform);
                SetPageColor(Color.yellow);
            }
            
            
            if (isPlayerLookingAtPage && Input.GetKeyDown(interactKey))
            {
                CollectPage();
            }
        }
        else
            {
                isPlayerLookingAtPage = false;
                SetPageColor(Color.white);
            }
    }

    private void CollectPage()
    {
        if (isCollected)
        {
            return;
        }

        isCollected = true;
        if (spellDrawingController != null)
        {
            spellDrawingController.UnlockSpell(SpellName);
        }

        Debug.Log($"Unlocked spell: {SpellName}");
        //SetPageColor(Color.blue);

        if (TryGetComponent<DissolveObject>(out DissolveObject dissolveObject))
        {
            dissolveObject.TriggerDissolve();
            StartCoroutine(DisableAfterDissolve());
            return;
        }

        gameObject.SetActive(false);
    }

    private System.Collections.IEnumerator DisableAfterDissolve()
    {
        yield return new WaitForSeconds(1.7f);
        gameObject.SetActive(false);
    }
}
