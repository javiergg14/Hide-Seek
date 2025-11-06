using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Zombie : MonoBehaviour
{
    [Header("Referencias")]
    public Transform player;
    public Camera playerCamera;
    public BoxCollider patrolArea;         // Área para patrulla y para elegir destinos random

    [Header("Velocidades")]
    public float wanderSpeed = 1.5f;
    public float chaseSpeed = 3.2f;

    [Header("Patrulla")]
    public float wanderIntervalMin = 2.0f;
    public float wanderIntervalMax = 4.0f;

    [Header("Persecución")]
    public float maxChaseDistance = 40f;   // si se aleja demasiado del jugador, vuelve a patrullar

    [Header("Comunicación")]
    public float alertRadius = 15f;        // radio para avisar a otros zombies
    public float alertCooldown = 2.0f;     // para no spamear avisos
    public LayerMask zombieLayer;          // opcional: capa "Zombie" para filtrar
    public string zombieTag = "Zombie";    // opcional: tag para filtrar

    [Header("Animación (opcional)")]
    public Animator animator;              // parámetros sugeridos: "Speed"(float), "Alert"(bool)

    private NavMeshAgent agent;
    private float nextWanderTime;
    private bool isChasing;
    private float nextAlertTime;

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
        // Asegurar que arranca en NavMesh
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            transform.position = hit.position;

        if (CompareTag(zombieTag) == false && !string.IsNullOrEmpty(zombieTag))
            gameObject.tag = zombieTag;
    }

    void Update()
    {
        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        if (!player) return;

        bool inPlayerView = IsInPlayerFrustum();
        float distToPlayer = Vector3.Distance(transform.position, player.position);

        if (inPlayerView)
        {
            // Entró en frustum -> perseguir + alertar
            if (!isChasing)
            {
                isChasing = true;
                agent.speed = chaseSpeed;
                if (animator) animator.SetBool("Alert", true);
                TryAlertNearbyZombies();
            }
            agent.SetDestination(player.position);
        }
        else
        {
            // Si estaba persiguiendo pero ya no se ve y está muy lejos, volver a patrullar
            if (isChasing && distToPlayer > maxChaseDistance)
            {
                isChasing = false;
                if (animator) animator.SetBool("Alert", false);
                ScheduleNextWanderSoon();
            }

            if (!isChasing)
            {
                agent.speed = wanderSpeed;
                HandleWanderInArea();
            }
            else
            {
                // Sigue en chase aunque no esté en frustum si aún está cerca
                agent.SetDestination(player.position);
            }
        }

        // Animación según velocidad
        if (animator) animator.SetFloat("Speed", agent.velocity.magnitude);

        // Orientación suave dependiente de la velocidad
        if (agent.velocity.sqrMagnitude > 0.05f)
        {
            Quaternion look = Quaternion.LookRotation(agent.velocity.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 5f);
        }
    }

    // ====== Percepción por frustum de la cámara del jugador ======
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

    // ====== Patrulla dentro de un área (cuando NO están en cámara) ======
    void HandleWanderInArea()
    {
        if (!patrolArea)
        {
            // fallback: moverse cerca en random si no hay área
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
        // Transformar punto a espacio local del BoxCollider y comprobar si está dentro
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

    void ScheduleNextWander()
    {
        nextWanderTime = Time.time + Random.Range(wanderIntervalMin, wanderIntervalMax);
    }

    void ScheduleNextWanderSoon()
    {
        nextWanderTime = Time.time + Random.Range(0.2f, 0.6f);
    }

    bool ReachedDestination()
    {
        if (agent.pathPending) return false;
        return agent.remainingDistance <= agent.stoppingDistance + 0.05f;
    }

    // ====== Comunicación entre zombies ======
    void TryAlertNearbyZombies()
    {
        if (Time.time < nextAlertTime) return;
        nextAlertTime = Time.time + alertCooldown;

        // Encuentra "otros" zombies cerca y les envía un mensaje
        Collider[] hits;
        if (zombieLayer.value != 0)
            hits = Physics.OverlapSphere(transform.position, alertRadius, zombieLayer);
        else
            hits = Physics.OverlapSphere(transform.position, alertRadius);

        foreach (var h in hits)
        {
            if (!h || h.attachedRigidbody && h.attachedRigidbody.gameObject == gameObject) continue;

            var go = h.attachedRigidbody ? h.attachedRigidbody.gameObject : h.gameObject;

            // Filtro por tag si lo necesitas
            if (!string.IsNullOrEmpty(zombieTag) && !go.CompareTag(zombieTag)) continue;
            if (go == gameObject) continue;

            // Enviar mensaje al otro zombie: "¡He visto al jugador!"
            // Usamos SendMessage para ese GameObject concreto.
            go.SendMessage("OnAllySpottedPlayer",
                player.position,
                SendMessageOptions.DontRequireReceiver);
        }

        // Además, este zombie avisa a *sus* componentes/hijos (por si hay lógica adicional)
        BroadcastMessage("OnPlayerSpottedLocal",
            player.position,
            SendMessageOptions.DontRequireReceiver);
    }

    // Recibe aviso de otro zombie
    void OnAllySpottedPlayer(Vector3 lastKnownPlayerPos)
    {
        // Pasa a alerta/persecución (o al menos a investigar)
        isChasing = true;
        agent.speed = chaseSpeed;
        if (animator) animator.SetBool("Alert", true);

        // Si tenemos player, perseguimos player; si no, ir a la última posición conocida
        if (player)
            agent.SetDestination(player.position);
        else
            agent.SetDestination(lastKnownPlayerPos);
    }

    // Aviso local (por si quieres efectos, sonidos, etc.)
    void OnPlayerSpottedLocal(Vector3 pos)
    {
        // Aquí puedes reproducir un gruñido, FX, etc.
        // (Dejamos el método para que BroadcastMessage no falle)
    }

    void OnDrawGizmosSelected()
    {
        // Radio de alerta
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, alertRadius);

        // Visual del área
        if (patrolArea)
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.2f);
            Gizmos.matrix = patrolArea.transform.localToWorldMatrix;
            Gizmos.DrawCube(patrolArea.center, patrolArea.size);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 1f);
            Gizmos.DrawWireCube(patrolArea.center, patrolArea.size);
        }
    }
}
