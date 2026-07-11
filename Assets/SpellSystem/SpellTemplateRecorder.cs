using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SpellSystem
{
    /// <summary>
    /// TEMP / DEV TOOL - not meant to ship with the game.
    /// Drop on the same object as SpellDrawingController. Draw one variation
    /// of a spell in Play mode, then press the export key (default: E) to
    /// save it as a JSON sample file that SpellDatabase will automatically
    /// pick up on next load. No code editing required - just draw, export,
    /// repeat for as many variations as you want, drop the files in, done.
    ///
    /// Files are written to Assets/Resources/SpellSamples/ so they can be
    /// loaded at runtime via Resources.LoadAll, matching SpellDatabase's
    /// expectations. In the Editor this writes directly there; if you ever
    /// run this from a built game instead, redirect the path (see WriteDirectory).
    /// </summary>
    [RequireComponent(typeof(SpellDrawingController))]
    public class SpellTemplateRecorder : MonoBehaviour
    {
        [SerializeField] private KeyCode exportKey = KeyCode.E;
        [SerializeField] private string spellName = "Hourglass";

        private SpellDrawingController controller;

        private void Awake()
        {
            controller = GetComponent<SpellDrawingController>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(exportKey))
                Export();
        }

        private void Export()
        {
            var strokes = controller.GetCurrentStrokes();
            if (strokes == null || strokes.Count == 0)
            {
                Debug.LogWarning("No strokes drawn yet - draw the spell before exporting.");
                return;
            }

            string safeName = spellName.Replace(" ", "");
            string dir = WriteDirectory();
            Directory.CreateDirectory(dir);

            int nextIndex = NextAvailableIndex(dir, safeName);
            string fileName = $"{safeName}_{nextIndex}.json";
            string path = Path.Combine(dir, fileName);

            var sampleFile = new SpellSampleFile
            {
                strokes = strokes.Select(s => new StrokeData(new List<Vector2>(s))).ToList()
            };
            string json = JsonUtility.ToJson(sampleFile, prettyPrint: true);
            File.WriteAllText(path, json);

            Debug.Log($"Exported '{spellName}' sample #{nextIndex} to: {path}");

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh(); // makes the new file show up as a loadable TextAsset immediately
#endif
        }

        // Points at Assets/Resources/SpellSamples so files are usable by
        // Resources.LoadAll at runtime, not just readable as raw text in the Editor.
        private string WriteDirectory()
        {
            return Path.Combine(Application.dataPath, "Resources", "SpellSamples");
        }

        // Scans existing files so re-exporting doesn't overwrite earlier
        // samples of the same spell - just keeps incrementing the index.
        private int NextAvailableIndex(string dir, string safeName)
        {
            int index = 0;
            while (File.Exists(Path.Combine(dir, $"{safeName}_{index}.json")))
                index++;
            return index;
        }
    }
}