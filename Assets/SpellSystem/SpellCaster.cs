using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpellSystem
{
    public class SpellCaster : MonoBehaviour
    {
        public SpellDrawingController SpellDrawingController;
        private SpellCastContext context;
        private string PrimedSpell = null;
        public Animator animator;
        public PlayerCam PlayerCam;
        public PlayerMovement playerMovement;
        [Header("Menu object")]
        public GameObject menuObject;
        [Tooltip("Settings/pause menu controller. Auto-found in the scene if left empty.")]
        public MenuOptions menuOptions;

        [Tooltip("Plays each spell's release sound. Auto-found in the scene if left empty.")]
        public SpellbookAudio spellbookAudio;

        [Tooltip("The Eye spell's map. Auto-found in the scene if left empty.")]
        public SpellMap spellMap;

        [Header("Spell ready display")]
        [Tooltip("The centre dot. Changes colour while a spell is ready to cast.")]
        public Graphic readyDot;
        [Tooltip("Text under the dot (TextMeshPro or legacy Text). Blank unless a spell is ready to cast.")]
        public Graphic readyLabel;
        public Color idleDotColor = Color.white;
        public Color fireballDotColor = Color.red;
        public Color lureDotColor = Color.yellow;
        public Color lockDotColor = Color.grey;
        [Tooltip("Names shown in \"Ready to cast: ...\".")]
        public string fireballDisplayName = "Fireball";
        public string lureDisplayName = "Lure";
        public string lockDisplayName = "Lock";

        private door currentHighlightedDoor;
        private string displayedSpell;

        private void Start()
        {
            if (spellbookAudio == null) spellbookAudio = FindAnyObjectByType<SpellbookAudio>(FindObjectsInactive.Include);
            UpdateReadyDisplay();
        }

        // Follows PrimedSpell, so casting, cancelling and priming a different spell all update it.
        private void UpdateReadyDisplay()
        {
            displayedSpell = PrimedSpell;

            Color dotColor = idleDotColor;
            string spellName = null;
            switch (PrimedSpell)
            {
                case "Fireball":
                    dotColor = fireballDotColor;
                    spellName = fireballDisplayName;
                    break;
                case "Noise":
                    dotColor = lureDotColor;
                    spellName = lureDisplayName;
                    break;
                case "Lock":
                    dotColor = lockDotColor;
                    spellName = lockDisplayName;
                    break;
            }

            if (readyDot != null) readyDot.color = dotColor;

            string labelText = spellName == null ? "" : $"Ready to cast: {spellName}";
            if (readyLabel is TMP_Text tmpLabel) tmpLabel.text = labelText;
            else if (readyLabel is Text legacyLabel) legacyLabel.text = labelText;
        }

        // The normal "cast and close the book" animation. Clears any map raise/lower trigger that never got used
        // first, so a stale one can't hijack it.
        private void PlayCastAnimation()
        {
            if (spellMap == null) spellMap = FindAnyObjectByType<SpellMap>(FindObjectsInactive.Include);
            if (spellMap != null) spellMap.ClearHandTriggers();
            animator.SetTrigger("Trigger2");
        }

        private void PlayRelease(string spellName)
        {
            if (spellbookAudio != null) spellbookAudio.PlayRelease(spellName);
        }

        public void CastLockSpell(SpellCastContext incomingContext)
        {
            context = incomingContext;
            PrimedSpell = "Lock";
            PlayCastAnimation();
            PlayerCam.paused = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        public void CastEyeSpell(SpellCastContext incomingContext)
        {
            // Not a primed spell: the book goes away and the map comes straight up.
            context = incomingContext;
            PrimedSpell = null;
            PlayerCam.paused = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SpellDrawingController.ClearPage();

            if (spellMap == null) spellMap = FindAnyObjectByType<SpellMap>(FindObjectsInactive.Include);
            bool opened = spellMap != null && spellMap.Open();
            if (spellMap == null) Debug.LogWarning("Eye cast but there's no SpellMap in the scene.");

            // The map's raise animation replaces the normal cast. Firing both in the same frame made them
            // fight (every transition is from Any State), so only fall back to the cast if the map has none.
            if (!opened || !spellMap.HasRaiseAnimation) PlayCastAnimation();
        }
        public void CastFireballSpell(SpellCastContext incomingContext)
        {
            context = incomingContext;
            PrimedSpell = "Fireball";
            PlayCastAnimation();
            PlayerCam.paused = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void CastNoiseSpell(SpellCastContext incomingContext)
        {
            context = incomingContext;
            PrimedSpell = "Noise";
            PlayCastAnimation();
            PlayerCam.paused = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void CastMenuSpell(SpellCastContext incomingContext)
        {
            context = incomingContext;
            PrimedSpell = "Menu";
            PlayCastAnimation();
            //PlayerCam.paused = false;
            //Cursor.lockState = CursorLockMode.Locked;
            //Cursor.visible = false;
            menuOptions.PauseGame();    
        }
        

        /// <summary>Drops whatever spell is primed (used by the death screen).</summary>
        public void CancelPrimedSpell()
        {
            PrimedSpell = null;
            if (currentHighlightedDoor != null)
            {
                currentHighlightedDoor.SetSpellHighlight(false, Color.black);
                currentHighlightedDoor.lookingAtDoor = false;
                currentHighlightedDoor = null;
            }
            UpdateReadyDisplay();
        }

        void Update()
        {
            // Dead: no throwing or locking with the clicks that press the death screen's buttons.
            if (DeathScreen.InputBlocked) return;

            // HEY 
            // whem adding new spells, add a new case here and make sure to set PrimedSpell to the name of the spell in the CastSpell function above
            // also add an entry in the spell database for the new spell and ALSO call it in the editor from the SpellCaster GameObject!!!!
            switch (PrimedSpell)
            {
                case "Lock":
                    SpellDrawingController.ClearPage();

                    Ray ray = new Ray(context.castOrigin.position, context.castOrigin.forward);

                    door newDoor = null;
                    if (Physics.Raycast(ray, out RaycastHit hit, 10f))
                    {
                        newDoor = hit.collider.GetComponentInParent<door>();
                    }

                    if (newDoor != currentHighlightedDoor)
                    {
                        if (currentHighlightedDoor != null)
                        {
                            currentHighlightedDoor.SetSpellHighlight(false, Color.black);
                            currentHighlightedDoor.lookingAtDoor = false;
                        }

                        currentHighlightedDoor = newDoor;

                        if (currentHighlightedDoor != null)
                        {
                            currentHighlightedDoor.SetSpellHighlight(true, new Color(0.4f, 0.7f, 1f, 1f));
                            currentHighlightedDoor.lookingAtDoor = true;
                        }
                    }
                    else if (currentHighlightedDoor != null)
                    {
                        currentHighlightedDoor.SetSpellHighlight(true, new Color(0.4f, 0.7f, 1f, 1f));

                        if (Input.GetMouseButtonDown(0))
                        {
                            currentHighlightedDoor.lockDoor();
                            PlayRelease("Lock");
                            PrimedSpell = null;
                            currentHighlightedDoor.SetSpellHighlight(false, Color.black);
                            currentHighlightedDoor = null;
                        }
                        
                    }

                    if (newDoor == null && currentHighlightedDoor != null)
                    {
                        currentHighlightedDoor.SetSpellHighlight(false, Color.black);
                        currentHighlightedDoor.lookingAtDoor = false;
                        currentHighlightedDoor = null;
                    }
                    if (Input.GetMouseButtonDown(1))
                    {
                        PrimedSpell = null;
                    } 

                    break;
                case "Menu":
                    SpellDrawingController.ClearPage();
                    PrimedSpell = null;

                    // MenuOptions.PauseGame() shows the menu and freezes the game via Time.timeScale,
                    // which stops the monster, its leg IK, physics and timers all at once.
                    if (menuOptions == null) menuOptions = FindAnyObjectByType<MenuOptions>(FindObjectsInactive.Include);
                    if (menuOptions != null)
                    {
                        menuOptions.PauseGame();
                    }
                    else
                    {
                        Debug.LogWarning("Menu spell cast but no MenuOptions found in the scene.");
                    }


                    break;
                case "Fireball":
                    SpellDrawingController.ClearPage();
                    //PrimedSpell = null;
                    // psudo code for fireball spell

                    // animation -- hand goes out and fireball is cast

                    // features
                    // fireball is a projectile that can be aimed and will explode on impact with an object or enemy
                    // prime on cast and throw on left click
                    // destroyed on impact with an object or after 15 seconds
                    // if it hits an object with the "WoodBaracade" tag, it will light the object on fire and destroy it after 3 seconds

                    if (Input.GetMouseButtonDown(0))
                        {
                            GameObject fireball = Instantiate(Resources.Load("Fireball") as GameObject, context.castOrigin.position + context.castOrigin.forward * 2, Quaternion.identity);
                            fireball.GetComponent<Rigidbody>().AddForce(context.castOrigin.forward * 300f);
                            PlayRelease("Fireball");
                            PrimedSpell = null;
                        }
                    if (Input.GetMouseButtonDown(1))
                    {
                        PrimedSpell = null;
                    }               

                    



                    break;
                case "Noise":
                    SpellDrawingController.ClearPage();
                    //PrimedSpell = null;

                    if (Input.GetMouseButtonDown(0))
                        {
                            GameObject Lure = Instantiate(Resources.Load("Lure") as GameObject, context.castOrigin.position + context.castOrigin.forward * 2, Quaternion.identity);
                            Lure.GetComponent<Rigidbody>().AddForce(context.castOrigin.forward * 300f);
                            PlayRelease("Noise");
                            PrimedSpell = null;
                        }
                    if (Input.GetMouseButtonDown(1))
                    {
                        PrimedSpell = null;
                    }    

                break;
            }

            if (PrimedSpell != displayedSpell) UpdateReadyDisplay();
        }
    }
}