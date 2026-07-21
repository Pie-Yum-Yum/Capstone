using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpellSystem
{
    /// <summary>
    /// The JSON shape of one exported sample file. Matches what
    /// SpellTemplateRecorder writes and what JsonUtility can parse directly.
    /// </summary>
    [Serializable]
    public class SpellSampleFile
    {
        public List<StrokeData> strokes = new List<StrokeData>();
    }

    /// <summary>
    /// Small hand-authored metadata for a spell - the stuff that ISN'T huge
    /// point data and is fine to edit by hand: display name, cast threshold,
    /// and which effect it triggers. Unlike sample geometry, there's normally
    /// only one entry per spell, so keeping this as plain code (or you could
    /// move it to a ScriptableObject/Inspector list later) is easy to manage.
    /// </summary>
    [Serializable]
    public class SpellMetadata
    {
        public string spellName;
        public float minAccuracyToCast = 0.7f;
        public string effectId;
    }

    /// <summary>
    /// Loads every spell sample file under Resources/SpellSamples and
    /// assembles them into SpellDefinitions.
    ///
    /// FILE NAMING CONVENTION: each sample file must be named
    ///     {SpellName}_{index}.json
    /// e.g. "Hourglass_0.json", "Hourglass_1.json", "Fireball_0.json".
    /// The loader groups files by the part before the last underscore, so
    /// spell name matching is driven entirely by filename - no need to
    /// touch code when you add a new sample, just drop the file in the folder.
    /// </summary>
    public static class SpellDatabase
    {
        private const string ResourcesFolder = "SpellSamples"; // maps to Assets/Resources/SpellSamples

        // Edit this list as you add spells. If a spell has sample files but
        // no entry here, it still loads - just with the default threshold
        // and no effectId, so add an entry once you're ready to wire gameplay.
        private static readonly List<SpellMetadata> Metadata = new List<SpellMetadata>
        {
            new SpellMetadata { spellName = "Hourglass", minAccuracyToCast = 0.7f, effectId = "Hourglass" },
            new SpellMetadata { spellName = "Crate", minAccuracyToCast = 0.7f, effectId = "Crate" },
            new SpellMetadata { spellName = "Lock", minAccuracyToCast = 0.7f, effectId = "Lock" },
        };

        public static List<SpellDefinition> BuildKnownSpells()
        {
            var textAssets = Resources.LoadAll<TextAsset>(ResourcesFolder);
            if (textAssets.Length == 0)
                Debug.LogWarning($"No sample files found under Resources/{ResourcesFolder}. " +
                                  "Export some with SpellTemplateRecorder first.");

            // Group files by spell name (part of filename before the last underscore).
            var samplesBySpell = new Dictionary<string, List<SpellSample>>();

            foreach (var asset in textAssets)
            {
                string spellName = ParseSpellName(asset.name);
                if (spellName == null)
                {
                    Debug.LogWarning($"Skipping '{asset.name}.json' - filename doesn't match the " +
                                      "'SpellName_index' convention.");
                    continue;
                }

                SpellSampleFile parsed;
                try
                {
                    parsed = JsonUtility.FromJson<SpellSampleFile>(asset.text);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to parse '{asset.name}.json': {e.Message}");
                    continue;
                }

                if (parsed == null || parsed.strokes == null || parsed.strokes.Count == 0)
                {
                    Debug.LogWarning($"'{asset.name}.json' parsed but had no stroke data - skipping.");
                    continue;
                }

                if (!samplesBySpell.TryGetValue(spellName, out var list))
                {
                    list = new List<SpellSample>();
                    samplesBySpell[spellName] = list;
                }
                list.Add(new SpellSample { strokes = parsed.strokes });
            }

            var result = new List<SpellDefinition>();
            foreach (var kvp in samplesBySpell)
            {
                var meta = Metadata.FirstOrDefault(m => m.spellName == kvp.Key);
                result.Add(new SpellDefinition
                {
                    spellName = kvp.Key,
                    samples = kvp.Value,
                    minAccuracyToCast = meta?.minAccuracyToCast ?? 0.7f,
                    effectId = meta?.effectId ?? kvp.Key.ToLowerInvariant()
                });
            }

            return result;
        }

        // "Hourglass_0" -> "Hourglass". Returns null if there's no underscore
        // to split on (i.e. the file doesn't follow the naming convention).
        private static string ParseSpellName(string fileName)
        {
            int lastUnderscore = fileName.LastIndexOf('_');
            if (lastUnderscore <= 0) return null;
            return fileName.Substring(0, lastUnderscore);
        }
    }
}