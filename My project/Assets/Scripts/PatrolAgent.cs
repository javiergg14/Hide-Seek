using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PatrolAgent : MonoBehaviour
{
    public enum PatrolMode { Loop, PingPong }

    [Header("Waypoints")]
    public Transform contenedorWaypoints;   // Padre con hijos como waypoints
    public PatrolMode modo = PatrolMode.PingPong;
    [Tooltip("Distancia para considerar 'llegado' y pasar al siguiente WP")]
    public float distanciaCambio = 1.0f;

    [Header("Spawn")]
    public bool spawnEnWaypointAleatorio = true;
    public float spawnSampleRadius = 2.0f; // para ajustar el spawn al NavMesh cerca del WP

    [Header("Suavizado (Ghost)")]
    [Tooltip("Menor = mas suave (mas filtrado)")]
    public float ghostResponsiveness = 0.25f;
    [Tooltip("Anticipacion hacia la velocidad actual (abre curvas)")]
    public float lookAhead = 2.0f;

    [Header("Robustez")]
    [Tooltip("Radio para ajustar puntos al NavMesh")]
    public float sampleRadius = 4f;
    [Tooltip("Radio para buscar alternativa alrededor del WP si no hay ruta directa")]
    public float probeAroundWP = 2.0f;
    [Tooltip("Tiempo sin progreso para cambiar de WP")]
    public float stuckSeconds = 2.0f;

    private Transform[] wps;
    private int index;
    private int dir = 1;

    private NavMeshAgent agent;

    // Ghost solo para suavizar rotacion/depurar (no para SetDestination continuo)
    private Vector3 ghostPos;
    private Vector3 ghostVel;

    // Stuck
    private float stuckTimer = 0f;
    private Vector3 lastPos;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.autoBraking = false; // transiciones mas suaves entre WPs
    }

    void Start()
    {
        if (contenedorWaypoints == null || contenedorWaypoints.childCount == 0)
        {
            Debug.LogError("[PatrolAgent] Falta contenedorWaypoints con hijos.");
            enabled = false; return;
        }

        // Cargar lista de waypoints
        wps = new Transform[contenedorWaypoints.childCount];
        for (int i = 0; i < wps.Length; i++) wps[i] = contenedorWaypoints.GetChild(i);

        // Punto y direccion inicial aleatorios
        index = Random.Range(0, wps.Length);
        dir = Random.value < 0.5f ? 1 : -1;

        EnsureOnNavMeshOrDisable();

        ghostPos = transform.position;
        lastPos = transform.position;

        // Spawn en waypoint aleatorio (opcional)
        if (spawnEnWaypointAleatorio)
        {
            Vector3 spawnPos = wps[index].position;
            if (NavMesh.SamplePosition(spawnPos, out var hit, spawnSampleRadius, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                ghostPos = hit.position;
            }
            else
            {
                // Fallback: intenta alrededor del waypoint
                for (int i = 0; i < 6; i++)
                {
                    var cand = spawnPos + Random.insideUnitSphere * spawnSampleRadius;
                    if (NavMesh.SamplePosition(cand, out var h2, spawnSampleRadius, NavMesh.AllAreas))
                    {
                        agent.Warp(h2.position);
                        ghostPos = h2.position;
                        break;
                    }
                }
            }

            // Si spawneamos en el WP "index", el primer destino debe ser el siguiente
            index = GetNextIndex(index, dir);
        }

        // Pone el destino al waypoint alcanzable
        SetDestinationToReachableWP(wps[index]);
    }

    void Update()
    {
        if (!enabled || wps == null || wps.Length == 0) return;
        if (!agent.enabled || !agent.isOnNavMesh) return;

        Transform objetivo = wps[index];

        // Llegada al WP -> escoger siguiente y poner destino UNA VEZ
        if (!agent.pathPending && agent.remainingDistance <= Mathf.Max(distanciaCambio, agent.stoppingDistance + 0.1f))
        {
            AvanzarIndice();
            SetDestinationToReachableWP(wps[index]);
        }

        // Ghost: suaviza rotacion/visual (no cambia destino del agent)
        Vector3 toWP = (objetivo.position - ghostPos);
        Vector3 desired = toWP.sqrMagnitude > 0.0001f ? toWP.normalized * agent.speed : Vector3.zero;
        float a = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, ghostResponsiveness));
        ghostVel = Vector3.Lerp(ghostVel, desired, a);
        ghostPos += ghostVel * Time.deltaTime;
        if (agent.velocity.sqrMagnitude > 0.01f) ghostPos += agent.velocity.normalized * lookAhead * Time.deltaTime;

        // Stuck / ruta parcial -> escoger siguiente WP y reponer destino
        UpdateStuck();
        if (agent.pathStatus == NavMeshPathStatus.PathInvalid ||
            agent.pathStatus == NavMeshPathStatus.PathPartial ||
            stuckTimer > stuckSeconds)
        {
            AvanzarIndice();
            SetDestinationToReachableWP(wps[index]);
            stuckTimer = 0f;
        }
    }

    void UpdateStuck()
    {
        float moved = (transform.position - lastPos).sqrMagnitude;
        lastPos = transform.position;
        bool noProgress = moved < 0.001f && agent.velocity.sqrMagnitude < 0.01f && !agent.pathPending;
        if (noProgress) stuckTimer += Time.deltaTime; else stuckTimer = 0f;
    }

    void AvanzarIndice()
    {
        if (modo == PatrolMode.Loop)
        {
            index = (index + dir) % wps.Length;
            if (index < 0) index += wps.Length;
        }
        else
        {
            index += dir;
            if (index >= wps.Length) { index = wps.Length - 2; dir = -1; }
            if (index < 0) { index = 1; dir = 1; }
        }
    }

    int GetNextIndex(int current, int direction)
    {
        if (modo == PatrolMode.Loop)
        {
            int next = (current + direction) % wps.Length;
            if (next < 0) next += wps.Length;
            return next;
        }
        else
        {
            int next = current + direction;
            if (next >= wps.Length) { next = wps.Length - 2; dir = -1; }
            if (next < 0) { next = 1; dir = 1; }
            return next;
        }
    }

    void SetDestinationToReachableWP(Transform wp)
    {
        if (TryGetReachable(wp.position, out Vector3 finalTarget, probeAroundWP))
        {
            agent.SetDestination(finalTarget);
        }
        else
        {
            // Si el WP no es alcanzable, salta al siguiente
            AvanzarIndice();
            if (TryGetReachable(wps[index].position, out Vector3 alt, probeAroundWP))
                agent.SetDestination(alt);
        }
    }

    bool TryGetReachable(Vector3 target, out Vector3 reachable, float probeRadius)
    {
        reachable = target;
        if (!NavMesh.SamplePosition(target, out var hit, probeRadius, NavMesh.AllAreas))
            return false;

        var path = new NavMeshPath();
        if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
        { reachable = hit.position; return true; }

        for (int i = 0; i < 6; i++)
        {
            Vector3 cand = target + Random.insideUnitSphere * probeRadius;
            if (NavMesh.SamplePosition(cand, out var h2, probeRadius, NavMesh.AllAreas))
            {
                if (agent.CalculatePath(h2.position, path) && path.status == NavMeshPathStatus.PathComplete)
                { reachable = h2.position; return true; }
            }
        }
        return false;
    }

    void EnsureOnNavMeshOrDisable()
    {
        if (!agent.enabled) agent.enabled = true;
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
                agent.Warp(hit.position);
            else { Debug.LogError("[PatrolAgent] No NavMesh under agent."); enabled = false; }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(ghostPos, 0.25f);
    }
}
