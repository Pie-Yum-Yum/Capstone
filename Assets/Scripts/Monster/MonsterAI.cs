using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Core monster state machine. Requires a NavMeshAgent on the same GameObject and a baked NavMesh in the scene.
[RequireComponent(typeof(NavMeshAgent))]
public class MonsterAI : MonoBehaviour
{
    public enum State { Patrol, Investigate, Chase }

    [Header("References")]
    public Transform player;
    [Tooltip("Optional. Animator to drive with Speed (float), IsChasing (bool), IsSearching (bool), Attack (trigger).")]
    public Animator animator;

    public PlayerMovement playerMovement; // Reference to the PlayerMovement script

    [Header("State (read-only at runtime)")]
    [SerializeField] private State currentState = State.Patrol;
    public State CurrentState => currentState;

    [Header("Patrol")]
    [Tooltip("Min/max seconds before picking a new patrol zone.")]
    public Vector2 patrolZoneSwitchInterval = new Vector2(15f, 30f);
    [Tooltip("Min/max seconds spent waiting at a patrol point before moving to a new one within the zone.")]
    public Vector2 patrolPointWaitTime = new Vector2(2f, 6f);
    public float patrolSpeed = 2f;

    [Header("Investigate (Searching)")]
    [Tooltip("How long the monster searches an area before giving up and returning to patrol.")]
    public float investigateDuration = 8f;
    [Tooltip("Radius around the investigate target the monster wanders while searching.")]
    public float investigateWanderRadius = 6f;
    public float investigateSpeed = 3f;

    [Header("Chase")]
    public float chaseSpeed = 5f;
    [Tooltip("Seconds the monster keeps chasing toward the last known player position after losing detection, " +
             "before switching to Investigate at that spot (Alien Isolation style).")]
    public float chaseMemoryDuration = 3f;
    [Tooltip("Distance at which the monster is considered to have caught the player.")]
    public float catchDistance = 1.2f;

    [Header("Detection - Proximity (blind 'feel', omnidirectional)")]
    public float proximityRadius = 2.5f;

    [Header("Detection - Short Vision Cone")]
    public float visionRange = 6f;
    [Range(0f, 360f)] public float visionAngle = 60f;
    [Tooltip("Layers that block the monster's short-range vision (walls, etc). Leave empty to skip occlusion checks.")]
    public LayerMask visionObstructionMask;

    [Header("Detection - Hearing")]
    [Tooltip("Base hearing range added on top of a sound event's own radius.")]
    public float hearingRadius = 3f;

    [Header("Detection Tick Rate")]
    [Tooltip("How many times per second detection checks run.")]
    public float detectionChecksPerSecond = 10f;

    private NavMeshAgent agent;
    private float detectionTickTimer;

    // Patrol state data
    private PatrolZone currentZone;
    private float patrolZoneTimer;
    private float patrolWaitTimer;
    private bool waitingAtPatrolPoint;

    // Investigate state data
    private Vector3 investigateTarget;
    private float investigateTimer;

    // Unreachable-path handling (e.g. a point behind a locked door)
    [Header("Unreachable Path Handling")]
    [Tooltip("If a point can't be fully pathed to this many times in a row, the monster abandons the current " +
             "patrol zone entirely and picks a different one (instead of just picking a new point in the same zone).")]
    public int maxUnreachablePointRetries = 3;
    private int unreachablePointCount;

    // Chase state data
    private Vector3 lastKnownPlayerPosition;
    private float lastDetectionTime;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        EnterPatrol();
    }

    private void Update()
    {
        detectionTickTimer -= Time.deltaTime;
        if (detectionTickTimer <= 0f)
        {
            detectionTickTimer = 1f / Mathf.Max(detectionChecksPerSecond, 0.01f);
            RunDetection();
        }

        switch (currentState)
        {
            case State.Patrol: TickPatrol(); break;
            case State.Investigate: TickInvestigate(); break;
            case State.Chase: TickChase(); break;
        }

        UpdateAnimator();
    }

    // ---------------- Detection ----------------

    private void RunDetection()
    {
        if (player == null) return;

        bool playerDetected = CheckProximity() || CheckVisionCone() && !playerMovement.hidingSpot;

        if (playerDetected)
        {
            lastKnownPlayerPosition = player.position;
            lastDetectionTime = Time.time;
            if (currentState != State.Chase) EnterChase();
            return;
        }

        // Sound can pull the monster out of Patrol or Investigate, but Chase relies on chaseMemoryDuration instead.
        if (currentState != State.Chase)
        {
            if (SoundManager.Instance != null &&
                SoundManager.Instance.TryGetLoudestSoundInRange(transform.position, hearingRadius, out SoundEvent heardSound))
            {
                EnterInvestigate(heardSound.position);
            }
        }
    }

    private bool CheckProximity()
    {
        return Vector3.Distance(transform.position, player.position) <= proximityRadius;
    }

    private bool CheckVisionCone()
    {
        Vector3 toPlayer = player.position - transform.position;
        float distance = toPlayer.magnitude;
        if (distance > visionRange) return false;

        float angle = Vector3.Angle(transform.forward, toPlayer);
        if (angle > visionAngle * 0.5f) return false;

        // If an obstruction mask is set, block vision through walls etc.
        if (visionObstructionMask.value != 0 &&
            Physics.Raycast(transform.position, toPlayer.normalized, out RaycastHit hit, visionRange, visionObstructionMask))
        {
            return false;
        }

        return true;
    }

    // ---------------- Patrol ----------------

    public void ResetToPatrolState()
    {
        EnterPatrol();
    }

    private void EnterPatrol()
    {
        currentState = State.Patrol;
        agent.speed = patrolSpeed;
        PickNewPatrolZone();
        PickNewPatrolPointInZone();
        patrolZoneTimer = Random.Range(patrolZoneSwitchInterval.x, patrolZoneSwitchInterval.y);
    }

    private void TickPatrol()
    {
        if (HandleUnreachablePath(PickNewPatrolPointInZone)) return;

        patrolZoneTimer -= Time.deltaTime;
        if (patrolZoneTimer <= 0f)
        {
            PickNewPatrolZone();
            patrolZoneTimer = Random.Range(patrolZoneSwitchInterval.x, patrolZoneSwitchInterval.y);
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!waitingAtPatrolPoint)
            {
                waitingAtPatrolPoint = true;
                patrolWaitTimer = Random.Range(patrolPointWaitTime.x, patrolPointWaitTime.y);
            }
            else
            {
                patrolWaitTimer -= Time.deltaTime;
                if (patrolWaitTimer <= 0f)
                {
                    PickNewPatrolPointInZone();
                }
            }
        }
    }

    private void PickNewPatrolZone()
    {
        List<PatrolZone> pool = PatrolZone.Active.Count > 0 ? PatrolZone.Active : PatrolZone.All;
        if (pool.Count == 0) return;
        currentZone = pool[Random.Range(0, pool.Count)];
    }

    private void PickNewPatrolPointInZone()
    {
        if (currentZone == null)
        {
            PickNewPatrolZone();
            if (currentZone == null) return;
        }

        if (TryGetRandomPointInRadius(currentZone.transform.position, currentZone.radius, out Vector3 point))
        {
            agent.SetDestination(point);
        }
        waitingAtPatrolPoint = false;
    }

    // ---------------- Investigate ----------------

    private void EnterInvestigate(Vector3 target)
    {
        currentState = State.Investigate;
        agent.speed = investigateSpeed;
        investigateTarget = target;
        investigateTimer = investigateDuration;
        agent.SetDestination(investigateTarget);
    }

    private void TickInvestigate()
    {
        if (HandleUnreachablePath(() =>
            {
                if (TryGetRandomPointInRadius(investigateTarget, investigateWanderRadius, out Vector3 point))
                    agent.SetDestination(point);
            })) return;

        // A newer/closer sound redirects attention and resets the search timer.
        if (SoundManager.Instance != null &&
            SoundManager.Instance.TryGetLoudestSoundInRange(transform.position, hearingRadius, out SoundEvent heardSound))
        {
            investigateTarget = heardSound.position;
            investigateTimer = investigateDuration;
            agent.SetDestination(investigateTarget);
        }

        investigateTimer -= Time.deltaTime;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            // Wander around the investigate point while searching.
            if (TryGetRandomPointInRadius(investigateTarget, investigateWanderRadius, out Vector3 point))
            {
                agent.SetDestination(point);
            }
        }

        if (investigateTimer <= 0f)
        {
            EnterPatrol();
        }
    }

    // ---------------- Chase ----------------

    private void EnterChase()
    {
        currentState = State.Chase;
        agent.speed = chaseSpeed;
    }

    private void TickChase()
    {
        bool stillDetected = CheckProximity() || CheckVisionCone();
        if (stillDetected)
        {
            lastKnownPlayerPosition = player.position;
            lastDetectionTime = Time.time;
        }

        agent.SetDestination(lastKnownPlayerPosition);

        if (Vector3.Distance(transform.position, player.position) <= catchDistance)
        {
            OnPlayerCaught();
            return;
        }

        if (Time.time - lastDetectionTime > chaseMemoryDuration)
        {
            // Alien Isolation style: search near the last known spot before giving up entirely.
            EnterInvestigate(lastKnownPlayerPosition);
        }
    }

    protected virtual void OnPlayerCaught()
    {
        // Hook this up to your game-over / damage logic.
        // if (animator != null) animator.SetTrigger("Attack");
        Debug.Log("Monster caught the player.");

        if (GameSaveManager.Instance != null && GameSaveManager.Instance.TryLoadLatestSave(player))
        {
            return;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    // ---------------- Helpers ----------------

    // Checks whether the last SetDestination() actually resolved to a full path. If it's only
    // partial or fully invalid (e.g. the point is behind a locked door that carves the NavMesh),
    // this picks a new point via retryPoint(), and after enough consecutive failures abandons the
    // current patrol zone entirely in favor of a different one. Returns true if it took action this frame.
    private bool HandleUnreachablePath(System.Action retryPoint)
    {
        if (agent.pathPending) return false;
        if (agent.pathStatus == NavMeshPathStatus.PathComplete)
        {
            unreachablePointCount = 0;
            return false;
        }

        // PathPartial or PathInvalid: the target point can't actually be reached.
        unreachablePointCount++;
        if (unreachablePointCount >= maxUnreachablePointRetries)
        {
            unreachablePointCount = 0;
            PickNewPatrolZone();
        }

        retryPoint();
        return true;
    }

    private bool TryGetRandomPointInRadius(Vector3 center, float radius, out Vector3 result)
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 randomPoint = center + Random.insideUnitSphere * radius;
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, radius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        result = center;
        return false;
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetFloat("Speed", agent.velocity.magnitude);
        animator.SetBool("IsChasing", currentState == State.Chase);
        animator.SetBool("IsSearching", currentState == State.Investigate);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, proximityRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, hearingRadius);
    }
}