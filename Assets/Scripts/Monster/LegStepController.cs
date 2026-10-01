using UnityEngine;
using System.Collections;

/// <summary>
/// Controls one leg's full lifecycle: deciding when to step (based on drift
/// between current and desired foot position), picking crawl vs grapple,
/// and managing two separate visual rigs -
///   - spiderRigRoot: the jointed leg hierarchy (drives LegIKChain)
///   - tentacleRigRoot: the single-piece tentacle (drives TentacleStretch)
///
/// Only one rig is "emerged" (visible, scaled up) at a time. If a step uses
/// the same style as the last one, the leg stays emerged and just moves -
/// it only retracts into the body and swaps rigs when the NEXT step needs
/// the other style.
/// </summary>
public class LegStepController : MonoBehaviour
{
    public enum StepStyle { Grapple, Crawl }

    [Header("References")]
    public Transform footTarget;          // shared target both rigs' scripts read from
    public Transform legAnchor;           // fixed point on the body this leg searches outward from
    public Transform bodyCenter;          // the monster's core/center - outward direction is computed FROM this
    public LayerMask surfaceMask;

    [Header("Rigs")]
    [Tooltip("Parent object wrapping the jointed spider-leg hierarchy. Scaled 0->1 to emerge/retract.")]
    public Transform spiderRigRoot;
    [Tooltip("Parent object wrapping the single-piece tentacle mesh. Scaled 0->1 to emerge/retract.")]
    public Transform tentacleRigRoot;
    [Tooltip("Which style this leg starts already emerged as. Make sure the matching rig root starts " +
             "at scale 1 and active in the scene, and the other starts at scale 0 and inactive.")]
    public StepStyle startingStyle = StepStyle.Crawl;

    [Header("Emerge / retract")]
    public float emergeDuration = 0.25f;
    public float retractDuration = 0.2f;

    [Header("Step trigger")]
    public float stepThreshold = 0.6f;
    public float searchRadius = 1.5f;
    public int searchRays = 12;
    [Tooltip("Max angle (degrees) a candidate foothold ray is allowed to stray from the computed outward direction. " +
             "Keeps footholds from crossing to the other side of the body - lower = tighter/safer, " +
             "higher = more natural spread but more risk of reaching under/across the body.")]
    public float searchConeAngle = 50f;
    [Tooltip("How much to tilt the outward direction downward, so legs prefer footholds toward the ground " +
             "rather than searching straight out at body height. 0 = pure outward, 1 = mostly downward.")]
    [Range(0f, 1f)]
    public float groundBias = 0.4f;
    public float desiredRecalcInterval = 0.05f;

    [Header("Grapple (tentacle) style")]
    public float grappleSpeed = 14f;
    public float grappleHoldTime = 0.6f;

    [Header("Crawl (spider) style")]
    public float crawlSpeed = 4f;
    public float crawlArcHeight = 0.25f;
    public float crawlHoldTime = 0.9f;

    [Header("Randomization")]
    [Range(0f, 1f)]
    public float grappleChance = 0.5f;

    bool isStepping = false;
    StepStyle currentStyle;
    Vector3 currentFootPosition;
    Vector3 desiredFootPosition;
    float recalcTimer = 0f;

    public bool IsStepping => isStepping;

    void Start()
    {
        currentStyle = startingStyle;
        currentFootPosition = footTarget.position;
        desiredFootPosition = FindFoothold();

        // Make sure the scene matches what this script thinks is emerged.
        // (You still need to set the correct starting scale/active state on
        // the rig objects yourself - this just keeps them in sync going forward.)
        SetRigState(spiderRigRoot, currentStyle == StepStyle.Crawl ? 1f : 0f);
        SetRigState(tentacleRigRoot, currentStyle == StepStyle.Grapple ? 1f : 0f);
    }

    void Update()
    {
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
            StartCoroutine(DoLegCycle(desiredFootPosition));
        }
    }

    Transform GetRig(StepStyle style) => style == StepStyle.Crawl ? spiderRigRoot : tentacleRigRoot;

    void SetRigState(Transform rig, float scale)
    {
        if (rig == null) return;
        rig.gameObject.SetActive(scale > 0f);
        rig.localScale = Vector3.one * scale;
    }

    IEnumerator DoLegCycle(Vector3 destination)
    {
        isStepping = true;

        Transform activeRig = GetRig(currentStyle);

        // If the current rig isn't already emerged (e.g. right after a
        // previous style swap), emerge it first before moving.
        if (activeRig != null && activeRig.localScale.x < 0.99f)
        {
            yield return StartCoroutine(ScaleRig(activeRig, 0f, 1f, emergeDuration));
        }

        // Perform the actual foot movement - same as before.
        if (currentStyle == StepStyle.Grapple)
            yield return StartCoroutine(DoGrappleStep(destination));
        else
            yield return StartCoroutine(DoCrawlStep(destination));

        // Decide the NEXT step's style now, so we know whether to retract.
        StepStyle nextStyle = (Random.value < grappleChance) ? StepStyle.Grapple : StepStyle.Crawl;

        if (nextStyle != currentStyle)
        {
            // Retract current rig, swap which rig is active.
            yield return StartCoroutine(ScaleRig(activeRig, 1f, 0f, retractDuration));
            currentStyle = nextStyle;

            Transform newRig = GetRig(currentStyle);

            // Emerge the new rig immediately, then take a catch-up step to a
            // FRESHLY computed target rather than leaving the foot stationary
            // until the next Update() drift check fires - the retract+emerge
            // time alone lets the body drift further away, so waiting an
            // extra cycle on top of that is what was causing the lag.
            if (newRig != null)
            {
                yield return StartCoroutine(ScaleRig(newRig, 0f, 1f, emergeDuration));

                Vector3 catchUpTarget = FindFoothold();

                if (currentStyle == StepStyle.Grapple)
                    yield return StartCoroutine(DoGrappleStep(catchUpTarget));
                else
                    yield return StartCoroutine(DoCrawlStep(catchUpTarget));
            }
        }
        // else: leave the current rig emerged, nothing to swap.

        isStepping = false;
    }

    IEnumerator ScaleRig(Transform rig, float from, float to, float duration)
    {
        if (rig == null) yield break;

        if (to > 0f) rig.gameObject.SetActive(true);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(duration, 0.0001f);
            float eased = Mathf.SmoothStep(from, to, Mathf.Clamp01(t));
            rig.localScale = Vector3.one * eased;
            yield return null;
        }

        rig.localScale = Vector3.one * to;
        if (to <= 0f) rig.gameObject.SetActive(false);
    }

    Vector3 FindFoothold()
    {
        Vector3 origin = legAnchor.position;
        Vector3 best = currentFootPosition;
        float bestScore = float.MinValue;
        bool foundAny = false;

        for (int i = 0; i < searchRays; i++)
        {
            Vector3 outwardWorld = GetOutwardDirection();
            Vector3 dir = RandomDirectionInCone(outwardWorld, searchConeAngle);

            if (Physics.Raycast(origin, dir, out RaycastHit hit, searchRadius, surfaceMask))
            {
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

        return foundAny ? best : desiredFootPosition;
    }

    // Computes the true outward direction for this leg based on body geometry
    // (anchor position relative to body center) rather than the anchor's own
    // rotation - robust even if anchors were only positioned, never individually
    // rotated to face outward.
    Vector3 GetOutwardDirection()
    {
        if (bodyCenter == null)
        {
            // Fallback if bodyCenter wasn't assigned - not as reliable, but
            // better than nothing.
            return legAnchor.forward;
        }

        Vector3 outward = (legAnchor.position - bodyCenter.position).normalized;
        Vector3 blended = Vector3.Lerp(outward, Vector3.down, groundBias).normalized;
        return blended;
    }

    // Returns a random direction within maxAngleDegrees of "forward" - used to
    // keep foothold search rays from ever pointing back across the body.
    Vector3 RandomDirectionInCone(Vector3 forward, float maxAngleDegrees)
    {
        forward.Normalize();

        // Build an arbitrary perpendicular basis around "forward".
        Vector3 perpendicular = Vector3.Cross(forward, Vector3.up);
        if (perpendicular.sqrMagnitude < 0.0001f)
            perpendicular = Vector3.Cross(forward, Vector3.right);
        perpendicular.Normalize();

        float tiltAngle = Random.Range(0f, maxAngleDegrees);
        float spinAngle = Random.Range(0f, 360f);

        Quaternion tilt = Quaternion.AngleAxis(tiltAngle, perpendicular);
        Quaternion spin = Quaternion.AngleAxis(spinAngle, forward);

        return spin * tilt * forward;
    }

    IEnumerator DoGrappleStep(Vector3 destination)
    {
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
        yield return StartCoroutine(HoldUntilDriftOrTimeout(grappleHoldTime));
    }

    IEnumerator DoCrawlStep(Vector3 destination)
    {
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
        yield return StartCoroutine(HoldUntilDriftOrTimeout(crawlHoldTime));
    }

    // Waits up to holdTime, but bails out early the moment drift between the
    // foot's current and desired position builds back past stepThreshold -
    // otherwise a leg sits through its FULL hold time even while the body
    // keeps moving away underneath it, and steadily falls behind.
    IEnumerator HoldUntilDriftOrTimeout(float holdTime)
    {
        float elapsed = 0f;
        while (elapsed < holdTime)
        {
            elapsed += Time.deltaTime;

            float drift = Vector3.Distance(currentFootPosition, desiredFootPosition);
            if (drift > stepThreshold)
                yield break; // don't wait out the rest of the hold - step again now

            yield return null;
        }
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