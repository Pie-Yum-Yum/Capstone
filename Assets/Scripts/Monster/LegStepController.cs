using UnityEngine;
using System.Collections;

/// <summary>
/// Controls where ONE leg's foot target moves to, and how it gets there.
///
/// Unlike a timed gait, this checks every frame how far the foot's ACTUAL
/// current position has drifted from its DESIRED position (a live raycast
/// result toward the ground/nearby surface). Once that drift exceeds
/// stepThreshold, the leg takes a step to catch back up - same idea as
/// classic spider-leg IK rigs (see AimAtPoint reference).
///
/// Each step, it randomly picks one of two movement styles:
///   - Grapple (tentacle): fast whip-out that snaps into place.
///   - Crawl (spider): slower arced step, like lifting and placing a leg.
/// </summary>
public class LegStepController : MonoBehaviour
{
    public enum StepStyle { Grapple, Crawl }

    [Header("References")]
    public Transform footTarget;          // the target LegIKChain follows (actual placed foot)
    public Transform legRoot;             // anchor point this leg searches outward from
    public LayerMask surfaceMask;         // what counts as a valid foothold (ground, walls, props)

    [Header("Step trigger")]
    [Tooltip("How far the desired foothold has to drift from the current foot position before a step is triggered.")]
    public float stepThreshold = 0.6f;
    [Tooltip("How far from the leg root this leg is allowed to search for a foothold.")]
    public float searchRadius = 1.5f;
    [Tooltip("Rays cast per search - more rays = more accurate foothold picking.")]
    public int searchRays = 12;
    [Tooltip("How often (seconds) to recompute the desired foothold. 0 = every frame.")]
    public float desiredRecalcInterval = 0.05f;

    [Header("Grapple (tentacle) style")]
    public float grappleSpeed = 14f;      // fast whip-out
    public float grappleHoldTime = 0.6f;

    [Header("Crawl (spider) style")]
    public float crawlSpeed = 4f;         // slower, deliberate
    public float crawlArcHeight = 0.25f;  // how much the foot lifts mid-step
    public float crawlHoldTime = 0.9f;

    [Header("Randomization")]
    [Range(0f, 1f)]
    public float grappleChance = 0.5f;    // 0 = always crawl, 1 = always grapple

    bool isStepping = false;
    Vector3 currentFootPosition;   // where the foot actually is right now (last placed spot)
    Vector3 desiredFootPosition;   // where the foot "wants" to be, recomputed periodically
    float recalcTimer = 0f;

    public bool IsStepping => isStepping;

    void Start()
    {
        // Initialize both to wherever the foot target currently sits, so it
        // doesn't immediately think it needs to step on frame one.
        currentFootPosition = footTarget.position;
        desiredFootPosition = FindFoothold();
    }

    void Update()
    {
        // Recompute the desired foothold on an interval rather than every
        // single frame - raycasting every frame per leg adds up with several legs.
        recalcTimer -= Time.deltaTime;
        if (recalcTimer <= 0f)
        {
            desiredFootPosition = FindFoothold();
            recalcTimer = desiredRecalcInterval;
        }

        if (isStepping) return;

        float drift = Vector3.Distance(currentFootPosition, desiredFootPosition);
        if (drift > stepThreshold)
        {
            StepStyle style = (Random.value < grappleChance) ? StepStyle.Grapple : StepStyle.Crawl;
            StartCoroutine(style == StepStyle.Grapple
                ? DoGrappleStep(desiredFootPosition)
                : DoCrawlStep(desiredFootPosition));
        }
    }

    Vector3 FindFoothold()
    {
        Vector3 origin = legRoot.position;
        Vector3 best = currentFootPosition; // fall back to current spot if nothing better found
        float bestScore = float.MinValue;
        bool foundAny = false;

        for (int i = 0; i < searchRays; i++)
        {
            // Spread rays roughly forward/downward in a cone around the leg root,
            // biased outward from the body so legs don't all reach for the same spot.
            Vector3 randomDir = Random.onUnitSphere;
            randomDir.y = Mathf.Abs(randomDir.y) * -1f; // bias downward/outward
            Vector3 dir = (legRoot.forward + randomDir).normalized;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, searchRadius, surfaceMask))
            {
                // Prefer footholds close to the "ideal" resting distance from the root,
                // not the closest or farthest hit - keeps legs from overreaching.
                float idealDist = searchRadius * 0.6f;
                float score = -Mathf.Abs(hit.distance - idealDist);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = hit.point;
                    foundAny = true;
                }
            }
        }

        // If nothing was hit this pass (e.g. leg over empty space), keep the
        // last known desired position instead of snapping back to current foot.
        return foundAny ? best : desiredFootPosition;
    }

    IEnumerator DoGrappleStep(Vector3 destination)
    {
        isStepping = true;
        Vector3 start = footTarget.position;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * grappleSpeed;
            footTarget.position = Vector3.Lerp(start, destination, EaseOutBack(Mathf.Clamp01(t)));
            yield return null;
        }

        footTarget.position = destination;
        currentFootPosition = destination;
        yield return new WaitForSeconds(grappleHoldTime);
        isStepping = false;
    }

    IEnumerator DoCrawlStep(Vector3 destination)
    {
        isStepping = true;
        Vector3 start = footTarget.position;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * crawlSpeed;
            float clamped = Mathf.Clamp01(t);
            Vector3 flatPos = Vector3.Lerp(start, destination, clamped);
            float lift = Mathf.Sin(clamped * Mathf.PI) * crawlArcHeight;
            footTarget.position = flatPos + Vector3.up * lift;
            yield return null;
        }

        footTarget.position = destination;
        currentFootPosition = destination;
        yield return new WaitForSeconds(crawlHoldTime);
        isStepping = false;
    }

    float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(desiredFootPosition, 0.06f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(currentFootPosition, 0.06f);
    }
}