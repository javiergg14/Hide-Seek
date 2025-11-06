using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ZombieController : MonoBehaviour
{
    [Header("Referencias")]
    public Transform player;
    public Camera playerCamera;
    public BoxCollider patrolArea;

    [Header("Velocidades")]
    public float wanderSpeed = 1.5f;
    public float chaseSpeed = 3.2f;

    [Header("Patrulla")]
    public float wanderIntervalMin = 2.0f;
    public float wanderIntervalMax = 4.0f;

    [Header("Persecución")]
    public float maxChaseDistance = 40f;

    [Header("Comunicación")]
    public float alertRadius = 15f;
    public float alertCooldown = 2.0f;
    public LayerMask zombieLayer;
    public string zombieTag = "Zombie";

    [Header("Smell / Olor")]
    public float smellMemorySeconds = 5f; // cuánto recuerda el último olor

    [Header("Animación (opcional)")]
    public Animator animator;

    // --- internos ---
    private NavMeshAgent agent;
    private float nextWanderTime;
    private bool isChasing; // estado de persecución por visión
    private float nextAlertTime;

    // memoria del olor (posición + caducidad)
    private bool hasSmellTarget;
    private Vector3 smellTarget;
    private float smellExpireTime;

    private Renderer[] renderers;
    private Collider[] colliders;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();

        agent.acceleration = 4f;
        agent.angularSpeed = 200f;
        agent.stoppingDistance = 0.2f;
    }

    void OnEnable()
    {
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            transform.position = hit.position;

        if (CompareTag(zombieTag) == false && !string.IsNullOrEmpty(zombieTag))
            gameObject.tag = zombieTag;
    }

    void Update()
    {
        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        if (!player) return;

        // caducar olor
        if (hasSmellTarget && Time.time >= smellExpireTime)
            hasSmellTarget = false;

        bool inPlayerView = IsInPlayerFrustum();
        float distToPlayer = Vector3.Distance(transform.position, player.position);

        if (inPlayerView)
        {
            // 1) PRIORIDAD VISIÓN: perseguir siempre
            if (!isChasing)
            {
                isChasing = true;
                agent.speed = chaseSpeed;
                if (animator) animator.SetBool("Alert", true);
                TryAlertNearbyZombies();
            }
            agent.SetDestination(player.position);
        }
        else if (hasSmellTarget)
        {
            isChasing = false;
            if (animator) animator.SetBool("Alert", false);

            agent.speed = Mathf.Max(agent.speed, wanderSpeed);
            agent.SetDestination(smellTarget);

            // si llegó al olor y no ve al jugador, limpia el objetivo
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f)
                hasSmellTarget = false;
        }
        else
        {
            // 3) Patrulla por área
            if (isChasing && distToPlayer > maxChaseDistance)
            {
                isChasing = false;
                if (animator) animator.SetBool("Alert", false);
                ScheduleNextWanderSoon();
            }

            agent.speed = wanderSpeed;
            HandleWanderInArea();
        }

        if (animator) animator.SetFloat("Speed", agent.velocity.magnitude);

        if (agent.velocity.sqrMagnitude > 0.05f)
        {
            Quaternion look = Quaternion.LookRotation(agent.velocity.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 5f);
        }
    }

    // ==== Percepción por frustum de la cámara del jugador ====
    bool IsInPlayerFrustum()
    {
        Camera cam = playerCamera ? playerCamera : Camera.main;
        if (!cam) return false;

        var planes = GeometryUtility.CalculateFrustumPlanes(cam);
        Bounds b = CalculateCombinedBounds();
        if (b.size == Vector3.zero) return false;

        return GeometryUtility.TestPlanesAABB(planes, b);
    }

    Bounds CalculateCombinedBounds()
    {
        bool hasAny = false;
        Bounds bounds = new Bounds(transform.position, Vector3.zero);

        foreach (var r in renderers)
        {
            if (!r || !r.enabled) continue;
            if (!hasAny) { bounds = r.bounds; hasAny = true; }
            else bounds.Encapsulate(r.bounds);
        }

        if (!hasAny)
        {
            foreach (var c in colliders)
            {
                if (!c || !c.enabled) continue;
                if (!hasAny) { bounds = c.bounds; hasAny = true; }
                else bounds.Encapsulate(c.bounds);
            }
        }
        return hasAny ? bounds : new Bounds(transform.position, Vector3.zero);
    }

    // ==== Patrulla en área ====
    void HandleWanderInArea()
    {
        if (!patrolArea)
        {
            if (Time.time >= nextWanderTime || ReachedDestination())
            {
                if (RandomPointNear(transform.position, 8f, out Vector3 dest))
                    agent.SetDestination(dest);
                ScheduleNextWander();
            }
            return;
        }

        if (Time.time >= nextWanderTime || ReachedDestination() || !IsDestinationInsideArea(agent.destination))
        {
            if (RandomPointInAreaOnNavMesh(patrolArea, out Vector3 dest))
                agent.SetDestination(dest);
            ScheduleNextWander();
        }
    }

    bool IsDestinationInsideArea(Vector3 pos)
    {
        var t = patrolArea.transform;
        Vector3 local = t.InverseTransformPoint(pos) - patrolArea.center;
        Vector3 half = patrolArea.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

    public static bool RandomPointInAreaOnNavMesh(BoxCollider area, out Vector3 result, int maxTries = 20)
    {
        for (int i = 0; i < maxTries; i++)
        {
            Vector3 rnd = RandomPointInsideBoxWorld(area);
            if (NavMesh.SamplePosition(rnd, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        result = area.transform.position;
        return false;
    }

    public static Vector3 RandomPointInsideBoxWorld(BoxCollider box)
    {
        Vector3 half = box.size * 0.5f;
        Vector3 local = new Vector3(
            Random.Range(-half.x, half.x),
            Random.Range(-half.y, half.y),
            Random.Range(-half.z, half.z)
        );
        return box.transform.TransformPoint(box.center + local);
    }

    public static bool RandomPointNear(Vector3 center, float radius, out Vector3 result)
    {
        for (int i = 0; i < 15; i++)
        {
            Vector3 random = center + Random.insideUnitSphere * radius;
            if (NavMesh.SamplePosition(random, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        result = center;
        return false;
    }

    void ScheduleNextWander() => nextWanderTime = Time.time + Random.Range(wanderIntervalMin, wanderIntervalMax);
    void ScheduleNextWanderSoon() => nextWanderTime = Time.time + Random.Range(0.2f, 0.6f);
    bool ReachedDestination() { if (agent.pathPending) return false; return agent.remainingDistance <= agent.stoppingDistance + 0.05f; }

    // ==== Comunicación ====
    void TryAlertNearbyZombies()
    {
        if (Time.time < nextAlertTime) return;
        nextAlertTime = Time.time + alertCooldown;

        Collider[] hits = (zombieLayer.value != 0)
            ? Physics.OverlapSphere(transform.position, alertRadius, zombieLayer)
            : Physics.OverlapSphere(transform.position, alertRadius);

        foreach (var h in hits)
        {
            if (!h) continue;
            var go = h.attachedRigidbody ? h.attachedRigidbody.gameObject : h.gameObject;
            if (go == gameObject) continue;
            if (!string.IsNullOrEmpty(zombieTag) && !go.CompareTag(zombieTag)) continue;

            go.SendMessage("OnAllySpottedPlayer", player.position, SendMessageOptions.DontRequireReceiver);
        }
        BroadcastMessage("OnPlayerSpottedLocal", player.position, SendMessageOptions.DontRequireReceiver);
    }

    void OnAllySpottedPlayer(Vector3 lastKnownPlayerPos)
    {
        // No rompe la prioridad: si luego entra al frustum, Update lo forzará a perseguir por visión
        isChasing = true;
        agent.speed = chaseSpeed;
        if (animator) animator.SetBool("Alert", true);

        if (player) agent.SetDestination(player.position);
        else agent.SetDestination(lastKnownPlayerPos);
    }

    // ==== Integración con SmellSensor ====
    // Llamado por SmellSensor cuando detecta una gota
    public void OnSmellDetected(Vector3 smellWorldPos)
    {
        hasSmellTarget = true;
        smellTarget = smellWorldPos;
        smellExpireTime = Time.time + smellMemorySeconds;
        // No forzamos aquí SetDestination si está viendo al jugador;
        // Update resolverá la prioridad (visión > olor).
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, alertRadius);
        if (patrolArea)
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.2f);
            Gizmos.matrix = patrolArea.transform.localToWorldMatrix;
            Gizmos.DrawCube(patrolArea.center, patrolArea.size);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 1f);
            Gizmos.DrawWireCube(patrolArea.center, patrolArea.size);
        }

        if (hasSmellTarget)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(smellTarget + Vector3.up * 0.05f, 0.15f);
        }
    }
}
