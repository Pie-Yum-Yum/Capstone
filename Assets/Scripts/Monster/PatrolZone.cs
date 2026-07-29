using System.Collections.Generic;
using UnityEngine;

// Attach to an empty GameObject placed in the level to mark an area the monster can patrol.
// Drop several of these around your map — the monster will pick between them.
//
// Zones also track distance to the player: if the player wanders far enough away, the zone
// removes itself from the pickable pool (Active) so the monster stops considering it, and
// re-adds itself once the player comes back within range.
public class PatrolZone : MonoBehaviour
{
    [Tooltip("Radius around this object's position that counts as part of the zone.")]
    public float radius = 10f;

    [Header("Player-based activation")]
    [Tooltip("If the player moves farther than this from the zone, it removes itself from the pool of zones the monster can pick.")]
    public float deactivationDistance = 40f;
    [Tooltip("Once deactivated, the player must come back within this distance to re-add the zone. " +
             "Keep this smaller than Deactivation Distance so it doesn't flicker on/off at the boundary.")]
    public float reactivationDistance = 30f;

    [Tooltip("Reference to the player. If left empty, it's auto-found via the 'Player' tag on Start.")]
    public Transform player;

    [Tooltip("How often (in seconds) this zone checks its distance to the player. Doesn't need to be every frame.")]
    public float checkInterval = 1f;
    private float checkTimer;

    // All patrol zones that currently exist in the scene.
    public static readonly List<PatrolZone> All = new List<PatrolZone>();
    // Subset of All that are currently within range of the player — this is what MonsterAI picks from.
    public static readonly List<PatrolZone> Active = new List<PatrolZone>();

    public bool IsActive { get; private set; } = true;

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
        if (IsActive && !Active.Contains(this)) Active.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
        Active.Remove(this);
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }

        checkTimer = Random.Range(0f, checkInterval);
    }

    private void Update()
    {
        if (player == null) return;

        checkTimer -= Time.deltaTime;
        if (checkTimer > 0f) return;
        checkTimer = checkInterval;

        float distance = Vector3.Distance(transform.position, player.position);

        if (IsActive && distance > deactivationDistance)
        {
            IsActive = false;
            Active.Remove(this);
        }
        else if (!IsActive && distance <= reactivationDistance)
        {
            IsActive = true;
            if (!Active.Contains(this)) Active.Add(this);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = IsActive ? new Color(0f, 1f, 0f, 0.3f) : new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}