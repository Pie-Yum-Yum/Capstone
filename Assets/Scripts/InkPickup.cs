using System;
using SaveSystem;
using SpellSystem;
using UnityEngine;

// Walk-over ink refill. Needs a trigger collider; the Player (tagged "Player", with a Rigidbody)
// collects it by walking into it. If a DissolveObject is on the same object it dissolves away,
// otherwise it just disappears. Saved with checkpoints so collected pickups stay collected.
[RequireComponent(typeof(Collider))]
public class InkPickup : MonoBehaviour, ICheckpointSaveable
{
    [Serializable]
    public class InkPickupCheckpointState
    {
        public bool isCollected;
    }

    [Tooltip("How much ink this pickup gives.")]
    public float inkAmount = 40f;
    [Tooltip("If false, the pickup is left in the world when the player's ink is already full.")]
    public bool collectWhenFull = false;

    private bool isCollected;

    public bool IsCollected => isCollected;
    public string SaveId => CheckpointSaveIdUtility.GetStableSaveId(this);

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    // Stay (not Enter) so a player standing on it while full still collects it once they've used some ink.
    private void OnTriggerStay(Collider other)
    {
        if (isCollected || !other.CompareTag("Player")) return;

        InkManager ink = InkManager.Instance;
        if (ink == null)
        {
            Debug.LogWarning("InkPickup touched but there's no InkManager in the scene.");
            return;
        }

        if (!collectWhenFull && ink.CurrentInk >= ink.MaxInk) return;

        ink.AddInk(inkAmount);
        isCollected = true;

        if (TryGetComponent(out DissolveObject dissolve))
        {
            dissolve.TriggerDissolve();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public object CaptureState()
    {
        return new InkPickupCheckpointState { isCollected = isCollected };
    }

    public void RestoreState(object state)
    {
        if (!(state is InkPickupCheckpointState pickupState)) return;

        isCollected = pickupState.isCollected;

        if (TryGetComponent(out DissolveObject dissolve))
        {
            dissolve.ApplySavedState(isCollected);
        }
        else
        {
            gameObject.SetActive(!isCollected);
        }
    }
}
