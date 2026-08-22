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

    void LateUpdate()
    {
        if (joints == null || joints.Length < 2 || footTarget == null) return;
        SolveCCD();
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
