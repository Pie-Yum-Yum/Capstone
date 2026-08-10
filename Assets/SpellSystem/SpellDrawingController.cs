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

        [Header("Spells")]
        [Tooltip("Populated in code from SpellDatabase.BuildKnownSpells() on Awake - see that file to add/edit spells and paste in exported samples.")]
        private List<SpellDefinition> knownSpells = new List<SpellDefinition>();

        private RectTransform rectTransform;
        private Texture2D inkTexture;
        private Color32[] clearPixels;

        private List<List<Vector2>> gestureStrokes = new List<List<Vector2>>();
        private List<Vector2> currentStroke;
        private Vector2? lastPaintedPixel;

        public System.Action<RecognitionResult> OnSpellCastAttempt;

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
            SetupInkTexture();
            ReloadKnownSpells();
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
            currentStroke = new List<Vector2>();
            gestureStrokes.Add(currentStroke);
            lastPaintedPixel = null;
            AddPoint(eventData);
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

            currentStroke.Add(localPoint);
            PaintInk(localPoint);
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

            OnSpellCastAttempt?.Invoke(result);

        }

        /// <summary>Read-only access to the strokes drawn so far this gesture. Used by SpellTemplateRecorder.</summary>
        public List<List<Vector2>> GetCurrentStrokes() => gestureStrokes;

        /// <summary>Wire this up to a "clear page" button, or call it after every cast attempt.</summary>
        public void ClearPage()
        {
            gestureStrokes.Clear();
            currentStroke = null;
            lastPaintedPixel = null;

            inkTexture.SetPixels32(clearPixels);
            inkTexture.Apply();
        }

        public HashSet<string> GetUnlockedSpells()
        {
            return unlockedSpells;
        }
    }
}