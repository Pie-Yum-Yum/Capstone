using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CheckpointTrigger : MonoBehaviour
{
    [Tooltip("Name used in the save file so you can tell which checkpoint was last reached.")]
    public string checkpointName = "Checkpoint";

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (GameSaveManager.Instance == null)
        {
            Debug.LogWarning("No GameSaveManager exists in the scene. Add one to the scene before using checkpoints.");
            return;
        }

        GameSaveManager.Instance.SaveCheckpoint(checkpointName, other.transform);
    }
}
