using UnityEngine;
using UnityEngine.AI;

public class LeaderWander : MonoBehaviour
{
    public GameObject suelo;
    public float tiempoEntrePuntos = 3f;
    public float radioBusqueda = 30f;   // radio para buscar nuevos puntos
    public int intentosMaximos = 10;

    private NavMeshAgent agent;
    private float temporizador;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        temporizador = tiempoEntrePuntos;

        // Asegura que el líder está sobre el NavMesh
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            else
            {
                Debug.LogError("[LeaderWander] No hay NavMesh cerca del líder.");
                enabled = false;
                return;
            }
        }

        // Forzamos un primer destino válido
        agent.SetDestination(ObtenerPuntoAleatorioSobreSuelo());
        temporizador = 0f;
    }

    void Update()
    {
        temporizador += Time.deltaTime;

        if (!agent.pathPending && (agent.remainingDistance < 1f || temporizador >= tiempoEntrePuntos))
        {
            agent.isStopped = false;
            agent.SetDestination(ObtenerPuntoAleatorioSobreSuelo());
            temporizador = 0f;
        }
    }

    Vector3 ObtenerPuntoAleatorioSobreSuelo()
    {
        // Centro y extensión: si hay Renderer úsalo; si no, usa posición del suelo como centro
        Vector3 centro = suelo ? suelo.transform.position : transform.position;
        Vector3 tam = Vector3.one * (radioBusqueda * 2f);
        if (suelo && suelo.TryGetComponent<Renderer>(out var rend))
            tam = rend.bounds.size;

        for (int i = 0; i < intentosMaximos; i++)
        {
            Vector3 randomPos = new Vector3(
                Random.Range(centro.x - tam.x / 2f, centro.x + tam.x / 2f),
                centro.y + 2f,
                Random.Range(centro.z - tam.z / 2f, centro.z + tam.z / 2f)
            );

            if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                return hit.position;
        }

        // Último recurso: alrededor de la posición actual
        if (NavMesh.SamplePosition(transform.position + Random.insideUnitSphere * radioBusqueda, out NavMeshHit hit2, 10f, NavMesh.AllAreas))
            return hit2.position;

        Debug.LogWarning("[LeaderWander] No se encontró punto válido en NavMesh; devolviendo posición actual.");
        return transform.position;
    }
}
