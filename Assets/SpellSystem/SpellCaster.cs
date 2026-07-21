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
        public GameObject EyeTint;

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

                    break;
                case "Eye":
                    SpellDrawingController.ClearPage();
                    PrimedSpell = null;
                    // psudo code for eye spell

                    // animation -- cover eyes / close eyes
                    // move player cam up
                    // tint screen yellow
                    // after 5 seconds, remove tint and move cam back down

                    // stop all other player movement and actions for 5 seconds and allow the player to look all around (unclamp the camera from the 90*)
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    PlayerCam.paused = false;
                    playerMovement.allowMovement = false;
                    

                    PlayerCam.transform.position += new Vector3(0, 5, 0);
                    EyeTint.SetActive(true);
                    
                    // wait 5 seconds
                    Invoke("EndEye", 5f);


                    break;
            }
        }
        private void EndEye()
        {
            EyeTint.SetActive(false);
            PlayerCam.transform.position -= new Vector3(0, 5, 0);
            playerMovement.allowMovement = true;
            
        }
    }
}