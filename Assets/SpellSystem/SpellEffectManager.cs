using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SpellSystem
{
    /// <summary>
    /// Passed to every effect callback. `accuracy` doubles as a free hook for
    /// effect strength/quality - e.g. scale a VFX down for a sloppy cast, or
    /// trigger something different entirely for a near-miss vs a clean cast.
    /// </summary>
    public struct SpellCastContext
    {
        public SpellDefinition spell;      // null in the fizzle case (no match found)
        public float accuracy;             // 0..1
        public Transform castOrigin;       // where the effect should originate from - e.g. player camera/hand
    }

    // A UnityEvent that carries SpellCastContext, so it shows up as a
    // configurable, parameterized event list in the Inspector.
    [Serializable]
    public class SpellCastUnityEvent : UnityEvent<SpellCastContext> { }

    [Serializable]
    public class SpellEffectBinding
    {
        [Tooltip("Must exactly match a SpellDefinition's effectId, set in SpellDatabase.")]
        public string effectId;
        public SpellCastUnityEvent onCast;
    }

    /// <summary>
    /// Central place to wire up what actually happens in-game when a spell
    /// is successfully cast.
    ///
    /// SETUP:
    /// 1. Drop this on your player (or a dedicated "SpellCasting" manager) object.
    /// 2. Drag your SpellDrawingController into the Drawing Controller field.
    /// 3. Add one entry to Effects per spell effectId you want to handle.
    /// 4. In each entry's "On Cast" event, click + and wire it to whatever
    ///    should happen - drag in a particle system and call Play(), an
    ///    AudioSource and call PlayOneShot(), etc. No code required for
    ///    simple effects.
    ///
    /// For effects too complex for the Inspector alone (spawning with
    /// runtime logic, movement, damage over time, coroutines), write a small
    /// MonoBehaviour with a public method matching SpellCastContext and drag
    /// THAT method into the binding's onCast list instead - see
    /// ExampleSpellEffect.cs for the pattern.
    /// </summary>
    public class SpellEffectManager : MonoBehaviour
    {
        [SerializeField] private SpellDrawingController drawingController;

        [Tooltip("Where effects should spawn from/aim from - e.g. the player's camera. Defaults to this object's transform if left empty.")]
        [SerializeField] private Transform castOrigin;

        [SerializeField] private List<SpellEffectBinding> effects = new List<SpellEffectBinding>();

        [Header("Fallback")]
        [Tooltip("Invoked when a cast attempt fails (score below threshold, or no shape matched). Optional - great spot for a fizzle/backfire effect.")]
        public SpellCastUnityEvent onFizzle;

        private void OnEnable()
        {
            if (drawingController == null)
            {
                Debug.LogWarning("SpellEffectManager has no SpellDrawingController assigned - it won't receive any cast attempts.");
                return;
            }
            drawingController.OnSpellCastAttempt += HandleCastAttempt;
        }

        private void OnDisable()
        {
            if (drawingController != null)
                drawingController.OnSpellCastAttempt -= HandleCastAttempt;
        }

        private void HandleCastAttempt(RecognitionResult result)
        {
            var context = new SpellCastContext
            {
                spell = result.bestMatch,
                accuracy = result.score,
                castOrigin = castOrigin != null ? castOrigin : transform
            };

            if (!result.successful || result.bestMatch == null)
            {
                onFizzle?.Invoke(context);
                return;
            }

            var binding = effects.Find(e => e.effectId == result.bestMatch.effectId);
            if (binding == null)
            {
                Debug.LogWarning($"No SpellEffectBinding found for effectId '{result.bestMatch.effectId}' " +
                                  $"(spell '{result.bestMatch.spellName}'). Add an entry for it in the Inspector.");
                return;
            }

            binding.onCast?.Invoke(context);
        }
        public void CastSpell(SpellDefinition spell)
        {
            var context = new SpellCastContext
            {
                spell = spell,
                castOrigin = castOrigin != null ? castOrigin : transform
            };

            if (spell == null)
            {
                onFizzle?.Invoke(context);
                return;
            }

            var binding = effects.Find(e => e.effectId == spell.effectId);
            if (binding == null)
            {
                Debug.LogWarning($"No SpellEffectBinding found for effectId '{spell.effectId}' " +
                                  $"(spell '{spell.spellName}'). Add an entry for it in the Inspector.");
                return;
            }

            binding.onCast?.Invoke(context);
        }

    }
}