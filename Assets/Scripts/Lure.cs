using System.Collections;
using UnityEngine;

public class Lure : MonoBehaviour
{

    private Material material;
    private Renderer objectRenderer;

    private void Awake()
    {
        objectRenderer = GetComponent<Renderer>();
        material = objectRenderer.material;
    }
    

    
    void OnCollisionEnter(Collision collision)
    {
        Rigidbody rb = gameObject.GetComponent<Rigidbody>();
        rb.isKinematic = true; 
        rb.detectCollisions = false;
        Debug.Log("Lure hit something");
        StartCoroutine(SoundEmmit());
        // Stop moving
        // initiate dissolving effect and destroy the object
        //Destroy(gameObject);
    }
    private IEnumerator SoundEmmit()
    {
        float duration = 3f;
        float size = 10f;
        SoundManager.Instance?.EmitSound(transform.position, size, duration);
        yield return new WaitForSeconds(5f);

        SoundManager.Instance?.EmitSound(transform.position, size, duration);
        yield return new WaitForSeconds(5f);
        material.SetColor("_Albedo", Color.red);
        SoundManager.Instance?.EmitSound(transform.position, size, duration);
        yield return new WaitForSeconds(3.5f);



        gameObject.GetComponent<DissolveObject>().TriggerDissolve();
        
    }
}
