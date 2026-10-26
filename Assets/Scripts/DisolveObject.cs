using System;
using System.Collections;
using SaveSystem;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class DissolveObject : MonoBehaviour, ICheckpointSaveable
{
    [Serializable]
    public class BreakableCheckpointState
    {
        public string objectName;
        public bool isDestroyed;
    }
    [SerializeField] private float noiseStrength = 0.25f;
    [SerializeField] private float dissolveDuration = 1.5f;

    private Material material;
    private Renderer objectRenderer;
    private bool isDissolving = false;
    private bool isDestroyed = false;
    private float originalCutoffHeight;
    public GameObject ParentObject;

    [Header("Audio (optional)")]
    [Tooltip("Plays when the dissolve starts. Leave empty for a silent dissolve.")]
    [SerializeField] private AudioClip dissolveClip;
    [SerializeField, Range(0f, 1f)] private float dissolveVolume = 0.8f;
    [Tooltip("Silent beyond this distance.")]
    [SerializeField] private float dissolveSoundMaxDistance = 20f;

    public string SaveId => CheckpointSaveIdUtility.GetStableSaveId(this);
    public bool IsDestroyed => isDestroyed;

    private void Awake()
    {
        EnsureRendererAndMaterial();
        material.SetFloat("_NoiseStrength", noiseStrength);

        // Cutoff above the object's actual world-space top = fully solid/visible
        originalCutoffHeight = GetTopHeight() + 2f;
        SetHeight(originalCutoffHeight);
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

    private float GetTopHeight()
    {
        return objectRenderer.bounds.max.y + noiseStrength;
    }

    private float GetBottomHeight()
    {
        return objectRenderer.bounds.min.y - noiseStrength;
    }

    private IEnumerator DissolveRoutine()
    {
        isDissolving = true;

        // Start above the object (solid) and animate down to below it (gone)
        float startHeight = GetTopHeight();
        float endHeight = GetBottomHeight();

        float elapsed = 0f;
        while (elapsed < dissolveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dissolveDuration;
            SetHeight(Mathf.Lerp(startHeight, endHeight, t));
            yield return null;
        }

        SetHeight(endHeight);
        isDestroyed = true;
        if (ParentObject != null)
        {
            ParentObject.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
        isDissolving = false;
    }

    public object CaptureState()
    {
        return new BreakableCheckpointState
        {
            objectName = name,
            isDestroyed = isDestroyed
        };
    }

    public void RestoreState(object state)
    {
        if (!(state is BreakableCheckpointState breakableState))
        {
            return;
        }

        ApplySavedState(breakableState.isDestroyed);
    }

    public void TriggerDissolve()
    {
        if (isDissolving || isDestroyed) return;
        PlayDissolveSound();
        StartCoroutine(DissolveRoutine());
    }

    // Plays from a temporary object, because this one is deactivated when the dissolve ends and
    // that would cut the sound off.
    private void PlayDissolveSound()
    {
        if (dissolveClip == null) return;

        GameObject soundObject = new GameObject($"{name} Dissolve Sound");
        soundObject.transform.position = objectRenderer != null ? objectRenderer.bounds.center : transform.position;

        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.clip = dissolveClip;
        source.volume = dissolveVolume;
        source.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1.5f;
        source.maxDistance = dissolveSoundMaxDistance;
        source.Play();

        Destroy(soundObject, dissolveClip.length / source.pitch + 0.1f);
    }

    public void ResetToOriginalCutoff()
    {
        EnsureRendererAndMaterial();
        isDissolving = false;
        isDestroyed = false;

        if (objectRenderer != null && objectRenderer.bounds.size.sqrMagnitude > 0f)
        {
            originalCutoffHeight = GetTopHeight() + 2f;
        }

        SetHeight(originalCutoffHeight);
    }

    public void ApplySavedState(bool destroyed)
    {
        EnsureRendererAndMaterial();
        isDestroyed = destroyed;
        if (destroyed)
        {
            if (ParentObject != null)
            {
                ParentObject.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
            SetHeight(GetBottomHeight());
            return;
        }

        if (ParentObject != null)
        {
            ParentObject.SetActive(true);
        }
        else
        {
            gameObject.SetActive(true);
        }

        ResetToOriginalCutoff();
    }

    private void SetHeight(float height)
    {
        EnsureRendererAndMaterial();
        if (material != null)
        {
            material.SetFloat("_CutoffHeight", height);
        }
    }
}