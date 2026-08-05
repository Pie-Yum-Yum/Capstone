using SpellSystem;
using UnityEngine;

public class NoteBookPage : MonoBehaviour
{
    public Transform playerCamera;
    public SpellDrawingController spellDrawingController;
    public string SpellName;
    public float interactDistance = 3f;
    public KeyCode interactKey = KeyCode.E;
    private bool isPlayerLookingAtPage = false;
    private Material material;
    private Renderer objectRenderer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        objectRenderer = GetComponent<Renderer>();
        material = objectRenderer.material;
    }
    // Update is called once per frame
    void Update()
    {
        bool withinDistance = Vector3.Distance(playerCamera.position, transform.position) <= interactDistance;
        if (withinDistance)
        {
            Ray ray = new Ray(playerCamera.position, playerCamera.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
            {
                isPlayerLookingAtPage = hit.transform == transform || hit.transform.IsChildOf(transform);
                material.SetColor("_Albedo", Color.yellow);
            }
            else
            {
                isPlayerLookingAtPage = false;
                material.SetColor("_Albedo", Color.white);
            }
            
            if (isPlayerLookingAtPage && Input.GetKeyDown(interactKey))
            {
                spellDrawingController.UnlockSpell(SpellName);
                Debug.Log($"Unlocked spell: {SpellName}");
                material.SetColor("_Albedo", Color.blue);
                gameObject.GetComponent<DissolveObject>().TriggerDissolve();
                
            }
        }
    }
}
