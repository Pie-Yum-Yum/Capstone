using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SaveSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSaveManager : MonoBehaviour
{
    private const string SaveFileName = "latest-save.json";

    private static GameSaveManager instance;
    private static SaveData pendingLoadData;
    private static Transform pendingPlayer;

    public static GameSaveManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<GameSaveManager>();
            }

            return instance;
        }
    }

    [Serializable]
    public class DoorSaveState
    {
        public string doorName;
        public bool isOpen;
        public bool isLocked;
        public bool isBroken;
        public Vector3 hingeLocalRotation;
    }

    [Serializable]
    public class SavedPickupState
    {
        public string pickupName;
        public string spellName;
        public bool isCollected;
    }

    [Serializable]
    public class BreakableSaveState
    {
        public string objectName;
        public bool isDestroyed;
    }

    [Serializable]
    public class SaveData
    {
        public string checkpointName;
        public string sceneName;
        public int sceneIndex;
        public Vector3 playerPosition;
        public Vector3 playerRotation;
        public Vector3 monsterPosition;
        public Vector3 monsterRotation;
        public List<CheckpointStateEntry> checkpointEntries = new List<CheckpointStateEntry>();
        public List<string> unlockedSpells = new List<string>();

        [Obsolete("Legacy field retained for backwards compatibility with older save files.")]
        public List<DoorSaveState> doorStates = new List<DoorSaveState>();

        [Obsolete("Legacy field retained for backwards compatibility with older save files.")]
        public List<SavedPickupState> pickupStates = new List<SavedPickupState>();

        [Obsolete("Legacy field retained for backwards compatibility with older save files.")]
        public List<BreakableSaveState> breakableStates = new List<BreakableSaveState>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pendingLoadData == null || pendingPlayer == null)
        {
            return;
        }

        ApplyLoadedState(pendingPlayer, pendingLoadData);
        pendingLoadData = null;
        pendingPlayer = null;
    }

    public bool HasSave()
    {
        return File.Exists(GetSavePath());
    }

    public void SaveCheckpoint(string checkpointName, Transform player)
    {
        if (player == null)
        {
            Debug.LogWarning("SaveCheckpoint called without a valid player transform.");
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        SaveData data = new SaveData
        {
            checkpointName = checkpointName,
            sceneName = activeScene.name,
            sceneIndex = activeScene.buildIndex,
            playerPosition = player.position,
            playerRotation = player.eulerAngles,
            monsterPosition = GetMonsterPosition(),
            monsterRotation = GetMonsterRotation(),
            checkpointEntries = CaptureCheckpointEntries(),
            unlockedSpells = GetUnlockedSpells()
        };

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetSavePath(), json);
        Debug.Log("Checkpoint saved: " + checkpointName + " at " + player.position);
    }

    public bool TryLoadLatestSave(Transform player)
    {
        if (player == null)
        {
            Debug.LogWarning("TryLoadLatestSave called without a valid player transform.");
            return false;
        }

        if (!HasSave())
        {
            return false;
        }

        string json = File.ReadAllText(GetSavePath());
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        if (data == null)
        {
            Debug.LogWarning("Save file was corrupt or empty.");
            return false;
        }

        if (SceneManager.GetActiveScene().buildIndex != data.sceneIndex)
        {
            pendingLoadData = data;
            pendingPlayer = player;
            SceneManager.LoadScene(data.sceneIndex);
            return true;
        }

        ApplyLoadedState(player, data);
        return true;
    }

    private void ApplyLoadedState(Transform player, SaveData data)
    {
        if (player == null || data == null)
        {
            return;
        }

        player.SetPositionAndRotation(data.playerPosition, Quaternion.Euler(data.playerRotation));

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = data.playerPosition;
            rb.rotation = Quaternion.Euler(data.playerRotation);
            rb.Sleep();
        }

        Physics.SyncTransforms();

        ApplyMonsterState(data);

        if (data.checkpointEntries != null && data.checkpointEntries.Count > 0)
        {
            RestoreCheckpointEntries(data);
        }
        else
        {
            ApplyDoorStates(data);
            ApplyPickupStates(data);
            ApplyBreakableStates(data);
        }

        ApplyUnlockedSpells(data);

        Debug.Log("Loaded checkpoint: " + data.checkpointName + " in scene " + data.sceneName);
    }

    private void ApplyMonsterState(SaveData data)
    {
        Transform monster = GetMonsterTransform();
        if (monster == null)
        {
            return;
        }

        monster.position = data.monsterPosition;
        monster.rotation = Quaternion.Euler(data.monsterRotation);

        Rigidbody monsterRb = monster.GetComponent<Rigidbody>();
        if (monsterRb != null)
        {
            monsterRb.linearVelocity = Vector3.zero;
            monsterRb.angularVelocity = Vector3.zero;
        }

        MonsterAI monsterAi = monster.GetComponent<MonsterAI>();
        if (monsterAi != null)
        {
            monsterAi.ResetToPatrolState();
        }
    }

    private List<CheckpointStateEntry> CaptureCheckpointEntries()
    {
        var saveables = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OfType<ICheckpointSaveable>()
            .Where(s => !string.IsNullOrWhiteSpace(s.SaveId))
            .GroupBy(s => s.SaveId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        List<CheckpointStateEntry> entries = new List<CheckpointStateEntry>();
        foreach (ICheckpointSaveable saveable in saveables)
        {
            object state = saveable.CaptureState();
            if (state == null)
            {
                continue;
            }

            string json = JsonUtility.ToJson(state);
            entries.Add(new CheckpointStateEntry
            {
                saveId = saveable.SaveId,
                typeName = state.GetType().AssemblyQualifiedName,
                stateJson = json
            });
        }

        return entries;
    }

    private void RestoreCheckpointEntries(SaveData data)
    {
        if (data == null || data.checkpointEntries == null)
        {
            return;
        }

        var saveables = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OfType<ICheckpointSaveable>()
            .GroupBy(saveable => saveable.SaveId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (CheckpointStateEntry entry in data.checkpointEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.saveId) || !saveables.TryGetValue(entry.saveId, out ICheckpointSaveable saveable))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.stateJson))
            {
                continue;
            }

            Type stateType = Type.GetType(entry.typeName);
            if (stateType == null)
            {
                continue;
            }

            object restoredState = JsonUtility.FromJson(entry.stateJson, stateType);
            if (restoredState != null)
            {
                saveable.RestoreState(restoredState);
            }
        }
    }

    private void ApplyDoorStates(SaveData data)
    {
        if (data == null || data.doorStates == null)
        {
            return;
        }

        door[] doors = FindObjectsByType<door>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (door currentDoor in doors)
        {
            foreach (DoorSaveState doorState in data.doorStates)
            {
                if (!string.Equals(currentDoor.name, doorState.doorName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                currentDoor.ApplySavedState(doorState.isOpen, doorState.isLocked, doorState.hingeLocalRotation, doorState.isBroken);
                break;
            }
        }
    }

    private void ApplyPickupStates(SaveData data)
    {
        if (data == null || data.pickupStates == null)
        {
            return;
        }

        NoteBookPage[] pages = FindObjectsByType<NoteBookPage>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (NoteBookPage page in pages)
        {
            foreach (SavedPickupState pickupState in data.pickupStates)
            {
                if (!string.Equals(page.name, pickupState.pickupName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                page.ApplySavedState(pickupState.isCollected, pickupState.spellName);
                break;
            }
        }
    }

    private void ApplyBreakableStates(SaveData data)
    {
        if (data == null || data.breakableStates == null)
        {
            return;
        }

        DissolveObject[] breakables = FindObjectsByType<DissolveObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (DissolveObject breakable in breakables)
        {
            foreach (BreakableSaveState savedState in data.breakableStates)
            {
                if (!string.Equals(breakable.name, savedState.objectName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                breakable.ApplySavedState(savedState.isDestroyed);
                break;
            }
        }
    }

    private void ApplyUnlockedSpells(SaveData data)
    {
        if (data == null || data.unlockedSpells == null)
        {
            return;
        }

        SpellSystem.SpellDrawingController spellUI = FindObjectOfType<SpellSystem.SpellDrawingController>();
        if (spellUI == null)
        {
            return;
        }

        var unlocked = spellUI.GetUnlockedSpells();
        if (unlocked == null)
        {
            return;
        }

        unlocked.Clear();
        foreach (string spellName in data.unlockedSpells)
        {
            if (!string.IsNullOrWhiteSpace(spellName))
            {
                unlocked.Add(spellName);
            }
        }
    }

    private List<DoorSaveState> CaptureDoorStates()
    {
        List<DoorSaveState> states = new List<DoorSaveState>();
        door[] doors = FindObjectsByType<door>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (door currentDoor in doors)
        {
            Baracade baracade = currentDoor.GetComponentInParent<Baracade>();
            DoorSaveState state = new DoorSaveState
            {
                doorName = currentDoor.name,
                isOpen = currentDoor.IsOpen,
                isLocked = currentDoor.IsLocked,
                isBroken = baracade != null && baracade.IsBroken,
                hingeLocalRotation = currentDoor.HingeLocalEuler
            };

            states.Add(state);
        }

        return states;
    }

    private List<SavedPickupState> CapturePickupStates()
    {
        List<SavedPickupState> states = new List<SavedPickupState>();
        NoteBookPage[] pages = FindObjectsByType<NoteBookPage>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (NoteBookPage page in pages)
        {
            states.Add(new SavedPickupState
            {
                pickupName = page.name,
                spellName = page.SpellName,
                isCollected = page.IsCollected
            });
        }

        return states;
    }

    private List<BreakableSaveState> CaptureBreakableStates()
    {
        List<BreakableSaveState> states = new List<BreakableSaveState>();
        DissolveObject[] breakables = FindObjectsByType<DissolveObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (DissolveObject breakable in breakables)
        {
            states.Add(new BreakableSaveState
            {
                objectName = breakable.name,
                isDestroyed = breakable.IsDestroyed
            });
        }

        return states;
    }

    private Vector3 GetMonsterPosition()
    {
        Transform monster = GetMonsterTransform();
        return monster != null ? monster.position : Vector3.zero;
    }

    private Vector3 GetMonsterRotation()
    {
        Transform monster = GetMonsterTransform();
        return monster != null ? monster.eulerAngles : Vector3.zero;
    }

    private Transform GetMonsterTransform()
    {
        GameObject monsterObject = GameObject.FindWithTag("Monster");
        if (monsterObject != null)
        {
            return monsterObject.transform;
        }

        MonsterAI monster = FindObjectOfType<MonsterAI>();
        return monster != null ? monster.transform : null;
    }

    private List<string> GetUnlockedSpells()
    {
        SpellSystem.SpellDrawingController spellUI = FindObjectOfType<SpellSystem.SpellDrawingController>();
        if (spellUI == null)
        {
            return new List<string>();
        }

        HashSet<string> unlocked = spellUI.GetUnlockedSpells();
        List<string> output = new List<string>();
        if (unlocked == null)
        {
            return output;
        }

        foreach (string spellName in unlocked)
        {
            output.Add(spellName);
        }

        return output;
    }

    public static string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, SaveFileName);
    }
}
