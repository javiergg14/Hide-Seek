using UnityEngine;
using UnityEngine.AI;

public class RandomSpawn : MonoBehaviour
{
    [Header("Referencias")]
    public GameObject suelo;                 // Plano/terreno donde caminan
    public GameObject playerPrefab;          // Opcional: si quieres instanciar
    public GameObject enemyPrefab;           // Opcional: si quieres instanciar
    public bool instantiateIfMissing = false;

    [Header("Parámetros")]
    public float minSeparation = 5f;         // Distancia mínima entre Player y Enemy
    public int maxTries = 30;                // Intentos para encontrar puntos válidos
    public float sampleRadius = 8f;          // Radio para NavMesh.SamplePosition

    private Transform playerT;
    private Transform enemyT;

    void Awake()
    {
        // Obtén/instancia Player
        var existingPlayer = FindFirstObjectByType<Player>();
        if (existingPlayer != null) playerT = existingPlayer.transform;
        else if (instantiateIfMissing && playerPrefab != null)
            playerT = Instantiate(playerPrefab).transform;

        // Obtén/instancia Enemy
        var existingEnemy = FindFirstObjectByType<EnemyScript>();
        if (existingEnemy != null) enemyT = existingEnemy.transform;
        else if (instantiateIfMissing && enemyPrefab != null)
            enemyT = Instantiate(enemyPrefab).transform;
    }

    void Start()
    {
        PlaceAgents();
    }

    [ContextMenu("Respawn Now")]
    public void Respawn()
    {
        PlaceAgents();
    }

    void PlaceAgents()
    {
        if (suelo == null)
        {
            Debug.LogWarning("[RandomSpawn] Asigna el objeto 'suelo' en el Inspector.");
            return;
        }

        // Calcula dos puntos válidos separados
        if (!TryGetRandomPointOnFloor(out Vector3 p1))
        {
            Debug.LogWarning("[RandomSpawn] No se encontró punto válido para el Player.");
            return;
        }

        Vector3 p2 = p1;
        bool ok2 = false;
        for (int i = 0; i < maxTries; i++)
        {
            if (TryGetRandomPointOnFloor(out p2) &&
                Vector3.Distance(p1, p2) >= minSeparation)
            {
                ok2 = true;
                break;
            }
        }

        if (!ok2)
        {
            Debug.LogWarning("[RandomSpawn] No se encontró un segundo punto con separación suficiente. Revisa 'minSeparation' o el tamaño del NavMesh.");
            return;
        }

        // Coloca/warp
        if (playerT != null) WarpTo(playerT, p1, randomYaw: true);
        if (enemyT != null) WarpTo(enemyT, p2, randomYaw: true);
    }

    bool TryGetRandomPointOnFloor(out Vector3 result)
    {
        result = Vector3.zero;

        // Bounds del suelo (Renderer o Collider)
        Bounds b;
        if (suelo.TryGetComponent<Renderer>(out var rend)) b = rend.bounds;
        else if (suelo.TryGetComponent<Collider>(out var col)) b = col.bounds;
        else
        {
            Debug.LogWarning("[RandomSpawn] El 'suelo' no tiene Renderer ni Collider.");
            return false;
        }

        // Varios intentos de muestreo
        for (int i = 0; i < maxTries; i++)
        {
            var candidate = new Vector3(
                Random.Range(b.min.x, b.max.x),
                b.center.y + 2f, // un poco por encima del suelo
                Random.Range(b.min.z, b.max.z)
            );

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        return false;
    }

    void WarpTo(Transform t, Vector3 pos, bool randomYaw)
    {
        // Rotación aleatoria en Y (opcional)
        if (randomYaw)
        {
            var e = t.eulerAngles;
            e.y = Random.Range(0f, 360f);
            t.rotation = Quaternion.Euler(e);
        }

        // Si tiene NavMeshAgent, usa Warp
        if (t.TryGetComponent<NavMeshAgent>(out var agent))
        {
            if (!agent.enabled) agent.enabled = true;
            agent.Warp(pos);
            agent.ResetPath(); // limpia path anterior por si acaso
        }
        else
        {
            t.position = pos;
        }
    }
}
