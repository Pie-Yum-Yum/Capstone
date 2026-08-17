using System;
using UnityEngine;

namespace SaveSystem
{
    public interface ICheckpointSaveable
    {
        string SaveId { get; }
        object CaptureState();
        void RestoreState(object state);
    }

    public static class CheckpointSaveIdUtility
    {
        public static string GetStableSaveId(Component component)
        {
            if (component == null)
            {
                return string.Empty;
            }

            string path = string.Empty;
            Transform current = component.transform;
            while (current != null)
            {
                string segment = current.name + "(" + current.GetSiblingIndex() + ")";
                path = string.IsNullOrEmpty(path) ? segment : segment + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }

    [Serializable]
    public class CheckpointStateEntry
    {
        public string saveId;
        public string typeName;
        public string stateJson;
    }
}
