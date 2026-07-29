using UnityEngine;

public class Baracade : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public GameObject Door;
    void Start()
    {
        if(Door != null)
        {
            Door.GetComponent<door>().isLocked = true;
            //Debug.Log("Door is locked");
        }
    }

    void OnCollisionEnter(Collision other)
    {
        //Debug.Log("Collision Entered");
        if (other.gameObject.CompareTag("Fireball"))
        {
            if(Door != null)
        {
            Door.GetComponent<door>().isLocked = false;
            //Debug.Log("Door is unlocked");
        }
        }
    }
}
