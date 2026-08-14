using System;
using System.Collections.Generic;
using System.IO;
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
    public class SaveData
    {
        public string checkpointName;
        public string sceneName;
        public int sceneIndex;
        public Vector3 playerPosition;
        public Vector3 playerRotation;
        public Vector3 monsterPosition;
        public Vector3 monsterRotation;
        public List<DoorSaveState> doorStates = new List<DoorSaveState>();
        public List<string> unlockedSpells = new List<string>();
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
            doorStates = CaptureDoorStates(),
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

        player.position = data.playerPosition;
        player.rotation = Quaternion.Euler(data.playerRotation);

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        ApplyMonsterState(data);
        ApplyDoorStates(data);
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
    }

    private void ApplyDoorStates(SaveData data)
    {
        if (data == null || data.doorStates == null)
        {
            return;
        }

        door[] doors = FindObjectsOfType<door>();
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
        door[] doors = FindObjectsOfType<door>();

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
