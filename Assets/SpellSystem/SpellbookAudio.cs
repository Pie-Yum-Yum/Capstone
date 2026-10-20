using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellSystem
{
    /// <summary>
    /// All spellbook sounds in one place: opening/closing the book, the pen scratching while you
    /// draw, and casting (success / fizzle / releasing a primed spell).
    ///
    /// SETUP: drop this on the spellbook (or the player camera), drag in the SpellDrawingController,
    /// and assign whichever clips you have. Every clip is optional - empty slots just stay silent.
    /// PlayerCam calls PlayOpen/PlayClose and SpellCaster calls PlayRelease; both find this
    /// automatically if their field is left empty. PlayOpen/PlayClose are public, so you can also
    /// call them from Animation Events on the book animation instead if you want exact timing.
    /// </summary>
    public class SpellbookAudio : MonoBehaviour
    {
        [Serializable]
        public class SpellSounds
        {
            [Tooltip("Must match the spell's name in SpellDatabase, e.g. Fireball, Noise, Lock, Menu.")]
            public string spellName;
            [Tooltip("Plays when this spell is recognised. Leave empty to use the generic cast clip.")]
            public AudioClip castClip;
            [Tooltip("Plays when the primed spell is actually used (fireball thrown, lure thrown, door locked).")]
            public AudioClip releaseClip;
        }

        [Header("References")]
        [SerializeField] private SpellDrawingController drawingController;

        [Header("Book")]
        public AudioClip openClip;
        public AudioClip closeClip;
        [Range(0f, 1f)] public float bookVolume = 0.8f;

        [Header("Drawing")]
        [Tooltip("A LOOPING pen/quill scratch sound. Its volume and pitch follow how fast the pen moves.")]
        public AudioClip drawLoopClip;
        [Tooltip("Optional short taps played when the pen touches the page (start of each stroke).")]
        public AudioClip[] penDownClips;
        [Range(0f, 1f)] public float drawMaxVolume = 0.6f;
        [Range(0f, 1f)] public float penDownVolume = 0.4f;
        [Tooltip("Pen speed (page widths per second) at which the scratch reaches full volume.")]
        public float fullVolumePenSpeed = 1.5f;
        [Tooltip("Pitch when moving slowly -> pitch when moving fast.")]
        public Vector2 drawPitchRange = new Vector2(0.9f, 1.15f);
        [Tooltip("How fast the scratch fades in when the pen moves (higher = snappier).")]
        public float drawAttack = 25f;
        [Tooltip("How fast the scratch fades out when the pen stops or lifts.")]
        public float drawRelease = 12f;

        [Header("Casting")]
        [Tooltip("Generic sound for any successfully recognised spell.")]
        public AudioClip castClip;
        [Tooltip("Plays when the drawing doesn't match a known/unlocked spell.")]
        public AudioClip fizzleClip;
        [Tooltip("Per-spell overrides. Any spell not listed uses the generic cast clip and no release sound.")]
        public List<SpellSounds> spellSounds = new List<SpellSounds>();
        [Range(0f, 1f)] public float castVolume = 0.9f;
        [Tooltip("Close the book with its close sound when a spell is cast (the cast animation puts the book away).")]
        public bool playCloseOnCast = true;

        private AudioSource oneShotSource;
        private AudioSource drawSource;
        private float drawVolume;

        private void Awake()
        {
            if (drawingController == null) drawingController = FindAnyObjectByType<SpellDrawingController>(FindObjectsInactive.Include);

            // UI sounds: 2D, and they keep playing when the Menu spell / Tab pauses the AudioListener,
            // so the book-close and cast sounds aren't cut off the moment the settings menu opens.
            oneShotSource = gameObject.AddComponent<AudioSource>();
            oneShotSource.playOnAwake = false;
            oneShotSource.spatialBlend = 0f;
            oneShotSource.ignoreListenerPause = true;

            drawSource = gameObject.AddComponent<AudioSource>();
            drawSource.playOnAwake = false;
            drawSource.spatialBlend = 0f;
            drawSource.loop = true;
            drawSource.volume = 0f;
        }

        private void OnEnable()
        {
            if (drawingController == null) return;
            drawingController.OnSpellCastAttempt += HandleCastAttempt;
            drawingController.OnStrokeStarted += HandleStrokeStarted;
        }

        private void OnDisable()
        {
            if (drawingController == null) return;
            drawingController.OnSpellCastAttempt -= HandleCastAttempt;
            drawingController.OnStrokeStarted -= HandleStrokeStarted;
        }

        private void Update()
        {
            UpdateDrawLoop();
        }

        // ---- Book ----

        public void PlayOpen() => PlayOneShot(openClip, bookVolume);
        public void PlayClose() => PlayOneShot(closeClip, bookVolume);

        // ---- Drawing ----

        private void HandleStrokeStarted()
        {
            if (penDownClips == null || penDownClips.Length == 0) return;
            PlayOneShot(penDownClips[UnityEngine.Random.Range(0, penDownClips.Length)], penDownVolume);
        }

        private void UpdateDrawLoop()
        {
            if (drawLoopClip == null || drawingController == null) return;

            float speed01 = drawingController.IsDrawing
                ? Mathf.Clamp01(drawingController.PenSpeed / Mathf.Max(fullVolumePenSpeed, 0.0001f))
                : 0f;
            float target = speed01 * drawMaxVolume;

            // Unscaled time: drawing happens with the book open, and the book must still sound right
            // if anything ever slows time down.
            float rate = target > drawVolume ? drawAttack : drawRelease;
            drawVolume = Mathf.MoveTowards(drawVolume, target, rate * drawMaxVolume * Time.unscaledDeltaTime);

            if (drawVolume > 0.001f)
            {
                if (!drawSource.isPlaying)
                {
                    drawSource.clip = drawLoopClip;
                    // Start somewhere random in the loop so every stroke doesn't begin with the same scratch.
                    drawSource.time = UnityEngine.Random.Range(0f, drawLoopClip.length);
                    drawSource.Play();
                }
                drawSource.volume = drawVolume;
                drawSource.pitch = Mathf.Lerp(drawPitchRange.x, drawPitchRange.y, speed01);
            }
            else if (drawSource.isPlaying)
            {
                drawSource.Stop();
            }
        }

        // ---- Casting ----

        private void HandleCastAttempt(RecognitionResult result)
        {
            if (!result.successful || result.bestMatch == null)
            {
                PlayOneShot(fizzleClip, castVolume);
                return;
            }

            SpellSounds sounds = FindSpellSounds(result.bestMatch.spellName);
            PlayOneShot(sounds != null && sounds.castClip != null ? sounds.castClip : castClip, castVolume);
            if (playCloseOnCast) PlayClose();
        }

        /// <summary>Called by SpellCaster when a primed spell is used (thrown / applied).</summary>
        public void PlayRelease(string spellName)
        {
            SpellSounds sounds = FindSpellSounds(spellName);
            if (sounds != null) PlayOneShot(sounds.releaseClip, castVolume);
        }

        private SpellSounds FindSpellSounds(string spellName)
        {
            return spellSounds.Find(s => s.spellName == spellName);
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (clip == null) return;
            oneShotSource.PlayOneShot(clip, volume);
        }
    }
}
