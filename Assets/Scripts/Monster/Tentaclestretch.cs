using UnityEngine;

/// <summary>
/// Drives a single, jointless tentacle mesh: instead of bending through
/// joints, it rotates to face the foot target and stretches its length
/// (scale along one local axis) to close the distance. Put this on the
/// tentacle mesh itself, inside the tentacle rig root that LegStepController
/// scales up/down for emerge/retract.
/// </summary>
public class TentacleStretch : MonoBehaviour
{
    [Header("References")]
    public Transform target;              // the shared footTarget

    [Header("Model setup")]
    [Tooltip("The tentacle's resting (unstretched) length in world units, measured along its forward axis.")]
    public float restLength = 1f;
    [Tooltip("Which local axis of the mesh points along its length. Almost always Forward for a modeled-straight tentacle.")]
    public Vector3 lengthAxisLocal = Vector3.forward;

    [Header("Feel")]
    [Tooltip("How quickly the tentacle re-orients toward the target. Higher = snappier.")]
    public float aimSpeed = 20f;
    [Tooltip("Extra scale added briefly at full extension for a whip-snap look. 0 disables.")]
    public float overstretchAmount = 0.08f;

    Quaternion desiredRotation;
    Vector3 baseScale;      // the authored scale as placed in the editor, remembered at Start
    float baseAxisScale;    // whatever baseScale was along lengthAxisLocal specifically

    void Start()
    {
        baseScale = transform.localScale;
        baseAxisScale = GetAxisScale(baseScale, lengthAxisLocal);
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 toTarget = target.position - transform.position;
        float distance = toTarget.magnitude;
        if (distance < 0.001f) return;

        // Rotate lengthAxisLocal to point at the target.
        Quaternion lengthAxisCorrection = Quaternion.FromToRotation(Vector3.forward, lengthAxisLocal);
        desiredRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up) * Quaternion.Inverse(lengthAxisCorrection);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * aimSpeed);

        // Stretch along the length axis relative to the AUTHORED scale, not
        // a raw overwrite - so however you originally sized the mesh in the
        // editor is preserved instead of being replaced by distance/restLength.
        float stretchFactor = Mathf.Max(distance / Mathf.Max(restLength, 0.0001f), 0.0001f);
        Vector3 scale = baseScale;
        SetAxisScale(ref scale, lengthAxisLocal, baseAxisScale * stretchFactor);
        transform.localScale = scale;
    }

    float GetAxisScale(Vector3 scale, Vector3 axis)
    {
        if (Mathf.Abs(axis.z) > 0.5f) return scale.z;
        if (Mathf.Abs(axis.y) > 0.5f) return scale.y;
        return scale.x;
    }

    void SetAxisScale(ref Vector3 scale, Vector3 axis, float value)
    {
        // axis is expected to be one of Vector3.forward/up/right (or their negatives).
        if (Mathf.Abs(axis.z) > 0.5f) scale.z = value;
        else if (Mathf.Abs(axis.y) > 0.5f) scale.y = value;
        else scale.x = value;
    }
}