using UnityEngine;

public class Baracade : MonoBehaviour
{
    public GameObject Door;
    public bool IsBroken { get; private set; }

    void Start()
    {
        if (Door != null)
        {
            door doorScript = Door.GetComponent<door>();
            if (doorScript != null)
            {
                doorScript.isLocked = true;
            }
        }
    }

    public void ApplySavedState(bool isBrokenState, bool isLockedState)
    {
        IsBroken = isBrokenState;

        if (Door != null)
        {
            door doorScript = Door.GetComponent<door>();
            if (doorScript != null)
            {
                doorScript.isLocked = isLockedState;
                if (doorScript.lockObject != null)
                {
                    doorScript.lockObject.SetActive(isLockedState);
                }
            }
        }
    }

    void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Fireball"))
        {
            IsBroken = true;

            if (Door != null)
            {
                door doorScript = Door.GetComponent<door>();
                if (doorScript != null)
                {
                    doorScript.isLocked = false;
                    if (doorScript.lockObject != null)
                    {
                        doorScript.lockObject.SetActive(false);
                    }
                }
            }
        }
    }
}
