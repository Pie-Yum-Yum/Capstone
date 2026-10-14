using UnityEditor;
using UnityEngine;

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

        private door currentHighlightedDoor;

        public void CastLockSpell(SpellCastContext incomingContext)
        {
            context = incomingContext;
            PrimedSpell = "Lock";
            animator.SetTrigger("Trigger2");
            PlayerCam.paused = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        public void CastEyeSpell(SpellCastContext incomingContext)
        {
            context = incomingContext;
            PrimedSpell = "Eye";
            animator.SetTrigger("Trigger2");
        }
        public void CastFireballSpell(SpellCastContext incomingContext)
        {
            context = incomingContext;
            PrimedSpell = "Fireball";
            animator.SetTrigger("Trigger2");
            PlayerCam.paused = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void CastNoiseSpell(SpellCastContext incomingContext)
        {
            context = incomingContext;
            PrimedSpell = "Noise";
            animator.SetTrigger("Trigger2");
            PlayerCam.paused = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void CastMenuSpell(SpellCastContext incomingContext)
        {
            context = incomingContext;
            PrimedSpell = "Menu";
            animator.SetTrigger("Trigger2");
            //PlayerCam.paused = false;
            //Cursor.lockState = CursorLockMode.Locked;
            //Cursor.visible = false;
            menuOptions.PauseGame();    
        }
        

        void Update()
        {
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
                            fireball.GetComponent<Rigidbody>().AddForce(context.castOrigin.forward * 100f);
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
                            PrimedSpell = null;
                        }
                    if (Input.GetMouseButtonDown(1))
                    {
                        PrimedSpell = null;
                    }    

                break;
            }
        }
    }
}