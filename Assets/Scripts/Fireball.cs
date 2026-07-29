using UnityEngine;

public class Fireball : MonoBehaviour
{
    //[SerializeField] private float flammableDestroyDelay = 3f;

    private void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.collider.gameObject);
    }

    private void HandleHit(GameObject target)
    {
        if (target == gameObject)
        {
            return;
        }

        if (target.CompareTag("Flammable"))
        {
            // for each child of the target, check if it has a DissolveObject component and trigger the dissolve effect
            foreach (Transform child in target.transform)
            {
                if (child.TryGetComponent(out DissolveObject childDissolve))
                {
                    childDissolve.TriggerDissolve();
                }
            }
            if (target.TryGetComponent(out DissolveObject parentDissolve))
            {
                parentDissolve.TriggerDissolve();
            }
            //Destroy(target, flammableDestroyDelay);
        }
        else
        {
            //Destroy(target);
        }

        Destroy(gameObject);
    }
}