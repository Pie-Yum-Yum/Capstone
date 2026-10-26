using System;
using SaveSystem;
using UnityEngine;

namespace SpellSystem
{
    /// <summary>
    /// The player's "ink" resource for drawing spells.
    ///
    /// - SpellDrawingController spends ink as the player draws (cost scales with line length).
    /// - At 0 ink the player can't start a new stroke, and a stroke in progress is cut off.
    /// - Ink slowly regenerates up to regenCapFraction of max, but NOT while the spellbook is open (or the game is paused).
    /// - Pickups call AddInk() for bigger refills.
    /// - Saved with checkpoints like other ICheckpointSaveable objects.
    ///
    /// SETUP: put one of these in the scene (e.g. on the Player). If there's no InkManager,
    /// drawing is free and unlimited, so older scenes keep working.
    /// </summary>
    public class InkManager : MonoBehaviour, ICheckpointSaveable
    {
        [Serializable]
        public class InkCheckpointState
        {
            public float currentInk;
        }

        public static InkManager Instance { get; private set; }

        [Header("Capacity")]
        public float maxInk = 100f;
        [Tooltip("Ink the player starts the scene with.")]
        public float startingInk = 100f;

        [Header("Drawing cost")]
        [Tooltip("Ink spent per page-width of line drawn. A stroke straight across the page costs this much.")]
        public float inkPerPageWidth = 10f;

        [Header("Regeneration")]
        [Tooltip("Ink regained per second while the spellbook is closed.")]
        public float regenPerSecond = 2f;
        [Tooltip("Passive regen stops at this fraction of max ink (0.35 = 35%). Pickups can still fill past it.")]
        [Range(0f, 1f)]
        public float regenCapFraction = 0.35f;
        [Tooltip("Used to tell whether the spellbook is open. Auto-found if left empty.")]
        public PlayerCam playerCam;

        [Header("Fizzle refund")]
        [Tooltip("Fraction of the ink spent on a drawing that is returned when the cast fizzles. 0 = no refund.")]
        [Range(0f, 1f)]
        public float fizzleRefundFraction = 0f;

        [Header("Debug")]
        [Tooltip("Drawing costs nothing. Handy when recording new spell samples.")]
        public bool infiniteInk = false;

        /// <summary>Fired whenever the ink amount changes: (current, max). Use this for the ink jar UI.</summary>
        public event Action<float, float> OnInkChanged;

        public float currentInk;

        public float CurrentInk => currentInk;
        public float MaxInk => maxInk;
        /// <summary>0..1 fill amount, e.g. for the ink jar.</summary>
        public float Normalized => maxInk > 0f ? currentInk / maxInk : 0f;
        public bool HasInk => infiniteInk || currentInk > 0f;
        public string SaveId => CheckpointSaveIdUtility.GetStableSaveId(this);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("More than one InkManager in the scene - only the first one is used.");
                return;
            }
            Instance = this;
            currentInk = Mathf.Clamp(startingInk, 0f, maxInk);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (playerCam == null) playerCam = FindAnyObjectByType<PlayerCam>();
            OnInkChanged?.Invoke(currentInk, maxInk);
        }

        private void Update()
        {
            // Spellbook open = no regen. The settings-menu pause also stops this, since Time.deltaTime is 0 then.
            bool spellbookOpen = playerCam != null && playerCam.paused;
            float regenCap = maxInk * regenCapFraction;
            if (spellbookOpen || currentInk >= regenCap) return;

            SetInk(Mathf.Min(currentInk + regenPerSecond * Time.deltaTime, regenCap));
        }

        /// <summary>
        /// Spends ink for a length of line, measured in page-widths. Returns the fraction (0..1) of that
        /// length the player could afford - less than 1 means they ran out partway through.
        /// </summary>
        public float SpendForLine(float pageWidths)
        {
            if (infiniteInk || pageWidths <= 0f) return 1f;

            float cost = pageWidths * inkPerPageWidth;
            if (cost <= currentInk)
            {
                SetInk(currentInk - cost);
                return 1f;
            }

            float affordable = currentInk / cost;
            SetInk(0f);
            return affordable;
        }

        /// <summary>Adds ink (pickups, refunds). Capped at maxInk. Returns how much was actually added.</summary>
        public float AddInk(float amount)
        {
            if (amount <= 0f) return 0f;
            float before = currentInk;
            SetInk(currentInk + amount);
            return currentInk - before;
        }

        /// <summary>Called by SpellDrawingController when a cast fizzles, with the ink spent on that drawing.</summary>
        public void RefundFizzle(float inkSpentOnDrawing)
        {
            if (fizzleRefundFraction > 0f) AddInk(inkSpentOnDrawing * fizzleRefundFraction);
        }

        public void SetInk(float amount)
        {
            float clamped = Mathf.Clamp(amount, 0f, maxInk);
            if (Mathf.Approximately(clamped, currentInk)) return;
            currentInk = clamped;
            OnInkChanged?.Invoke(currentInk, maxInk);
        }

        public object CaptureState()
        {
            return new InkCheckpointState { currentInk = currentInk };
        }

        public void RestoreState(object state)
        {
            if (state is InkCheckpointState inkState) SetInk(inkState.currentInk);
        }
    }
}
