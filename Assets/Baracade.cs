using UnityEngine;

public class Baracade : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public GameObject Door;
    void Start()
    {
        Door.GetComponent<door>().isLocked = true;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Fireball"))
        {
            Door.GetComponent<door>().isLocked = false;
        }
    }
}
