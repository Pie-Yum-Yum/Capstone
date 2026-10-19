using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// A text popup that slides up from the bottom of the screen and slides back down when hidden.
/// Put one in the UI for each channel you want (one for "Interact" prompts, one for tutorial "Hint"s).
/// If only one popup exists, every channel uses it.
///
/// SETUP: on the Overlay canvas, make a panel anchored to the bottom-centre with a TextMeshPro child,
/// add this to the panel, and drag the text into "Label". Position the panel where it should rest
/// when shown - this script slides it below the screen while hidden.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class PromptPopup : MonoBehaviour
{
    public enum Channel { Interact, Hint }

    // ---- Global on/off (settings menu toggle) ----
    private const string EnabledPrefKey = "Settings.PromptsEnabled";
    public static bool PromptsEnabled
    {
        get => PlayerPrefs.GetInt(EnabledPrefKey, 1) == 1;
        set => PlayerPrefs.SetInt(EnabledPrefKey, value ? 1 : 0);
    }

    private static readonly Dictionary<Channel, PromptPopup> popups = new Dictionary<Channel, PromptPopup>();

    /// <summary>Finds the popup for a channel, or any popup if that channel has none.</summary>
    public static PromptPopup Get(Channel channel)
    {
        if (popups.TryGetValue(channel, out PromptPopup popup) && popup != null) return popup;
        foreach (PromptPopup any in popups.Values) if (any != null) return any;
        return null;
    }

    [Header("Setup")]
    public Channel channel = Channel.Interact;
    public TMP_Text label;

    [Header("Animation")]
    [Tooltip("How far below its resting position the popup hides.")]
    public float hiddenOffset = 200f;
    [Tooltip("Higher = snappier slide.")]
    public float slideSpeed = 12f;

    private RectTransform rect;
    private CanvasGroup canvasGroup;
    private Vector2 shownPosition;
    private Vector2 hiddenPosition;
    private bool wantShown;
    private object owner; // whoever is currently showing text, so others can't hide it by accident

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false; // never steal clicks from the settings menu
        canvasGroup.interactable = false;

        shownPosition = rect.anchoredPosition;
        hiddenPosition = shownPosition - new Vector2(0f, hiddenOffset);
        rect.anchoredPosition = hiddenPosition;
        canvasGroup.alpha = 0f;

        popups[channel] = this;
    }

    private void OnDestroy()
    {
        if (popups.TryGetValue(channel, out PromptPopup popup) && popup == this) popups.Remove(channel);
    }

    /// <summary>Shows text. "source" is whoever asked (an InteractPrompt, a hint zone...).</summary>
    public void Show(object source, string text)
    {
        owner = source;
        if (label != null && label.text != text) label.text = text;
        wantShown = true;
    }

    /// <summary>Hides the popup, but only if "source" is the one currently showing it.</summary>
    public void Hide(object source)
    {
        if (owner != source) return;
        owner = null;
        wantShown = false;
    }

    public bool IsShowing(object source) => wantShown && owner == source;

    private void Update()
    {
        // Hidden while the settings menu is open or popups are turned off.
        bool visible = wantShown && PromptsEnabled && !MenuOptions.GamePaused;

        // Unscaled time so it still slides away smoothly when the game pauses.
        float t = 1f - Mathf.Exp(-slideSpeed * Time.unscaledDeltaTime);
        rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, visible ? shownPosition : hiddenPosition, t);
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, visible ? 1f : 0f, t);
    }
}
