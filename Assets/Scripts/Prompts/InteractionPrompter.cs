using UnityEngine;

/// <summary>
/// Put on the player camera. Fires a single raycast each frame and shows the popup of whichever
/// InteractPrompt the player is looking at (on the "Interact" PromptPopup channel).
/// </summary>
public class InteractionPrompter : MonoBehaviour
{
    [Tooltip("Longest distance to check. Should be at least the largest InteractPrompt distance.")]
    public float maxDistance = 5f;
    [Tooltip("Layers the look-at ray can hit. Default hits everything, same as the door/page scripts.")]
    public LayerMask raycastMask = ~0;
    [Tooltip("Used to hide prompts while the spellbook is open. Auto-found if empty.")]
    public PlayerCam playerCam;

    private InteractPrompt current;
    private Collider lastCollider;
    private InteractPrompt lastColliderPrompt;

    private void Start()
    {
        if (playerCam == null) playerCam = GetComponentInParent<PlayerCam>();
        if (playerCam == null) playerCam = FindAnyObjectByType<PlayerCam>();
    }

    private void Update()
    {
        InteractPrompt target = null;
        string text = null;

        bool spellbookOpen = playerCam != null && playerCam.paused;
        if (!spellbookOpen && Physics.Raycast(transform.position, transform.forward, out RaycastHit hit,
                maxDistance, raycastMask, QueryTriggerInteraction.Ignore))
        {
            // Only look the component up when the ray hits a different collider than last frame.
            if (hit.collider != lastCollider)
            {
                lastCollider = hit.collider;
                lastColliderPrompt = hit.collider.GetComponentInParent<InteractPrompt>();
            }

            if (lastColliderPrompt != null && hit.distance <= lastColliderPrompt.interactDistance)
            {
                text = lastColliderPrompt.GetCurrentText();
                if (text != null) target = lastColliderPrompt;
            }
        }

        PromptPopup popup = PromptPopup.Get(PromptPopup.Channel.Interact);
        if (popup == null) return;

        if (target != null)
        {
            popup.Show(this, text);
        }
        else if (current != null)
        {
            popup.Hide(this);
        }
        current = target;
    }

    private void OnDisable()
    {
        PromptPopup popup = PromptPopup.Get(PromptPopup.Channel.Interact);
        if (popup != null) popup.Hide(this);
        current = null;
    }
}
