using UnityEngine;

namespace SpellSystem
{
    public class SpellCaster : MonoBehaviour
    {
        public SpellDrawingController SpellDrawingController;
        private SpellCastContext context;
        private string PrimedSpell = null;
        public Animator animator;
        public PlayerCam playerCam;

        private door currentHighlightedDoor;

        public void CastLockSpell(SpellCastContext incomingContext)
        {
            context = incomingContext;
            PrimedSpell = "Lock";
            animator.SetTrigger("Trigger2");
            playerCam.paused = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
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
            }
        }
    }
}