using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpellSystem
{
    /// <summary>
    /// One drawn example of a spell - one or more strokes. A single
    /// SpellDefinition can (and should) hold many of these: the more
    /// natural variation you sample, the more forgiving and reliable
    /// recognition becomes for real players.
    /// </summary>
    [Serializable]
    public class SpellSample
    {
        public List<StrokeData> strokes = new List<StrokeData>();

        // Cached normalized point cloud, built once via Recognizer.Prepare()
        [NonSerialized] public List<Vector2> NormalizedCloud;

        public SpellSample() { }
        public SpellSample(List<List<Vector2>> rawStrokes)
        {
            strokes = rawStrokes.Select(s => new StrokeData(s)).ToList();
        }
    }

    /// <summary>
    /// A spell: a name, gameplay hooks, and every sample drawing you've
    /// collected for it. Recognition scores the player's drawing against
    /// EVERY sample across EVERY spell and reports whichever single sample
    /// scored best, along with which spell it belongs to.
    /// </summary>
    [Serializable]
    public class SpellDefinition
    {
        public string spellName;
        public List<SpellSample> samples = new List<SpellSample>();

        [Range(0f, 1f)]
        public float minAccuracyToCast = 0.7f;

        // Hook whatever you want triggered on a successful cast here -
        // an id, a prefab reference, a UnityEvent, etc.
        public string effectId;
    }

    [Serializable]
    public class StrokeData
    {
        public List<Vector2> points = new List<Vector2>();

        public StrokeData() { }
        public StrokeData(List<Vector2> pts) { points = pts; }
    }

    public struct RecognitionResult
    {
        public SpellDefinition bestMatch;
        public SpellSample matchedSample; // which specific sample scored best - useful for debugging/tuning
        public float score;       // 0..1, higher is better
        public bool successful;   // score >= bestMatch.minAccuracyToCast
    }

    /// <summary>
    /// Static implementation of the $P Point-Cloud gesture recognizer.
    /// Reference: Vatavu, Anthony & Wobbrock, "Gestures as Point Clouds:
    /// A $P Recognizer for User Interface Prototypes" (ICMI 2012).
    ///
    /// Works naturally with any number of strokes and does not care about
    /// stroke order or drawing direction - which is what makes it a good
    /// fit for multi-line spells drawn freehand with a mouse.
    /// </summary>
    public static class GestureRecognizer
    {
        // Number of points every gesture (candidate or template) is resampled to.
        // Higher = more precise matching but slower. 32-64 is a good range.
        public const int ResamplePointCount = 32;

        /// <summary>
        /// Call once per sample after loading (e.g. on game start) to
        /// precompute its normalized point cloud so recognition at cast-time
        /// only has to normalize the player's drawing, not every sample.
        /// </summary>
        public static void Prepare(SpellSample sample)
        {
            var raw = FlattenStrokes(sample.strokes.Select(s => s.points).ToList());
            sample.NormalizedCloud = Normalize(raw);
        }

        /// <summary>Prepares every sample belonging to a spell.</summary>
        public static void Prepare(SpellDefinition spell)
        {
            foreach (var sample in spell.samples)
                if (sample.NormalizedCloud == null) Prepare(sample);
        }

        /// <summary>
        /// Recognize a freshly-drawn gesture (list of strokes, each a list of
        /// points in the same local space the samples were authored in)
        /// against every sample of every provided spell. The single
        /// best-scoring sample, from any spell, wins.
        /// </summary>
        public static RecognitionResult Recognize(List<List<Vector2>> drawnStrokes, IEnumerable<SpellDefinition> spells)
        {
            var raw = FlattenStrokes(drawnStrokes);
            if (raw.Count < 2)
                return new RecognitionResult { bestMatch = null, matchedSample = null, score = 0f, successful = false };

            var candidate = Normalize(raw);

            SpellDefinition best = null;
            SpellSample bestSample = null;
            float bestDistance = float.MaxValue;

            foreach (var spell in spells)
            {
                foreach (var sample in spell.samples)
                {
                    if (sample.NormalizedCloud == null) Prepare(sample);

                    float distance = GreedyCloudMatch(candidate, sample.NormalizedCloud);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = spell;
                        bestSample = sample;
                    }
                }
            }

            // Convert raw greedy-cloud distance into a friendlier 0..1 score.
            // The divisor is a tunable "how far is a total miss" constant -
            // start here and adjust based on playtesting with your actual shapes.
            const float maxExpectedDistance = 0.35f;
            float score = Mathf.Clamp01(1f - (bestDistance / maxExpectedDistance));

            return new RecognitionResult
            {
                bestMatch = best,
                matchedSample = bestSample,
                score = score,
                successful = best != null && score >= best.minAccuracyToCast
            };
        }

        // ---- Internals ----------------------------------------------------

        // Flattens multiple strokes into one ordered point list, splitting the
        // fixed ResamplePointCount budget across strokes proportional to each
        // stroke's share of total drawn length. This matters for multi-stroke
        // spells: a long looping stroke shouldn't get the same weight as a
        // short slash.
        //
        // IMPORTANT: the $P matching below requires every normalized cloud -
        // candidate and every stored sample - to have exactly the same point
        // count. Naively rounding each stroke's share independently can drift
        // above or below the target total once more than one stroke is
        // involved (e.g. 15+16 instead of 16+16), so this uses a
        // largest-remainder apportionment to guarantee the sum is always
        // exactly ResamplePointCount, no matter how many strokes there are.
        private static List<Vector2> FlattenStrokes(List<List<Vector2>> strokes)
        {
            var validStrokes = strokes.Where(s => s != null && s.Count > 0).ToList();
            if (validStrokes.Count == 0) return new List<Vector2>();

            int strokeCount = validStrokes.Count;
            int[] counts = AllocatePointsPerStroke(validStrokes, strokeCount);

            var result = new List<Vector2>();
            for (int i = 0; i < strokeCount; i++)
                result.AddRange(Resample(validStrokes[i], counts[i]));

            return result;
        }

        private static int[] AllocatePointsPerStroke(List<List<Vector2>> validStrokes, int strokeCount)
        {
            const int minPerStroke = 1; // every stroke must be represented by at least one point
            var counts = new int[strokeCount];

            if (strokeCount >= ResamplePointCount)
            {
                // Pathological case: more strokes than the point budget. Just
                // give everyone the minimum; total may exceed the target
                // slightly, but this only happens with an unreasonable number
                // of strokes for a single gesture.
                for (int i = 0; i < strokeCount; i++) counts[i] = minPerStroke;
                return counts;
            }

            float[] lengths = validStrokes.Select(PathLength).ToArray();
            float totalLength = lengths.Sum();

            if (totalLength <= Mathf.Epsilon)
            {
                // No usable length info (e.g. every stroke is a single point) - split evenly.
                int basePerStroke = ResamplePointCount / strokeCount;
                int remainder = ResamplePointCount - basePerStroke * strokeCount;
                for (int i = 0; i < strokeCount; i++)
                    counts[i] = basePerStroke + (i < remainder ? 1 : 0);
                return counts;
            }

            var fractional = new float[strokeCount];
            int allocated = 0;
            for (int i = 0; i < strokeCount; i++)
            {
                float share = ResamplePointCount * (lengths[i] / totalLength);
                counts[i] = Mathf.Max(minPerStroke, Mathf.FloorToInt(share));
                fractional[i] = share - Mathf.Floor(share);
                allocated += counts[i];
            }

            // Distribute leftover/deficit points via largest-remainder order
            // until the total exactly matches ResamplePointCount.
            int diff = ResamplePointCount - allocated;
            var order = Enumerable.Range(0, strokeCount).OrderByDescending(i => fractional[i]).ToArray();
            int guard = 0;
            int maxIterations = strokeCount * 10 + ResamplePointCount; // safety net against edge-case infinite loops
            while (diff != 0 && guard < maxIterations)
            {
                int i = order[guard % strokeCount];
                if (diff > 0)
                {
                    counts[i]++;
                    diff--;
                }
                else if (counts[i] > minPerStroke)
                {
                    counts[i]--;
                    diff++;
                }
                guard++;
            }

            return counts;
        }

        private static float PathLength(List<Vector2> pts)
        {
            float len = 0f;
            for (int i = 1; i < pts.Count; i++)
                len += Vector2.Distance(pts[i - 1], pts[i]);
            return len;
        }

        private static List<Vector2> Resample(List<Vector2> points, int n)
        {
            if (points.Count == 1) return Enumerable.Repeat(points[0], n).ToList();

            float interval = PathLength(points) / Mathf.Max(1, n - 1);
            if (interval <= Mathf.Epsilon)
                return Enumerable.Repeat(points[0], n).ToList();

            var src = new List<Vector2>(points);
            var resampled = new List<Vector2> { src[0] };
            float D = 0f;

            for (int i = 1; i < src.Count; i++)
            {
                float d = Vector2.Distance(src[i - 1], src[i]);
                if (D + d >= interval)
                {
                    float t = (interval - D) / d;
                    Vector2 q = Vector2.Lerp(src[i - 1], src[i], t);
                    resampled.Add(q);
                    src.Insert(i, q);
                    D = 0f;
                }
                else
                {
                    D += d;
                }
            }

            // Rounding can leave us one point short/long - pad or trim.
            while (resampled.Count < n) resampled.Add(src[src.Count - 1]);
            if (resampled.Count > n) resampled.RemoveRange(n, resampled.Count - n);

            return resampled;
        }

        // Scales the cloud to a unit bounding box (non-uniform) and
        // translates it so its centroid sits at the origin. This removes
        // sensitivity to where on the "page" the player drew and at what size.
        private static List<Vector2> Normalize(List<Vector2> points)
        {
            float minX = points.Min(p => p.x), maxX = points.Max(p => p.x);
            float minY = points.Min(p => p.y), maxY = points.Max(p => p.y);
            float w = Mathf.Max(maxX - minX, 0.0001f);
            float h = Mathf.Max(maxY - minY, 0.0001f);

            var scaled = points.Select(p => new Vector2(
                (p.x - minX) / w,
                (p.y - minY) / h
            )).ToList();

            Vector2 centroid = new Vector2(scaled.Average(p => p.x), scaled.Average(p => p.y));
            return scaled.Select(p => p - centroid).ToList();
        }

        // Greedy cloud matching: approximates the minimum-cost pairing between
        // two point clouds without the full combinatorial cost of an exact
        // solution. Tries several rotational starting offsets and keeps the best.
        private static float GreedyCloudMatch(List<Vector2> points1, List<Vector2> points2)
        {
            if (points1.Count != points2.Count)
            {
                // Should never happen after the FlattenStrokes fix, but fail safely
                // rather than index out of range if it ever does (e.g. a
                // hand-edited or corrupted sample file).
                Debug.LogWarning($"Point cloud size mismatch ({points1.Count} vs {points2.Count}) - skipping this comparison.");
                return float.MaxValue;
            }

            int n = points1.Count;
            float eps = 0.5f;
            int step = Mathf.FloorToInt(Mathf.Pow(n, 1f - eps));
            float min = float.MaxValue;

            for (int i = 0; i < n; i += Mathf.Max(1, step))
            {
                float d1 = CloudDistance(points1, points2, i);
                float d2 = CloudDistance(points2, points1, i);
                min = Mathf.Min(min, Mathf.Min(d1, d2));
            }
            return min;
        }

        private static float CloudDistance(List<Vector2> points1, List<Vector2> points2, int start)
        {
            int n = points1.Count;
            var matched = new bool[n];
            float sum = 0f;
            int i = start;

            do
            {
                float minDist = float.MaxValue;
                int index = -1;
                for (int j = 0; j < n; j++)
                {
                    if (matched[j]) continue;
                    float d = Vector2.Distance(points1[i], points2[j]);
                    if (d < minDist) { minDist = d; index = j; }
                }
                matched[index] = true;
                float weight = 1f - ((((i - start) + n) % n) / (float)n);
                sum += weight * minDist;
                i = (i + 1) % n;
            } while (i != start);

            return sum;
        }
    }
}