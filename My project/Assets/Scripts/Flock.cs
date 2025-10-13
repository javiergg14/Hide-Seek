using UnityEngine;
using UnityEngine.AI;

public class Flock : MonoBehaviour
{
    public FlockManager miManager;
    private NavMeshAgent agent;

    [Header("Pesos de reglas (tuneables)")]
    public float wSeparacion = 1.2f;
    public float wAlineacion = 1.0f;
    public float wCohesion = 1.0f;

    [Header("Líder")]
    public float wLiderAtraccion = 1.2f;   // empuje hacia el líder
    public float wLiderAlineacion = 0.6f;  // alinear rumbo con el líder
    public float radioLider = 6f;          // radio cómodo alrededor del líder

    [Header("Parámetros")]
    public float distanciaSeparacion = 1.0f;
    public float velocidadMinDeseada = 2.0f;
    public float velocidadMaxDeseada = 5.0f;

    // Wander (cuando no hay vecinos)
    private Vector3 wanderTarget;
    private float wanderCooldown = 1.0f;
    private float wanderTimer = 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        // Velocidad inicial tomada del manager
        float v = Random.Range(miManager.velocidadMin, miManager.velocidadMax);
        agent.speed = Mathf.Clamp(v, velocidadMinDeseada, velocidadMaxDeseada);

        // Garantiza estar sobre NavMesh
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            else
                Debug.LogWarning($"{name} no está sobre el NavMesh.");
        }

        wanderTarget = transform.position;
    }

    void Update()
    {
        AplicarReglas();
        if (wanderTimer > 0f) wanderTimer -= Time.deltaTime;
    }

    void AplicarReglas()
    {
        GameObject[] todos = miManager.todosLosBoids;

        Vector3 separacion = Vector3.zero;
        Vector3 alineacion = Vector3.zero;
        Vector3 cohesion = Vector3.zero;

        int vecinos = 0;

        // Promedios para alineación (velocidades/headings de vecinos)
        foreach (GameObject obj in todos)
        {
            if (obj == this.gameObject) continue;
            if (miManager.lider != null && obj.transform == miManager.lider) continue; // ignora al líder como vecino

            float d = Vector3.Distance(obj.transform.position, transform.position);
            if (d <= miManager.distanciaVecino)
            {
                vecinos++;

                // COHESIÓN: media de posiciones
                cohesion += obj.transform.position;

                // SEPARACIÓN: empuje inverso cuando está muy cerca
                if (d < distanciaSeparacion)
                    separacion += (transform.position - obj.transform.position) / Mathf.Max(d, 0.001f);

                // ALINEACIÓN: suma de headings/velocidades
                if (obj.TryGetComponent<NavMeshAgent>(out var otherAgent))
                {
                    Vector3 v = otherAgent.velocity;
                    if (v.sqrMagnitude > 0.0001f) alineacion += v.normalized;
                }
                else
                {
                    alineacion += obj.transform.forward;
                }
            }
        }

        Vector3 direccionDeseada = Vector3.zero;

        if (vecinos > 0)
        {
            // Cohesión -> hacia el centro medio
            cohesion /= vecinos;
            Vector3 dirCohesion = (cohesion - transform.position);
            if (dirCohesion.sqrMagnitude > 0.0001f) dirCohesion.Normalize();

            // Separación ya es suma de empujes – normaliza para evitar picos
            if (separacion.sqrMagnitude > 0.0001f) separacion = separacion.normalized;

            // Alineación -> hacia heading promedio
            if (alineacion.sqrMagnitude > 0.0001f) alineacion = alineacion.normalized;

            direccionDeseada += dirCohesion * wCohesion;
            direccionDeseada += separacion * wSeparacion;
            direccionDeseada += alineacion * wAlineacion;
        }
        else
        {
            // Wander con caché (si no hay vecinos)
            if (wanderTimer <= 0f || agent.remainingDistance < 0.3f || !agent.hasPath)
            {
                Vector3 candidato = transform.position + Random.insideUnitSphere * 2.5f;
                if (NavMesh.SamplePosition(candidato, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                    wanderTarget = hit.position;

                wanderTimer = wanderCooldown;
            }
            direccionDeseada += (wanderTarget - transform.position);
        }

        // Influencia del líder
        if (miManager.lider != null)
        {
            Vector3 haciaLider = (miManager.lider.position - transform.position);
            float distLider = haciaLider.magnitude;

            if (distLider > 0.001f)
            {
                Vector3 dirLider = haciaLider / distLider;

                // Atracción modulada: fuerte fuera del radio, suave dentro
                float factor = (distLider > radioLider)
                    ? Mathf.InverseLerp(radioLider, radioLider * 3f, distLider) // 0..1
                    : 0.2f;

                direccionDeseada += dirLider * wLiderAtraccion * Mathf.Clamp01(factor);

                // Alineación con el rumbo del líder (si se mueve)
                if (miManager.lider.TryGetComponent<NavMeshAgent>(out var leaderAgent))
                {
                    Vector3 vLider = leaderAgent.velocity;
                    if (vLider.sqrMagnitude > 0.0001f)
                        direccionDeseada += vLider.normalized * wLiderAlineacion;
                }
                else
                {
                    direccionDeseada += miManager.lider.forward * (wLiderAlineacion * 0.5f);
                }
            }
        }

        // Normaliza dirección y define un “micro-destino” delante
        if (direccionDeseada.sqrMagnitude > 0.0001f)
        {
            direccionDeseada = direccionDeseada.normalized;

            float velObjetivo = Mathf.Clamp(agent.speed, velocidadMinDeseada, velocidadMaxDeseada);
            Vector3 destino = transform.position + direccionDeseada * 3f; // paso corto

            if (agent.isOnNavMesh && NavMesh.SamplePosition(destino, out NavMeshHit hit2, 5f, NavMesh.AllAreas))
            {
                bool debeActualizar =
                    !agent.hasPath ||
                    agent.remainingDistance < 0.2f ||
                    (agent.destination - hit2.position).sqrMagnitude > 0.25f;

                if (debeActualizar)
                {
                    agent.speed = velObjetivo;
                    agent.SetDestination(hit2.position);
                }
            }
        }
    }
}
