using UnityEngine;

/// <summary>
/// A trigger area that shows a hint (e.g. "Press Ctrl to crouch") while the player is inside it,
/// on the "Hint" PromptPopup channel.
///
/// SETUP: empty GameObject + a collider sized to the area (Is Trigger gets ticked automatically).
/// The player must be tagged "Player".
/// </summary>
[RequireComponent(typeof(Collider))]
public class TutorialPromptZone : MonoBehaviour
{
    [Tooltip("Hint text shown while the player is in this area.")]
    public string promptText = "Press 'Ctrl' to crouch";
    [Tooltip("Turn off to stop this zone showing its hint.")]
    public bool showPrompt = true;

    [Header("When to stop showing")]
    [Tooltip("Once the player presses this key the hint is done and won't show again. None = ignore.")]
    public KeyCode completeOnKey = KeyCode.None;
    [Tooltip("Hide after this many seconds (0 = stay until the player leaves the area).")]
    public float hideAfterSeconds = 0f;
    [Tooltip("Only show the first time the player enters.")]
    public bool showOnlyOnce = true;

    private bool playerInside = false;
    private bool completed;
    private bool shownBefore;
    private float shownTime;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || completed) return;
        if (showOnlyOnce && shownBefore) return;

        playerInside = true;
        shownBefore = true;
        shownTime = 0f;
    }

    // private void OnTriggerExit(Collider other)
    // {
    //     if (!other.CompareTag("Player")) return;
    //     //playerInside = false;
    //     //HidePopup();
    // }

    private void Update()
    {
        if (!playerInside) return;

        if (completeOnKey != KeyCode.None && Input.GetKeyDown(completeOnKey))
        {
            Finish();
            return;
        }

        shownTime += Time.deltaTime;
        if (hideAfterSeconds > 0f && shownTime >= hideAfterSeconds)
        {
            Finish();
            return;
        }

        // Re-asserted every frame so it reappears if something else briefly used the same popup.
        PromptPopup popup = PromptPopup.Get(PromptPopup.Channel.Hint);
        if (popup == null) return;
        if (showPrompt && !string.IsNullOrEmpty(promptText)) popup.Show(this, promptText);
        else popup.Hide(this);
    }

    private void Finish()
    {
        completed = true;
        playerInside = false;
        HidePopup();
    }

    private void HidePopup()
    {
        PromptPopup popup = PromptPopup.Get(PromptPopup.Channel.Hint);
        if (popup != null) popup.Hide(this);
    }

    private void OnDisable()
    {
        HidePopup();
    }
}
