using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class DissolveObject : MonoBehaviour
{
    [SerializeField] private float noiseStrength = 0.25f;
    [SerializeField] private float dissolveDuration = 1.5f;

    private Material material;
    private Renderer objectRenderer;
    private bool isDissolving = false;

    private void Awake()
    {
        objectRenderer = GetComponent<Renderer>();
        material = objectRenderer.material;
        material.SetFloat("_NoiseStrength", noiseStrength);

        // Cutoff above the object's actual world-space top = fully solid/visible
        SetHeight(GetTopHeight());
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
        // Optional: gameObject.SetActive(false); or Destroy(gameObject, 0.1f);
    }

    // private void OnTriggerEnter(Collider other)
    // {
    //     if (other.CompareTag("Fireball"))
    //     {
    //         TriggerDissolve();
    //     }
    // }

    // Use OnCollisionEnter instead if the fireball has a non-trigger collider/rigidbody
    // private void OnCollisionEnter(Collision collision)
    // {
    //     if (collision.collider.CompareTag("Fireball"))
    //     {
    //         TriggerDissolve();
    //     }
    // }

    public void TriggerDissolve()
    {
        if (isDissolving) return;
        StartCoroutine(DissolveRoutine());
    }

    private void SetHeight(float height)
    {
        material.SetFloat("_CutoffHeight", height);
    }
}