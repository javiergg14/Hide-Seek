using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PoliceHunterSearch : MonoBehaviour
{
    public enum PoliceState { Chase, Search }

    [Header("Target")]
    public Transform target;                 // Player (robber)
    public float visionRange = 40f;
    public float eyeHeight = 1.6f;
    public LayerMask obstacleMask;           // Obstacles/covers/paredes (NO incluyas la capa del Player)

    [Header("Search")]
    public List<Collider> hideObstacles = new List<Collider>();
    public float searchRadius = 25f;
    public float coverOffset = 2.0f;
    public int maxCandidates = 10;

    [Header("Timing")]
    public float loseSightDelay = 0.3f;
    public float repathInterval = 0.2f;
    public float investigatePause = 0.5f;
    public float giveUpSearchAfter = 15f;

    [Header("NavMesh")]
    public float sampleRadius = 6f;

    private NavMeshAgent agent;
    private PoliceState state = PoliceState.Chase;

    private float repathTimer;
    private float losLostTimer;
    private float investigateTimer;
    private float searchTimer;

    private Vector3 lastSeenPosition;
    private readonly Queue<Vector3> searchQueue = new Queue<Vector3>();
    private Vector3 currentSearchSpot;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (target == null)
        {
            var p = FindFirstObjectByType<Player>();
            if (p != null) target = p.transform;
            else
            {
                var t = GameObject.FindWithTag("Player");
                if (t != null) target = t.transform;
            }
        }

        if (hideObstacles.Count == 0)
        {
            foreach (var go in GameObject.FindGameObjectsWithTag("HidingObstacle"))
            {
                var col = go.GetComponent<Collider>();
                if (col != null) hideObstacles.Add(col);
            }
        }
    }

    void Start()
    {
        EnsureOnNavMesh();
        if (target != null) lastSeenPosition = target.position;
        // Primer destino por si ya lo vemos al empezar
        TrySetDestination(lastSeenPosition);
    }

    void Update()
    {
        if (target == null || !agent.enabled || !agent.isOnNavMesh) return;

        repathTimer += Time.deltaTime;

        bool canSee = HasLineOfSightToTarget();
        if (canSee)
        {
            lastSeenPosition = target.position;
            losLostTimer = 0f;
            searchTimer = 0f;
            if (state != PoliceState.Chase)
            {
                state = PoliceState.Chase;
                searchQueue.Clear();
            }
        }
        else
        {
            losLostTimer += Time.deltaTime;
        }

        if (state == PoliceState.Chase) TickChase(canSee);
        else TickSearch(canSee);
    }

    // -------- CHASE --------
    void TickChase(bool canSee)
    {
        if (canSee)
        {
            if (repathTimer >= repathInterval)
            {
                repathTimer = 0f;
                TrySetDestination(lastSeenPosition);  // <-- ya sin comprobacion interna duplicada
            }
        }
        else
        {
            if (losLostTimer >= loseSightDelay)
            {
                BuildSearchQueue();
                if (searchQueue.Count == 0) TrySetDestination(lastSeenPosition);
                state = PoliceState.Search;
                investigateTimer = 0f;
                searchTimer = 0f;
            }
        }
    }

    // -------- SEARCH --------
    void TickSearch(bool canSee)
    {
        if (canSee)
        {
            state = PoliceState.Chase;
            searchQueue.Clear();
            return;
        }

        searchTimer += Time.deltaTime;

        if (giveUpSearchAfter > 0f && searchTimer >= giveUpSearchAfter)
        {
            searchQueue.Clear();
            return;
        }

        // Si llego (o casi) al objetivo actual, espera un momento e intenta el siguiente
        if (!agent.pathPending && agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, 0.3f))
        {
            investigateTimer += Time.deltaTime;
            if (investigateTimer >= investigatePause)
            {
                investigateTimer = 0f;

                // Si no quedan puntos, genera otra tanda y continua
                if (searchQueue.Count == 0)
                    BuildSearchQueue();

                if (searchQueue.Count > 0)
                {
                    currentSearchSpot = searchQueue.Dequeue();
                    TrySetDestination(currentSearchSpot);
                }
                else
                {
                    // ultimo recurso: paseito corto aleatorio para no quedarse quieto
                    Vector3 probe = transform.position + Random.insideUnitSphere * 3f;
                    if (NavMesh.SamplePosition(probe, out var h2, 4f, NavMesh.AllAreas))
                        TrySetDestination(h2.position);
                }
            }
        }
        else
        {
            // si no tiene path, arranca Search de inmediato
            if (!agent.hasPath && searchQueue.Count > 0)
            {
                currentSearchSpot = searchQueue.Dequeue();
                TrySetDestination(currentSearchSpot);
            }
        }
    }

    // -------- SEARCH BUILD --------
    void BuildSearchQueue()
    {
        searchQueue.Clear();

        List<Vector3> candidates = new List<Vector3>();

        // 1) Spots detras de obstaculos cerca de la ultima posicion vista
        if (hideObstacles != null && hideObstacles.Count > 0)
        {
            foreach (var col in hideObstacles)
            {
                if (col == null) continue;

                Vector3 closestToSeen = col.ClosestPoint(lastSeenPosition);
                if ((closestToSeen - lastSeenPosition).sqrMagnitude > searchRadius * searchRadius)
                    continue;

                Vector3 dir = closestToSeen - lastSeenPosition;
                dir.y = 0f;
                float mag = dir.magnitude;
                if (mag < 0.01f) continue;
                dir /= mag;

                Vector3 candidate = closestToSeen + dir * coverOffset;

                if (NavMesh.SamplePosition(candidate, out var hit, sampleRadius, NavMesh.AllAreas))
                {
                    Vector3 cover = hit.position;

                    // Relajar la condicion de bloqueo: si no bloquea perfecto desde lastSeen,
                    // aun asi aceptamos si hay ruta completa (evita quedarnos sin puntos)
                    if (!IsPathComplete(cover)) continue;

                    candidates.Add(cover);
                }
            }
        }

        // 2) Fallback: anillo de busqueda alrededor de lastSeenPosition si no hay suficientes
        int needed = Mathf.Max(0, maxCandidates - candidates.Count);
        if (needed > 0)
        {
            float radius = Mathf.Clamp(searchRadius * 0.6f, 4f, searchRadius); // anillo medio
            int ringPoints = Mathf.Max(needed, 6); // al menos 6 puntos
            for (int i = 0; i < ringPoints; i++)
            {
                float ang = (Mathf.PI * 2f) * (i / (float)ringPoints);
                Vector3 p = lastSeenPosition + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius;

                if (NavMesh.SamplePosition(p, out var hit, sampleRadius, NavMesh.AllAreas) && IsPathComplete(hit.position))
                    candidates.Add(hit.position);
            }
        }

        // Orden: primero mas cerca de lastSeen, luego mas cerca de la police
        candidates.Sort((a, b) =>
        {
            float da = (a - lastSeenPosition).sqrMagnitude + 0.25f * (a - transform.position).sqrMagnitude;
            float db = (b - lastSeenPosition).sqrMagnitude + 0.25f * (b - transform.position).sqrMagnitude;
            return da.CompareTo(db);
        });

        // Limitar y encolar
        int count = Mathf.Min(maxCandidates, candidates.Count);
        for (int i = 0; i < count; i++) searchQueue.Enqueue(candidates[i]);

        // Si aun asi no hay nada, al menos ir a la ultima posicion vista
        if (searchQueue.Count == 0)
            searchQueue.Enqueue(lastSeenPosition);
    }


    // -------- VISION --------
    bool HasLineOfSightToTarget()
    {
        if (target == null) return false;

        Vector3 from = transform.position + Vector3.up * eyeHeight;
        Vector3 to = target.position + Vector3.up * eyeHeight;

        if ((to - from).sqrMagnitude > visionRange * visionRange) return false;

        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist <= 0.001f) return true;
        dir /= dist;

        // Si el raycast impacta con obstacleMask, no hay vision
        if (Physics.Raycast(from, dir, out RaycastHit hit, dist, obstacleMask, QueryTriggerInteraction.Ignore))
            return false;

        return true;
    }

    bool BlocksLineOfSight(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist <= 0.001f) return false;
        dir /= dist;
        return Physics.Raycast(from, dir, dist, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    // -------- NAVMESH UTILS --------
    void TrySetDestination(Vector3 pos)
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;

        if (NavMesh.SamplePosition(pos, out var hit, sampleRadius, NavMesh.AllAreas))
        {
            bool update =
                !agent.hasPath ||
                agent.remainingDistance < 0.2f ||
                (agent.destination - hit.position).sqrMagnitude > 0.25f;

            if (update) agent.SetDestination(hit.position);
        }
    }

    bool IsPathComplete(Vector3 dest)
    {
        if (!NavMesh.SamplePosition(dest, out var hit, sampleRadius, NavMesh.AllAreas))
            return false;

        var path = new NavMeshPath();
        if (!agent.CalculatePath(hit.position, path)) return false;
        return path.status == NavMeshPathStatus.PathComplete;
    }

    void EnsureOnNavMesh()
    {
        if (!agent.enabled) agent.enabled = true;
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, 10f, NavMesh.AllAreas))
                agent.Warp(hit.position);
        }
    }
}
