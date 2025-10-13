using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class WanderAgent : MonoBehaviour
{
    [Header("Area (opcional)")]
    public GameObject suelo;
    public bool limitarA_BoundsDelSuelo = true;

    [Header("Spawn")]
    public bool spawnEnAreaAleatoria = true;
    public float spawnSampleRadius = 6f;

    [Header("Wander params")]
    public float wanderCircleDistance = 6.0f;   // antes 3.0
    public float wanderCircleRadius = 3.0f;   // antes 2.0
    public float wanderJitterPerSec = 1.5f;   // menos jitter

    [Header("Actualizacion de destino")]
    public float updateInterval = 0.35f;        // antes 0.1
    public float sampleRadius = 6f;

    private NavMeshAgent agent;
    private Vector3 wanderTarget;
    private float timer;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        EnsureOnNavMesh();

        if (spawnEnAreaAleatoria && suelo != null && TryGetRandomPointOnFloor(out Vector3 p))
            agent.Warp(p);

        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        wanderTarget = Random.onUnitSphere;
        wanderTarget.y = 0f;
        wanderTarget = wanderTarget.normalized * wanderCircleRadius;
        timer = updateInterval;
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            timer = 0f;

            // Variacion aleatoria en la direccion del wander
            Vector3 jitter = new Vector3(
                Random.Range(-1f, 1f),
                0f,
                Random.Range(-1f, 1f)
            ) * (wanderJitterPerSec * updateInterval);

            wanderTarget += jitter;
            wanderTarget = wanderTarget.normalized * wanderCircleRadius;

            Vector3 circleCenter = transform.position + transform.forward * wanderCircleDistance;
            Vector3 targetWorld = circleCenter + transform.TransformDirection(wanderTarget);

            if (limitarA_BoundsDelSuelo && suelo != null && suelo.TryGetComponent<Renderer>(out var rend))
            {
                Bounds b = rend.bounds;
                targetWorld.x = Mathf.Clamp(targetWorld.x, b.min.x + 1f, b.max.x - 1f);
                targetWorld.z = Mathf.Clamp(targetWorld.z, b.min.z + 1f, b.max.z - 1f);
            }

            if (NavMesh.SamplePosition(targetWorld, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            {
                // Solo actualiza si el nuevo destino esta razonablemente lejos
                if (!agent.hasPath || Vector3.Distance(agent.destination, hit.position) > 1.0f)
                    agent.SetDestination(hit.position);
            }
        }

        // Opcional: girar suavemente hacia la direccion de movimiento
        if (agent.velocity.sqrMagnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(agent.velocity.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 3f);
        }
    }

    // --- Utilidades ---

    void EnsureOnNavMesh()
    {
        if (!agent.enabled) agent.enabled = true;
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                agent.Warp(hit.position);
        }
    }

    bool TryGetRandomPointOnFloor(out Vector3 result)
    {
        result = transform.position;
        if (suelo == null) return false;
        Bounds b;
        if (suelo.TryGetComponent<Renderer>(out var rend)) b = rend.bounds;
        else if (suelo.TryGetComponent<Collider>(out var col)) b = col.bounds;
        else return false;

        for (int i = 0; i < 30; i++)
        {
            var candidate = new Vector3(
                Random.Range(b.min.x, b.max.x),
                b.center.y + 2f,
                Random.Range(b.min.z, b.max.z)
            );
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, spawnSampleRadius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 center = Application.isPlaying
            ? (transform.position + transform.forward * wanderCircleDistance)
            : (transform.position + Vector3.forward * wanderCircleDistance);
        Gizmos.DrawWireSphere(center, wanderCircleRadius);
    }
}
