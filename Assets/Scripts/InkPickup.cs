using System;
using SaveSystem;
using UnityEngine;

// Walk-over mana potion: goes into the player's InkPotionInventory, to be drunk later with the use key.
// Needs a trigger collider; the Player (tagged "Player", with a Rigidbody) collects it by walking
// into it. If a DissolveObject is on the same object it dissolves away, otherwise it just disappears.
// Saved with checkpoints so collected pickups stay collected.
//
// SETUP: empty GameObject with this + a trigger collider, with the potion model as a child that has
// a PickupHover. The hover then moves only the model, not the collider.
[RequireComponent(typeof(Collider))]
public class InkPickup : MonoBehaviour, ICheckpointSaveable
{
    [Serializable]
    public class InkPickupCheckpointState
    {
        public bool isCollected;
    }

    private bool isCollected;

    public bool IsCollected => isCollected;
    public string SaveId => CheckpointSaveIdUtility.GetStableSaveId(this);

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    // Stay (not Enter) so a player standing on it with a full inventory still collects it once they've used a potion.
    private void OnTriggerStay(Collider other)
    {
        if (isCollected || !other.CompareTag("Player")) return;

        InkPotionInventory inventory = InkPotionInventory.Instance;
        if (inventory == null)
        {
            Debug.LogWarning("InkPickup touched but there's no InkPotionInventory in the scene.");
            return;
        }

        if (!inventory.TryAddPotion()) return; // carrying the max: leave it in the world
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
