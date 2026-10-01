using UnityEngine;

/// <summary>
/// Simple CCD (Cyclic Coordinate Descent) IK solver for a short chain of joints
/// (e.g. hip -> knee -> foot). Attach this to the ROOT joint of a leg.
/// Assign "joints" in order from root to tip, and "footTarget" to the point
/// the tip should reach (this is what LegStepController moves around).
/// </summary>
public class LegIKChain : MonoBehaviour
{
    [Header("Chain setup (root to tip, in order)")]
    public Transform[] joints;      // e.g. [hipJoint, kneeJoint, footTipJoint]
    public Transform footTarget;    // empty GameObject that marks where the foot should be

    [Header("Solver settings")]
    [Tooltip("How many passes per frame. 3-10 is usually enough for a 2-3 joint leg.")]
    public int iterations = 8;
    [Tooltip("Stop early once the tip is within this distance of the target.")]
    public float tolerance = 0.01f;

    [Header("Pole (bend) direction")]
    [Tooltip("Optional. Place an empty above/outward from the elbow and assign it here for the most " +
             "control over which way the leg bends. If left empty, poleDirectionWorld is used instead.")]
    public Transform poleTarget;
    [Tooltip("Used only if poleTarget is empty. World-space direction the elbow should bend toward - " +
             "defaults to straight up, which stops legs from collapsing under the body.")]
    public Vector3 poleDirectionWorld = Vector3.up;

    void LateUpdate()
    {
        if (joints == null || joints.Length < 2 || footTarget == null) return;
        SolveCCD();
        ApplyPoleBend();
    }

    void SolveCCD()
    {
        Transform tip = joints[joints.Length - 1];

        for (int pass = 0; pass < iterations; pass++)
        {
            if (Vector3.Distance(tip.position, footTarget.position) < tolerance)
                break;

            // Walk from the joint closest to the tip back to the root.
            for (int i = joints.Length - 2; i >= 0; i--)
            {
                Transform joint = joints[i];

                Vector3 toTip = tip.position - joint.position;
                Vector3 toTarget = footTarget.position - joint.position;

                if (toTip.sqrMagnitude < 0.0001f || toTarget.sqrMagnitude < 0.0001f)
                    continue;

                // Rotate this joint so the tip swings toward the target.
                Quaternion rotation = Quaternion.FromToRotation(toTip, toTarget);
                joint.rotation = rotation * joint.rotation;
            }
        }
    }

    /// <summary>
    /// CCD alone has no preference for WHICH direction the elbow bends toward
    /// while reaching the target - it'll happily let the elbow swing under the
    /// body if that's the path it finds. This corrects that by twisting the
    /// whole chain around the root-to-tip axis so the middle joint (elbow)
    /// lines up with the pole direction, without moving the foot target.
    /// </summary>
    void ApplyPoleBend()
    {
        if (joints.Length < 3) return; // need a root, a middle joint, and a tip at minimum

        Transform root = joints[0];
        Transform mid = joints[joints.Length / 2];
        Transform tip = joints[joints.Length - 1];

        Vector3 axis = (tip.position - root.position);
        if (axis.sqrMagnitude < 0.0001f) return;
        axis.Normalize();

        Vector3 poleDir = (poleTarget != null)
            ? (poleTarget.position - root.position)
            : poleDirectionWorld;

        Vector3 currentBend = Vector3.ProjectOnPlane(mid.position - root.position, axis);
        Vector3 desiredBend = Vector3.ProjectOnPlane(poleDir, axis);

        if (currentBend.sqrMagnitude < 0.0001f || desiredBend.sqrMagnitude < 0.0001f) return;

        float angle = Vector3.SignedAngle(currentBend, desiredBend, axis);
        root.rotation = Quaternion.AngleAxis(angle, axis) * root.rotation;
    }

    // Draw the chain in the editor so you can see it while placing legs.
    void OnDrawGizmosSelected()
    {
        if (joints == null) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < joints.Length - 1; i++)
        {
            if (joints[i] != null && joints[i + 1] != null)
                Gizmos.DrawLine(joints[i].position, joints[i + 1].position);
        }
        if (footTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(footTarget.position, 0.05f);
        }
    }
}