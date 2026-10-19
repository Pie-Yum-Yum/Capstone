using UnityEngine;

/// <summary>
/// Put on anything the player can interact with (doors, spell pages, ...) to show a popup like
/// "Open Door: 'E'" while they look at it. Detection is done by ONE raycast per frame in
/// InteractionPrompter (on the player camera), not by each object.
///
/// The object (or one of its children) needs a collider for the look-at raycast to hit.
/// </summary>
public class InteractPrompt : MonoBehaviour
{
    [Tooltip("Text shown while looking at this object.")]
    public string promptText = "Interact: 'E'";
    [Tooltip("Turn off to stop this object showing a popup.")]
    public bool showPrompt = true;
    [Tooltip("Max distance the popup appears from. Match the object's own interact distance (doors and pages use 3).")]
    public float interactDistance = 3f;

    [Header("Doors (only used if this object has a door script)")]
    [Tooltip("Shown instead of Prompt Text while the door is open. Leave empty to always use Prompt Text.")]
    public string openDoorText = "Close Door: 'E'";
    [Tooltip("Shown while the door is locked. Leave empty to show nothing on locked doors.")]
    public string lockedDoorText = "";

    private door doorScript;

    private void Awake()
    {
        doorScript = GetComponentInParent<door>();
        if (doorScript == null) doorScript = GetComponentInChildren<door>();
    }

    /// <summary>The text to show right now, or null if this object shouldn't show a popup.</summary>
    public string GetCurrentText()
    {
        if (!showPrompt || !isActiveAndEnabled) return null;

        if (doorScript != null)
        {
            if (doorScript.IsLocked) return string.IsNullOrEmpty(lockedDoorText) ? null : lockedDoorText;
            if (doorScript.IsOpen && !string.IsNullOrEmpty(openDoorText)) return openDoorText;
        }

        return string.IsNullOrEmpty(promptText) ? null : promptText;
    }
}
