using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpellSystem;

public class CyclePage : MonoBehaviour
{
    [System.Serializable]
    public class SpellPage
    {
        public string spellName;   // must match the name used by SpellDrawingController (e.g. "Fireball", "Noise", "Lock")
        public Mesh mesh;
    }

    public SpellDrawingController spellDrawingController;
    public PlayerCam playerCam;
    public Animator animator;
    public Mesh baseMesh;
    public List<SpellPage> spellPages = new List<SpellPage>(); // cycle order = list order

    private MeshFilter meshFilter;
    private MeshCollider meshCollider;
    private int currentIndex = -1;   // -1 means the base page
    private bool canCycle;

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
        ShowPage(-1, false);
        //meshFilter.mesh = baseMesh;
    }

    void Update()
    {
        bool paused = playerCam.paused;
        meshCollider.enabled = paused;

        if (!paused || !Input.GetMouseButtonDown(0)) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 3f) && hit.transform.IsChildOf(transform))
            Cycle();
    }

    private void Cycle()
    {
        if (!canCycle) return;

        int next = FindNextUnlocked(spellDrawingController.GetUnlockedSpells());
        if (next == currentIndex) return;   // nothing new to show

        ShowPage(next);
    }

    // Walks the list starting after the current page, wrapping around.
    // Returns -1 (base page) if nothing is unlocked.
    private int FindNextUnlocked(HashSet<string> unlocked)
    {
        for (int i = 1; i <= spellPages.Count; i++)
        {
            int index = (currentIndex + i) % spellPages.Count;
            if (unlocked.Contains(spellPages[index].spellName))
                return index;
        }
        return -1;
    }

    private void ShowPage(int index)
    {
        currentIndex = index;
        Mesh mesh = index < 0 ? baseMesh : spellPages[index].mesh;

        canCycle = false;
        animator.SetTrigger("Trigger4");
        StartCoroutine(SwapMeshAfterAnimation(mesh));
    }

    private void ShowPage(int index, bool animate)
    {
        currentIndex = index;
        Mesh mesh = index < 0 ? baseMesh : spellPages[index].mesh;

        canCycle = false;
        if (animate) animator.SetTrigger("Trigger4");
        StartCoroutine(SwapMeshAfterAnimation(mesh));
    }

    private IEnumerator SwapMeshAfterAnimation(Mesh mesh)
    {
        yield return new WaitForSeconds(0.1f);
        meshFilter.mesh = mesh;
        yield return new WaitForSeconds(0.44f);
        animator.ResetTrigger("Trigger4");
        canCycle = true;
    }
}