using UnityEngine;

namespace SpellSystem
{
    /// <summary>
    /// Put on the Cast and Clear buttons. The button only shows while the spellbook is open and,
    /// if a Book Anchor is set, stays pinned over that point on the 3D book as it moves.
    ///
    /// The buttons stay on the screen-space Canvas instead of a world-space one on the book so they
    /// aren't scrambled by the ASCII filter and still line up with the DrawPage.
    ///
    /// SETUP: add this to the button, make an empty child of the Spellbook where the button should
    /// sit, and drag it into Book Anchor. With no anchor the button keeps its Canvas position.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SpellbookButton : MonoBehaviour
    {
        [Tooltip("Point on the book this button sticks to. Empty = keep the button's Canvas position.")]
        public Transform bookAnchor;
        [Tooltip("Camera that renders the book. Auto-found from the PlayerCam if left empty.")]
        public Camera bookCamera;
        [Tooltip("Used to tell whether the spellbook is open. Auto-found if left empty.")]
        public PlayerCam playerCam;
        [Tooltip("Alpha per second when showing/hiding. Higher = snappier.")]
        public float fadeSpeed = 8f;

        private RectTransform rect;
        private Canvas canvas;
        private CanvasGroup canvasGroup;

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void Start()
        {
            if (playerCam == null) playerCam = FindAnyObjectByType<PlayerCam>();
            if (bookCamera == null && playerCam != null) bookCamera = playerCam.GetComponentInChildren<Camera>();
            if (bookCamera == null) bookCamera = Camera.main;
        }

        // LateUpdate so the book's Animator has already moved it this frame.
        private void LateUpdate()
        {
            bool bookOpen = playerCam != null && playerCam.paused && !MenuOptions.GamePaused;

            // While the book is still being pulled out from behind the camera there's no valid spot
            // to pin to, so stay hidden until there is. The button then appears already in place.
            bool pinned = (bookOpen || canvasGroup.alpha > 0f) && FollowAnchor();
            bool show = bookOpen && pinned;

            canvasGroup.interactable = show;
            canvasGroup.blocksRaycasts = show;
            // Unscaled so it still hides when the Menu spell freezes time.
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, show ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
        }

        // Returns false only while the anchor is behind the camera. With no anchor there's nothing
        // to wait for, so the button just keeps its Canvas position.
        private bool FollowAnchor()
        {
            if (bookAnchor == null || bookCamera == null || canvas == null) return true;

            Vector3 screenPoint = bookCamera.WorldToScreenPoint(bookAnchor.position);
            if (screenPoint.z <= 0f) return false;

            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    (RectTransform)rect.parent, screenPoint, uiCamera, out Vector3 worldPoint))
            {
                rect.position = worldPoint;
            }
            return true;
        }
    }
}
