using UnityEngine;
using UnityEngine.SceneManagement;

// Sends the player back to the main menu when they walk into this object's trigger (e.g. the end of a level).
//
// SETUP: put this on a GameObject with a collider (Is Trigger gets ticked automatically).
// The player must be tagged "Player", and the scene named below must be in the Build Profile's scene list.
[RequireComponent(typeof(Collider))]
public class MainMenuTrigger : MonoBehaviour
{
    [Tooltip("Scene to load, by name. Loading by name keeps working if the scene order in the Build Profile changes.")]
    public string sceneName = "MainMenu";

    private bool triggered;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered || !other.CompareTag("Player")) return;
        triggered = true;

        // The level locks and hides the cursor, and the main menu doesn't undo that itself.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene(sceneName);
    }
}
