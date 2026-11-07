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

    // ---- One popup at a time, across every channel ----
    // Everyone currently asking to show text, oldest first. Only the newest request is on screen, and
    // showing it instantly clears any other popup so they can't overlap. Sources re-assert every frame,
    // so a repeat of the same text doesn't count as new. When the newest is hidden, the next one returns.
    private class Request
    {
        public object source;
        public string text;
        public PromptPopup popup;
    }

    private static readonly List<Request> requests = new List<Request>();
    private static Request active;

    private static void RefreshActive()
    {
        active = requests.Count > 0 ? requests[requests.Count - 1] : null;
        if (active == null) return; // nothing new: the last popup just slides away

        foreach (PromptPopup other in popups.Values)
        {
            if (other != null && other != active.popup) other.SnapHidden();
        }
        active.popup.SetText(active.text);
    }

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
    [Tooltip("Letters typed per second when text appears. 0 = show instantly.")]
    public float charactersPerSecond = 60f;

    private RectTransform rect;
    private CanvasGroup canvasGroup;
    private Vector2 shownPosition;
    private Vector2 hiddenPosition;
    private bool wasVisible;
    private float charactersRevealed;

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

        requests.RemoveAll(request => request.popup == this);
        RefreshActive();
    }

    /// <summary>
    /// Shows text. "source" is whoever asked (an InteractPrompt, a hint zone...). Safe to call every
    /// frame: only a new source or new text takes over the screen.
    /// </summary>
    public void Show(object source, string text)
    {
        // Plain loop: this runs every frame, so no allocating lambda.
        int index = -1;
        for (int i = 0; i < requests.Count; i++)
        {
            if (requests[i].source == source) { index = i; break; }
        }

        if (index >= 0)
        {
            if (requests[index].popup == this && requests[index].text == text) return;
            requests.RemoveAt(index);
        }

        requests.Add(new Request { source = source, text = text, popup = this });
        RefreshActive();
    }

    private void SetText(string text)
    {
        if (label != null && label.text != text)
        {
            label.text = text;
            RestartTyping();
        }
    }

    private void SnapHidden()
    {
        rect.anchoredPosition = hiddenPosition;
        canvasGroup.alpha = 0f;
        wasVisible = false;
    }

    private void RestartTyping()
    {
        charactersRevealed = 0f;
        if (label != null) label.maxVisibleCharacters = charactersPerSecond > 0f ? 0 : 99999;
    }

    /// <summary>Withdraws "source"'s text. Anything other sources are showing is left alone.</summary>
    public void Hide(object source)
    {
        if (requests.RemoveAll(r => r.source == source) > 0) RefreshActive();
    }

    public bool IsShowing(object source) => active != null && active.popup == this && active.source == source;

    private void Update()
    {
        // Hidden while the settings menu is open or popups are turned off.
        bool visible = active != null && active.popup == this && PromptsEnabled && !MenuOptions.GamePaused;

        // Retype the text every time the popup reappears, not just when the text changes.
        if (visible && !wasVisible) RestartTyping();
        wasVisible = visible;

        // Typewriter: maxVisibleCharacters skips rich-text tags, so <b> etc. never show half-typed.
        if (visible && label != null && charactersPerSecond > 0f && charactersRevealed < label.text.Length)
        {
            charactersRevealed += charactersPerSecond * Time.unscaledDeltaTime;
            label.maxVisibleCharacters = Mathf.FloorToInt(charactersRevealed);
        }

        // Unscaled time so it still slides away smoothly when the game pauses.
        float t = 1f - Mathf.Exp(-slideSpeed * Time.unscaledDeltaTime);
        rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, visible ? shownPosition : hiddenPosition, t);
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, visible ? 1f : 0f, t);
    }
}
