using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Coordinates every leg on the monster (across both body segments) so they
/// don't all lift off and reposition at the same time - that reads as
/// "walking," which isn't what you want here. Instead legs step roughly
/// in alternating groups, like a spider gait, while each individual leg
/// still randomly picks grapple vs crawl on its own (handled in LegStepController).
///
/// Put this on the root monster GameObject and drag every leg's
/// LegStepController into the "legs" list.
/// </summary>
public class MonsterLegManager : MonoBehaviour
{
    [Header("Legs (drag all LegStepController components here)")]
    public List<LegStepController> legs = new List<LegStepController>();

    [Header("Gait timing")]
    [Tooltip("Time between checking whether it's time for the next group of legs to step.")]
    public float stepCheckInterval = 0.15f;
    [Tooltip("How far a foothold has to have drifted (body moved) before a leg bothers re-stepping.")]
    public float restepDistance = 0.8f;

    [Tooltip("Number of alternating groups. 2 = classic tripod-style alternating gait. " +
             "Higher = more staggered/chaotic stepping, which can read as creepier.")]
    public int gaitGroups = 2;

    List<List<LegStepController>> groups;
    int currentGroup = 0;
    float timer = 0f;

    void Awake()
    {
        BuildGroups();
    }

    void BuildGroups()
    {
        groups = new List<List<LegStepController>>();
        for (int i = 0; i < gaitGroups; i++)
            groups.Add(new List<LegStepController>());

        // Distribute legs round-robin across groups. Since legs were added in the
        // order you dragged them in (e.g. segment1 legs then segment2 legs), this
        // naturally mixes legs from both body segments into each group.
        for (int i = 0; i < legs.Count; i++)
        {
            groups[i % gaitGroups].Add(legs[i]);
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer < stepCheckInterval) return;
        timer = 0f;

        StepCurrentGroupIfReady();
    }

    void StepCurrentGroupIfReady()
    {
        var group = groups[currentGroup];
        bool anyStillStepping = false;

        foreach (var leg in group)
        {
            if (leg.IsStepping)
            {
                anyStillStepping = true;
                break;
            }
        }

        // Wait for the whole group to finish its current step before advancing,
        // so legs in the same group move together (tripod-style groupings).
        if (anyStillStepping) return;

        foreach (var leg in group)
        {
            //leg.RequestStep();
        }

        currentGroup = (currentGroup + 1) % groups.Count;
    }

    /// <summary>
    /// Call this if you add/remove legs at runtime (e.g. a leg gets destroyed
    /// in-game) and need the gait groups rebuilt.
    /// </summary>
    public void RefreshGroups()
    {
        BuildGroups();
        currentGroup = 0;
    }
}
