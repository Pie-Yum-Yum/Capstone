using System;
using SaveSystem;
using SpellSystem;
using UnityEngine;

// The potions the player is carrying, up to maxPotions. InkPickup adds to it; the use key drinks one
// and restores a share of max ink. A potion isn't used up if ink is already full. Saved with checkpoints.
//
// SETUP: put one in the scene (e.g. on the Player). Put the potion sprites next to the ink jar on the
// HUD and drag them into Potion Icons, in the order they should fill up.
public class InkPotionInventory : MonoBehaviour, ICheckpointSaveable
{
    [Serializable]
    public class InkPotionCheckpointState
    {
        public int potionCount;
    }

    public static InkPotionInventory Instance { get; private set; }

    [Tooltip("Drinks a potion.")]
    public KeyCode useKey = KeyCode.T;
    [Tooltip("Share of max ink one potion restores (0.5 = 50%).")]
    [Range(0f, 1f)]
    public float restoreFraction = 0.5f;
    [Tooltip("Most potions the player can carry. Pickups stay in the world while at this limit.")]
    public int maxPotions = 2;
    [Tooltip("HUD sprite objects, one per potion slot. Turned on as potions are picked up, and off from the last one as they're used.")]
    public GameObject[] potionIcons;

    private int potionCount;

    public int PotionCount => potionCount;
    public bool IsFull => potionCount >= maxPotions;
    public string SaveId => CheckpointSaveIdUtility.GetStableSaveId(this);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("More than one InkPotionInventory in the scene - only the first one is used.");
            return;
        }
        Instance = this;
        UpdateIcons();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(useKey) && !MenuOptions.GamePaused) TryUsePotion();
    }

    /// <summary>Adds a potion. Returns false if the player is already carrying the most they can.</summary>
    public bool TryAddPotion()
    {
        if (IsFull) return false;
        SetCount(potionCount + 1);
        return true;
    }

    /// <summary>Drinks a potion. Returns false if there's none to drink or ink is already full.</summary>
    public bool TryUsePotion()
    {
        InkManager ink = InkManager.Instance;
        if (potionCount <= 0 || ink == null || ink.CurrentInk >= ink.MaxInk) return false;

        ink.AddInk(ink.MaxInk * restoreFraction);
        SetCount(potionCount - 1);
        return true;
    }

    private void SetCount(int count)
    {
        potionCount = Mathf.Clamp(count, 0, maxPotions);
        UpdateIcons();
    }

    private void UpdateIcons()
    {
        if (potionIcons == null) return;
        for (int i = 0; i < potionIcons.Length; i++)
        {
            if (potionIcons[i] != null) potionIcons[i].SetActive(i < potionCount);
        }
    }

    public object CaptureState()
    {
        return new InkPotionCheckpointState { potionCount = potionCount };
    }

    public void RestoreState(object state)
    {
        if (state is InkPotionCheckpointState potionState) SetCount(potionState.potionCount);
    }
}
