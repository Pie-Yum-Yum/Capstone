using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpellSystem
{
    /// <summary>
    /// Attach to the "page" RectTransform of your spellbook UI.
    /// Handles capturing mouse strokes for RECOGNITION (a plain list of
    /// Vector2 points per stroke) and, separately, drawing visible INK so the
    /// player can see what they're drawing. The two are decoupled on purpose:
    /// ink resolution/appearance can change freely without touching the
    /// recognizer, and the recognizer's sample points don't need to match
    /// pixel-for-pixel what's rendered.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SpellDrawingController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("References")]
        [Tooltip("The RawImage the ink texture is painted onto. Should exactly cover this RectTransform.")]
        [SerializeField] private RawImage inkSurface;

        [Header("Ink Settings")]
        [SerializeField] private int textureResolution = 512;
        [SerializeField] private Color inkColor = Color.white;
        [SerializeField] private int brushRadius = 3;

        [Header("Fizzle")]
        [Tooltip("Colour the ink flashes when a cast fizzles.")]
        [SerializeField] private Color fizzleGlowColor = new Color(1f, 0.85f, 0.2f, 1f);
        [Tooltip("Seconds the ink stays lit up before fading.")]
        [SerializeField] private float fizzleGlowDuration = 0.25f;
        [Tooltip("Seconds the ink takes to fade to nothing after the glow.")]
        [SerializeField] private float fizzleFadeDuration = 0.35f;

        [Header("Spells")]
        [Tooltip("Populated in code from SpellDatabase.BuildKnownSpells() on Awake - see that file to add/edit spells and paste in exported samples.")]
        private List<SpellDefinition> knownSpells = new List<SpellDefinition>();

        private RectTransform rectTransform;
        private Texture2D inkTexture;
        private Color32[] clearPixels;

        private List<List<Vector2>> gestureStrokes = new List<List<Vector2>>();
        private List<Vector2> currentStroke;
        private Vector2? lastPaintedPixel;

        // Ink spent on the drawing currently on the page (for fizzle refunds). Reset on ClearPage/cast.
        private float inkSpentOnDrawing;

        public System.Action<RecognitionResult> OnSpellCastAttempt;

        // ---- Pen state (used by SpellbookAudio for the scratch sound) ----
        /// <summary>Fired when the pen touches the page and a new stroke begins.</summary>
        public System.Action OnStrokeStarted;
        /// <summary>True while the pen is down and still laying ink.</summary>
        public bool IsDrawing => currentStroke != null;
        /// <summary>Smoothed pen speed in page widths per second (0 when the pen isn't moving).</summary>
        public float PenSpeed { get; private set; }
        private float penDistanceThisFrame;

        private Coroutine fizzleRoutine;
        private Color inkSurfaceBaseColor = Color.white;

        [Header("Spell Unlock List")]
        [Tooltip("A list of all spells that have been unlocked by the player.")]
        private HashSet<string> unlockedSpells = new HashSet<string>();

        /// <summary>
        /// Re-scans Resources/SpellSamples and rebuilds the known spell list.
        /// Useful during development: after exporting a new sample mid-Play-session,
        /// call this instead of stopping/restarting Play mode to pick it up.
        /// Right-click the component header in the Inspector to find this in
        /// the context menu, or call it from your own debug UI/hotkey.
        /// </summary>
        [ContextMenu("Reload Known Spells")]

        public void UnlockSpell(string spellName)
        {
            unlockedSpells.Add(spellName);
        }
        void Start()
        {
            unlockedSpells.Add("Menu");
        }
        public void ReloadKnownSpells()
        {
            knownSpells = SpellDatabase.BuildKnownSpells();
            foreach (var spell in knownSpells)
                GestureRecognizer.Prepare(spell);

            int totalSamples = 0;
            foreach (var spell in knownSpells) totalSamples += spell.samples.Count;
            Debug.Log($"Reloaded {knownSpells.Count} spell(s), {totalSamples} total sample(s).");
        }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (inkSurface != null) inkSurfaceBaseColor = inkSurface.color;
            SetupInkTexture();
            ReloadKnownSpells();
        }

        // If the book is hidden mid-fizzle the coroutine dies, so finish the clear here.
        private void OnDisable()
        {
            if (fizzleRoutine != null) ClearPage();
        }

        private void Update()
        {
            // Drag events don't arrive every frame, so smooth the raw per-frame speed.
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            float rawSpeed = penDistanceThisFrame / dt;
            PenSpeed = Mathf.Lerp(PenSpeed, rawSpeed, 1f - Mathf.Exp(-20f * dt));
            if (!IsDrawing) PenSpeed = 0f;
            penDistanceThisFrame = 0f;
        }

        private void SetupInkTexture()
        {
            inkTexture = new Texture2D(textureResolution, textureResolution, TextureFormat.RGBA32, false);
            clearPixels = new Color32[textureResolution * textureResolution];
            for (int i = 0; i < clearPixels.Length; i++) clearPixels[i] = new Color32(0, 0, 0, 0);
            inkTexture.SetPixels32(clearPixels);
            inkTexture.Apply();

            if (inkSurface != null) inkSurface.texture = inkTexture;
        }

        // ---- Input handling -------------------------------------------------

        public void OnPointerDown(PointerEventData eventData)
        {
            // Out of ink: the pen won't start a new stroke.
            if (InkManager.Instance != null && !InkManager.Instance.HasInk) return;
            // Wait for the fizzled drawing to finish fading before drawing again.
            if (fizzleRoutine != null) return;

            currentStroke = new List<Vector2>();
            gestureStrokes.Add(currentStroke);
            lastPaintedPixel = null;
            AddPoint(eventData);
            OnStrokeStarted?.Invoke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            AddPoint(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            AddPoint(eventData);
            currentStroke = null;
        }

        private void AddPoint(PointerEventData eventData)
        {
            if (currentStroke == null) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
                return;

            // Spend ink for the line from the previous point to this one. If the player can only
            // afford part of it, stop the line where the ink ran out and end the stroke.
            bool ranOut = false;
            InkManager ink = InkManager.Instance;
            if (ink != null && currentStroke.Count > 0)
            {
                Vector2 previous = currentStroke[currentStroke.Count - 1];
                float pageWidths = Vector2.Distance(previous, localPoint) / Mathf.Max(rectTransform.rect.width, 0.0001f);
                float inkBefore = ink.CurrentInk;
                float affordable = ink.SpendForLine(pageWidths);
                inkSpentOnDrawing += inkBefore - ink.CurrentInk;

                if (affordable < 1f)
                {
                    localPoint = Vector2.Lerp(previous, localPoint, affordable);
                    ranOut = true;
                }
            }

            if (currentStroke.Count > 0)
            {
                penDistanceThisFrame += Vector2.Distance(currentStroke[currentStroke.Count - 1], localPoint)
                                        / Mathf.Max(rectTransform.rect.width, 0.0001f);
            }

            currentStroke.Add(localPoint);
            PaintInk(localPoint);

            if (ranOut) currentStroke = null;
        }

        // ---- Ink rendering (visual only, not used for recognition) ---------

        private void PaintInk(Vector2 localPoint)
        {
            Vector2 pixel = LocalPointToTexturePixel(localPoint);

            if (lastPaintedPixel.HasValue)
                DrawLineOnTexture(lastPaintedPixel.Value, pixel);
            else
                StampBrush(pixel);

            lastPaintedPixel = pixel;
            inkTexture.Apply();
        }

        private Vector2 LocalPointToTexturePixel(Vector2 localPoint)
        {
            Rect rect = rectTransform.rect;
            float u = (localPoint.x - rect.x) / rect.width;
            float v = (localPoint.y - rect.y) / rect.height;
            return new Vector2(u * textureResolution, v * textureResolution);
        }

        private void DrawLineOnTexture(Vector2 from, Vector2 to)
        {
            float dist = Vector2.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist));
            for (int i = 0; i <= steps; i++)
                StampBrush(Vector2.Lerp(from, to, i / (float)steps));
        }

        private void StampBrush(Vector2 pixelPos)
        {
            int cx = Mathf.RoundToInt(pixelPos.x);
            int cy = Mathf.RoundToInt(pixelPos.y);

            for (int dy = -brushRadius; dy <= brushRadius; dy++)
            {
                for (int dx = -brushRadius; dx <= brushRadius; dx++)
                {
                    if (dx * dx + dy * dy > brushRadius * brushRadius) continue;
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || x >= textureResolution || y < 0 || y >= textureResolution) continue;
                    inkTexture.SetPixel(x, y, inkColor);
                }
            }
        }

        // ---- Public API -------------------------------------------------

        /// <summary>Wire this up to your "cast" button.</summary>
        public void AttemptCast()
        {
            // TEMP DIAGNOSTIC - remove once casting is confirmed working.
            int totalPoints = gestureStrokes.Sum(s => s.Count);
            int totalKnownSamples = knownSpells.Sum(sp => sp.samples.Count);
            //Debug.Log($"[Cast attempt] drawn: {gestureStrokes.Count} stroke(s), {totalPoints} point(s) | " +
            //          $"known: {knownSpells.Count} spell(s), {totalKnownSamples} sample(s) total");

            var result = GestureRecognizer.Recognize(gestureStrokes, knownSpells);

            // TEMP DIAGNOSTIC - remove once casting is confirmed working.
            string matchName = result.bestMatch != null ? result.bestMatch.spellName : "none";
            //Debug.Log($"[Cast result] match: {matchName} | score: {result.score:P1} | " +
            //          $"rawDistance: {result.rawDistance:F4} | successful: {result.successful}");
            if(!unlockedSpells.Contains(matchName))
            {
                result.successful = false;
                result.bestMatch = null;
            }

            // Fizzled casts can refund part of the ink (InkManager.fizzleRefundFraction, 0 by default).
            // The spent counter resets either way so the same drawing can't be refunded twice.
            if (!result.successful && InkManager.Instance != null)
            {
                InkManager.Instance.RefundFizzle(inkSpentOnDrawing);
            }
            inkSpentOnDrawing = 0f;

            if (!result.successful) StartFizzle();

            OnSpellCastAttempt?.Invoke(result);

        }

        // Failed cast: the drawing is thrown away straight away (so it can't be cast again), but
        // the ink stays on screen just long enough to flash yellow and fade out.
        private void StartFizzle()
        {
            gestureStrokes.Clear();
            currentStroke = null;
            lastPaintedPixel = null;

            if (inkSurface == null || !isActiveAndEnabled)
            {
                ClearPage();
                return;
            }

            if (fizzleRoutine != null) StopCoroutine(fizzleRoutine);
            fizzleRoutine = StartCoroutine(FizzleRoutine());
        }

        private System.Collections.IEnumerator FizzleRoutine()
        {
            // Recolour every inked pixel to the glow colour (keeping its alpha). Tinting the RawImage
            // wouldn't work because RawImage.color multiplies, and dark ink times yellow is still dark.
            Color32 glow32 = fizzleGlowColor;
            Color32[] pixels = inkTexture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a == 0) continue;
                pixels[i] = new Color32(glow32.r, glow32.g, glow32.b, pixels[i].a);
            }
            inkTexture.SetPixels32(pixels);
            inkTexture.Apply();

            // White keeps the yellow pure; the fade below only changes alpha.
            Color glow = new Color(1f, 1f, 1f, inkSurfaceBaseColor.a);
            inkSurface.color = glow;

            // Unscaled so it still plays if the Menu spell freezes time right after.
            yield return new WaitForSecondsRealtime(fizzleGlowDuration);

            float elapsed = 0f;
            while (elapsed < fizzleFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                Color c = glow;
                c.a = Mathf.Lerp(glow.a, 0f, elapsed / Mathf.Max(fizzleFadeDuration, 0.0001f));
                inkSurface.color = c;
                yield return null;
            }

            ClearPage();
        }

        /// <summary>Read-only access to the strokes drawn so far this gesture. Used by SpellTemplateRecorder.</summary>
        public List<List<Vector2>> GetCurrentStrokes() => gestureStrokes;

        /// <summary>Wire this up to a "clear page" button, or call it after every cast attempt.</summary>
        public void ClearPage()
        {
            gestureStrokes.Clear();
            currentStroke = null;
            lastPaintedPixel = null;
            inkSpentOnDrawing = 0f;

            if (fizzleRoutine != null)
            {
                StopCoroutine(fizzleRoutine);
                fizzleRoutine = null;
            }
            if (inkSurface != null) inkSurface.color = inkSurfaceBaseColor;

            inkTexture.SetPixels32(clearPixels);
            inkTexture.Apply();
        }

        public HashSet<string> GetUnlockedSpells()
        {
            return unlockedSpells;
        }
    }
}