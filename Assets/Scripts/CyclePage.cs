using UnityEngine;
using System.Collections.Generic;
using SpellSystem;
using Unity.VisualScripting;
using System.Collections;

public class CyclePage : MonoBehaviour
{
    private HashSet<string> unlockedSpells = new HashSet<string>();
    public SpellDrawingController spellDrawingController;
    public PlayerCam playerCam;
    public Mesh LockTexture;
    public Mesh FireballTexture;
    public Mesh LureTexture;
    public Mesh BaseTexture;
    private MeshFilter targetRenderer;
    public Animator animator;
    //public int targetMaterialElement = 1;
    private string currentSpell;
    private bool canCycle = false;
    //private string[] spellOrder = new string[] {"Fireball", "Lure", "Lock"};

    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        unlockedSpells = spellDrawingController.GetUnlockedSpells();
        targetRenderer = GetComponent<MeshFilter>();
        
        //material.SetTexture("_Albedo", BaseTexture);
        currentSpell = "base";

        // initialize target renderer's element to base texture if available
        SetTargetTexture(BaseTexture);

    }
    void Update()
    {
        // if playercam is paused, do not allow cycling
        // if not paused, cast a ray from mouse to the button, if it hits the button and the user clicks, cycle the spell paper
        if (!playerCam.paused)
        {
            
            GetComponent<MeshCollider>().enabled = false;
            return;
        }
        else
        {
            GetComponent<MeshCollider>().enabled = true;
            //Ray ray = new Ray(playerCam.transform.position, (Input.mousePosition - new Vector3(Screen.width / 2, Screen.height / 2, 0)).normalized);
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 3f))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    //Debug.Log("Hit");
                    if (Input.GetMouseButtonDown(0))
                    {
                        cycle();
                        Debug.Log("Spell Cycled");
                    }
                }
            }
        }
    }
    private void cycle()
    {
        if (!canCycle) return;
        unlockedSpells = spellDrawingController.GetUnlockedSpells();
        Debug.Log("Unlocked Spells: " + string.Join(", ", unlockedSpells));
        Debug.Log("Current Spell: " + currentSpell);
        if (unlockedSpells.Count == 0)
        {
            SetTargetTexture(BaseTexture);
            currentSpell = "base";
            return;
        }

        // base -> fireball -> lure -> lock -> fireball
        //currentSpell = nextSpell;

        if(currentSpell == "base")
        {
            //nextSpell = "Fireball";
            if (unlockedSpells.Contains("Fireball"))
            {
                SetTargetTexture(FireballTexture);
                currentSpell = "Fireball";
            }
            else if (unlockedSpells.Contains("Noise"))
            {
                SetTargetTexture(LureTexture);
                currentSpell = "Noise";
            }
            else if (unlockedSpells.Contains("Lock"))
            {
                SetTargetTexture(LockTexture);
                currentSpell = "Lock";
            }
        }
        
        else if(currentSpell == "Fireball")
        {
            if (unlockedSpells.Contains("Noise"))
            {
                SetTargetTexture(LureTexture);
                currentSpell = "Noise";
            }
            else if (unlockedSpells.Contains("Lock"))
            {
                Debug.Log("Switching to Lock from fireball");
                SetTargetTexture(LockTexture);
                currentSpell = "Lock";
            }else{
                return;
            }
        }
        
        else if(currentSpell == "Noise")
        {
            if (unlockedSpells.Contains("Lock"))
            {
                SetTargetTexture(LockTexture);
                currentSpell = "Lock";
            }
            else if (unlockedSpells.Contains("Fireball"))
            {
                SetTargetTexture(FireballTexture);
                currentSpell = "Fireball";
            }else{
                return;
            }
        }

        else if(currentSpell == "Lock")
        {
            if (unlockedSpells.Contains("Fireball"))
            {
                SetTargetTexture(FireballTexture);
                currentSpell = "Fireball";
            }
            else if (unlockedSpells.Contains("Noise"))
            {
                SetTargetTexture(LureTexture);
                currentSpell = "Noise";
            }else{
                return;
            }
        }

    }

    private void SetTargetTexture(Mesh mesh)
    {
        canCycle = false; // Disable cycling while the animation is playing
        if (targetRenderer != null)
        {
            animator.SetTrigger("Trigger4");
            StartCoroutine(ResetTriggerAndUpdateMesh(mesh));
            return;
            
        }
    }

    private IEnumerator ResetTriggerAndUpdateMesh(Mesh mesh)
    {
        yield return new WaitForSeconds(0.1f);
        targetRenderer.mesh = mesh;
        yield return new WaitForSeconds(0.44f);
        animator.ResetTrigger("Trigger4");
        canCycle = true; // Re-enable cycling after the animation is done
        
    }

    
}
