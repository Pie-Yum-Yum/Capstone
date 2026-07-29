using UnityEngine;

// Placeholder for the spider-style procedural leg IK system.
// Not implemented yet — this just reserves the slot so MonsterAI/animation work isn't blocked on it.
// When you're ready to build it: raycast down from each leg's rest position to find ground contact,
// then move legTargets toward those points with step-timing/gait logic (Unity's Animation Rigging
// package with Two Bone IK Constraints per leg is a solid fit here).
public class MonsterLegIK : MonoBehaviour
{
    [Tooltip("IK target transforms for each leg, to be assigned once the leg rig exists.")]
    public Transform[] legTargets;

    private void Update()
    {
        // TODO: implement leg placement/gait logic.
    }
}
