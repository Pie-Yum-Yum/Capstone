using System.Collections;
using UnityEngine;

public class Lure : MonoBehaviour
{

    [Header("Audio (optional)")]
    [Tooltip("Plays on the first two pings.")]
    public AudioClip pingClip;
    [Tooltip("Plays on the third (last) ping so the player knows the lure is about to run out. Falls back to the ping clip if empty.")]
    public AudioClip finalPingClip;
    [Range(0f, 1f)] public float pingVolume = 1f;
    [Tooltip("Full volume inside this distance.")]
    public float pingMinDistance = 2f;
    [Tooltip("Silent beyond this distance.")]
    public float pingMaxDistance = 30f;

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
        //Debug.Log("Lure hit something");
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
        PlayPing(pingClip);
        yield return new WaitForSeconds(5f);

        SoundManager.Instance?.EmitSound(transform.position, size, duration);
        PlayPing(pingClip);
        yield return new WaitForSeconds(5f);
        material.SetColor("_Albedo", Color.red);
        SoundManager.Instance?.EmitSound(transform.position, size, duration);
        PlayPing(finalPingClip != null ? finalPingClip : pingClip);
        yield return new WaitForSeconds(3.5f);



        gameObject.GetComponent<DissolveObject>().TriggerDissolve();

    }

    // Plays from a temporary object at the lure's position, so the last ping isn't cut off
    // when the lure dissolves and deactivates 3.5s later.
    private void PlayPing(AudioClip clip)
    {
        if (clip == null) return;

        GameObject soundObject = new GameObject("Lure Ping");
        soundObject.transform.position = transform.position;

        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = pingVolume;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = pingMinDistance;
        source.maxDistance = pingMaxDistance;
        source.dopplerLevel = 0f;
        source.Play();

        Destroy(soundObject, clip.length + 0.1f);
    }
}
