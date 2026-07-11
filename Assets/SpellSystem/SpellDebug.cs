using UnityEngine;
using SpellSystem;

public class SpellCastListener : MonoBehaviour
{
    [SerializeField] private SpellDrawingController drawingController;

    private void OnEnable()
    {
        drawingController.OnSpellCastAttempt += HandleCastResult;
    }

    private void OnDisable()
    {
        drawingController.OnSpellCastAttempt -= HandleCastResult;
    }

    private void HandleCastResult(RecognitionResult result)
    {
        if (result.successful)
            Debug.Log($"Cast {result.bestMatch.spellName} at {result.score:P0} accuracy!");
        else
            Debug.Log($"Fizzled. Closest match: {result.bestMatch?.spellName ?? "none"} at {result.score:P0}");
    }
}