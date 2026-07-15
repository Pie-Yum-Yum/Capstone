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
        public float rawDistance; // unscaled greedy-cloud-match distance - useful for calibrating the score formula
    }

    /// <summary>
    /// Implementation of the $P Point-Cloud gesture recognizer, ported to
    /// match the reference C# implementation by Vatavu, Anthony & Wobbrock
    /// (the original "$P Point-Cloud Gesture Recognizer C# library").
    /// Reference: Vatavu, R.-D., Anthony, L. and Wobbrock, J.O. (2012).
    /// "Gestures as point clouds: A $P recognizer for user interface
    /// prototypes." Proceedings of ICMI '12.
    ///
    /// Ported (not copy-pasted) to work with Unity's Vector2 and this
    /// project's SpellSample/StrokeData data model, but the normalization
    /// pipeline and matching algorithm follow the reference exactly:
    /// scale (shape-preserving) -> translate to centroid -> resample last,
    /// as one continuous pass across all strokes using a per-point stroke
    /// tag so the resample doesn't bridge a fake line between strokes.
    /// </summary>
    public static class GestureRecognizer
    {
        // Number of points every gesture (candidate or sample) is resampled to.
        public const int ResamplePointCount = 32;

        // A point tagged with which stroke it belongs to. Used internally
        // during normalization so PathLength/Resample can skip the "gap"
        // between the end of one stroke and the start of the next, exactly
        // like the reference implementation's StrokeID field.
        private struct TaggedPoint
        {
            public Vector2 Pos;
            public int StrokeId;
            public TaggedPoint(Vector2 pos, int strokeId) { Pos = pos; StrokeId = strokeId; }
        }

        /// <summary>
        /// Call once per sample after loading (e.g. on game start) to
        /// precompute its normalized point cloud so recognition at cast-time
        /// only has to normalize the player's drawing, not every sample.
        /// </summary>
        public static void Prepare(SpellSample sample)
        {
            var rawStrokes = sample.strokes.Select(s => s.points).ToList();
            sample.NormalizedCloud = Normalize(rawStrokes);
        }

        /// <summary>Prepares every sample belonging to a spell.</summary>
        public static void Prepare(SpellDefinition spell)
        {
            foreach (var sample in spell.samples)
                if (sample.NormalizedCloud == null) Prepare(sample);
        }

        // Point-cloud matching alone treats a gesture as an unordered set of
        // points, which deliberately ignores stroke count/topology. That's
        // normally useful (it's forgiving about draw order), but it also
        // means two genuinely different shapes with similar "ink coverage"
        // in their bounding box (e.g. a spiral vs. a multi-line shape) can
        // end up geometrically closer than expected. Penalizing a stroke
        // count mismatch adds back a cheap, strong signal that pure
        // point-cloud distance throws away, and meaningfully widens the gap
        // between real matches and coincidentally-similar wrong shapes.
        private const float StrokeCountMismatchPenalty = 0.05f; // added to average per-point distance, per extra/missing stroke

        /// <summary>
        /// Recognize a freshly-drawn gesture (list of strokes, each a list of
        /// points in the same local space the samples were authored in)
        /// against every sample of every provided spell. The single
        /// best-scoring sample, from any spell, wins.
        /// </summary>
        public static RecognitionResult Recognize(List<List<Vector2>> drawnStrokes, IEnumerable<SpellDefinition> spells)
        {
            var candidate = Normalize(drawnStrokes);
            if (candidate.Count == 0)
                return new RecognitionResult { bestMatch = null, matchedSample = null, score = 0f, successful = false };

            int drawnStrokeCount = drawnStrokes?.Count(s => s != null && s.Count > 0) ?? 0;

            SpellDefinition best = null;
            SpellSample bestSample = null;
            float bestDistance = float.MaxValue;

            foreach (var spell in spells)
            {
                foreach (var sample in spell.samples)
                {
                    if (sample.NormalizedCloud == null) Prepare(sample);

                    float rawDistance = GreedyCloudMatch(candidate, sample.NormalizedCloud);

                    // Penalize stroke-count mismatches. Penalty is scaled by
                    // ResamplePointCount so it's expressed in the same
                    // "summed distance" units as rawDistance, since both get
                    // divided by ResamplePointCount together below.
                    int strokeDiff = Mathf.Abs(drawnStrokeCount - sample.strokes.Count);
                    float adjustedDistance = rawDistance + (strokeDiff * StrokeCountMismatchPenalty * ResamplePointCount);

                    if (adjustedDistance < bestDistance)
                    {
                        bestDistance = adjustedDistance;
                        best = spell;
                        bestSample = sample;
                    }
                }
            }

            // Convert raw greedy-cloud distance into a friendlier 0..1 score.
            // CloudDistance SUMS weighted distances across all ResamplePointCount
            // points rather than averaging them, so we divide by point count first
            // to get a per-point average distance - a number that stays meaningful
            // regardless of ResamplePointCount. maxExpectedAverageDistance is the
            // tunable "how far off is a total miss" constant: start here and
            // adjust based on the rawDistance/score values you see while
            // playtesting with your actual drawn shapes.
            float averageDistance = bestDistance / ResamplePointCount;
            const float maxExpectedAverageDistance = 0.15f;
            float score = Mathf.Clamp01(1f - (averageDistance / maxExpectedAverageDistance));

            return new RecognitionResult
            {
                bestMatch = best,
                matchedSample = bestSample,
                score = score,
                successful = best != null && score >= best.minAccuracyToCast,
                rawDistance = bestDistance
            };
        }

        // ---- Normalization pipeline (matches the reference implementation's order) ----

        // Scale (shape-preserving) -> translate to centroid -> resample LAST,
        // as one continuous pass over the raw high-density points. Resampling
        // last (rather than per-stroke first, before normalizing) preserves
        // much more shape detail and is what the reference implementation does.
        private static List<Vector2> Normalize(List<List<Vector2>> strokes)
        {
            var tagged = FlattenWithStrokeIds(strokes);
            if (tagged.Count == 0) return new List<Vector2>();

            tagged = ScaleShapePreserving(tagged);
            tagged = TranslateToCentroid(tagged);
            return ResampleTagged(tagged, ResamplePointCount);
        }

        private static List<TaggedPoint> FlattenWithStrokeIds(List<List<Vector2>> strokes)
        {
            var result = new List<TaggedPoint>();
            if (strokes == null) return result;

            for (int s = 0; s < strokes.Count; s++)
            {
                var stroke = strokes[s];
                if (stroke == null) continue;
                foreach (var p in stroke)
                    result.Add(new TaggedPoint(p, s));
            }
            return result;
        }

        // Scales both axes by the SAME factor (max of width/height), unlike a
        // naive fit-to-unit-box which would stretch x and y independently and
        // distort the shape. This is the reference implementation's approach.
        private static List<TaggedPoint> ScaleShapePreserving(List<TaggedPoint> points)
        {
            float minX = points.Min(p => p.Pos.x), maxX = points.Max(p => p.Pos.x);
            float minY = points.Min(p => p.Pos.y), maxY = points.Max(p => p.Pos.y);
            float scale = Mathf.Max(maxX - minX, maxY - minY);
            if (scale <= Mathf.Epsilon) scale = 1f; // degenerate: a single point or a dot

            return points.Select(p => new TaggedPoint(
                new Vector2((p.Pos.x - minX) / scale, (p.Pos.y - minY) / scale),
                p.StrokeId
            )).ToList();
        }

        private static List<TaggedPoint> TranslateToCentroid(List<TaggedPoint> points)
        {
            Vector2 centroid = new Vector2(points.Average(p => p.Pos.x), points.Average(p => p.Pos.y));
            return points.Select(p => new TaggedPoint(p.Pos - centroid, p.StrokeId)).ToList();
        }

        // Resamples into exactly n equally-spaced points across the WHOLE
        // gesture in one continuous pass, only accumulating distance between
        // consecutive points that share the same StrokeId - so the resample
        // never draws a phantom line bridging the gap between two strokes.
        private static List<Vector2> ResampleTagged(List<TaggedPoint> points, int n)
        {
            var result = new List<Vector2> { points[0].Pos };

            float pathLength = PathLengthTagged(points);
            if (pathLength <= Mathf.Epsilon)
            {
                // Degenerate: effectively a single point (e.g. a dot/click). Repeat it.
                while (result.Count < n) result.Add(points[0].Pos);
                return result;
            }

            float interval = pathLength / (n - 1);
            float D = 0f;

            for (int i = 1; i < points.Count && result.Count < n; i++)
            {
                if (points[i].StrokeId != points[i - 1].StrokeId) continue; // skip the gap between strokes

                float d = Vector2.Distance(points[i - 1].Pos, points[i].Pos);
                if (D + d >= interval)
                {
                    Vector2 firstPoint = points[i - 1].Pos;
                    while (D + d >= interval && result.Count < n)
                    {
                        float t = Mathf.Clamp01((interval - D) / d);
                        if (float.IsNaN(t)) t = 0.5f;
                        Vector2 q = Vector2.Lerp(firstPoint, points[i].Pos, t);
                        result.Add(q);

                        d = D + d - interval;
                        D = 0f;
                        firstPoint = q;
                    }
                    D = d;
                }
                else
                {
                    D += d;
                }
            }

            // Rounding can occasionally leave the result a point short or, in
            // rare multi-stroke edge cases, more than one short - pad with the
            // final point so the count is always guaranteed to be exactly n.
            while (result.Count < n) result.Add(points[points.Count - 1].Pos);
            if (result.Count > n) result.RemoveRange(n, result.Count - n);

            return result;
        }

        private static float PathLengthTagged(List<TaggedPoint> points)
        {
            float length = 0f;
            for (int i = 1; i < points.Count; i++)
                if (points[i].StrokeId == points[i - 1].StrokeId)
                    length += Vector2.Distance(points[i - 1].Pos, points[i].Pos);
            return length;
        }

        // ---- Matching (greedy cloud matching, same as reference) ----

        private static float GreedyCloudMatch(List<Vector2> points1, List<Vector2> points2)
        {
            if (points1.Count != points2.Count)
            {
                // Should never happen now that both clouds always resample to
                // exactly ResamplePointCount, but fail safely rather than
                // index out of range if it ever does (e.g. a hand-edited or
                // corrupted sample file).
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