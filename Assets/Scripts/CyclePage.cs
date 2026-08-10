using UnityEngine;
using System.Collections.Generic;
using SpellSystem;
using Unity.VisualScripting;

public class CyclePage : MonoBehaviour
{
    private HashSet<string> unlockedSpells = new HashSet<string>();
    public SpellDrawingController spellDrawingController;
    public PlayerCam playerCam;
    public Texture LockTexture;
    public Texture FireballTexture;
    public Texture LureTexture;
    public Texture BaseTexture;
    public SkinnedMeshRenderer targetSkinnedMeshRenderer;
    public int targetMaterialElement = 1;
    private string currentSpell;
    
    private Renderer objectRenderer;
    private Material material;
    //private string[] spellOrder = new string[] {"Fireball", "Lure", "Lock"};

    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        unlockedSpells = spellDrawingController.GetUnlockedSpells();
        
        objectRenderer = GetComponent<Renderer>();
        material = objectRenderer.material;
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
        unlockedSpells = spellDrawingController.GetUnlockedSpells();
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
            else if (unlockedSpells.Contains("Lure"))
            {
                SetTargetTexture(LureTexture);
                currentSpell = "Lure";
            }
            else if (unlockedSpells.Contains("Lock"))
            {
                SetTargetTexture(LockTexture);
                currentSpell = "Lock";
            }
        }
        
        else if(currentSpell == "Fireball")
        {
            if (unlockedSpells.Contains("Lure"))
            {
                SetTargetTexture(LureTexture);
                currentSpell = "Lure";
            }
            else if (unlockedSpells.Contains("Lock"))
            {
                SetTargetTexture(LockTexture);
                currentSpell = "Lock";
            }else{
                return;
            }
        }
        
        else if(currentSpell == "Lure")
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
            else if (unlockedSpells.Contains("Lure"))
            {
                SetTargetTexture(LureTexture);
                currentSpell = "Lure";
            }else{
                return;
            }
        }

    }

    private void SetTargetTexture(Texture tex)
    {
        if (targetSkinnedMeshRenderer != null)
        {
            var mats = targetSkinnedMeshRenderer.materials;
            if (mats != null && mats.Length > targetMaterialElement && mats[targetMaterialElement] != null)
            {
                mats[targetMaterialElement].SetTexture("_Albedo", tex);
                //targetSkinnedMeshRenderer.materials = mats;
                return;
            }
        }

        // fallback to this object's material if target not set or invalid
        if (material != null)
        {
            material.SetTexture("_Albedo", tex);
        }
    }

    
}
