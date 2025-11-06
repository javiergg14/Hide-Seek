using UnityEngine;
using UnityEngine.AI;

public class ZombieSpawnerArea : MonoBehaviour
{
    [Header("Prefab & Cantidad")]
    public GameObject zombiePrefab;
    public int count = 15;

    [Header("Área de spawn (BoxCollider)")]
    public BoxCollider spawnArea;

    [Header("Jugador / Cámara")]
    public Transform player;
    public Camera playerCamera;

    [Header("Ajuste al NavMesh")]
    public float navmeshProjectMaxDist = 10f;

    [Header("Exclusión alrededor del jugador")]
    [Tooltip("Radio (en metros) donde NO se permitirán spawns cerca del jugador.")]
    public float exclusionRadiusFromPlayer = 8f;

    [Header("Opcional")]
    public bool spawnOnStart = true;
    public int maxTriesPerZombie = 40;

    void Start()
    {
        if (!spawnArea) { Debug.LogError("[ZombieSpawnerArea] Falta 'spawnArea'."); return; }
        if (!zombiePrefab) { Debug.LogError("[ZombieSpawnerArea] Falta 'zombiePrefab'."); return; }

        if (spawnOnStart)
        {
            int spawned = 0;
            for (int i = 0; i < count; i++)
                if (SpawnOne()) spawned++;

            if (spawned < count)
                Debug.LogWarning($"[ZombieSpawnerArea] Spawneados {spawned}/{count}. Falta NavMesh/espacio fuera del radio de exclusión.");
        }
    }

    public bool SpawnOne()
    {
        Vector3 pos;
        int tries = 0;

        while (tries++ < maxTriesPerZombie)
        {
            pos = RandomPointInsideBoxWorld(spawnArea);

            // evitar cercanía al jugador
            if (player && Vector3.SqrMagnitude(pos - player.position) < exclusionRadiusFromPlayer * exclusionRadiusFromPlayer)
                continue;

            // proyectar al NavMesh
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, navmeshProjectMaxDist, NavMesh.AllAreas))
            {
                pos = hit.position;

                // comprobación de nuevo respecto al jugador (por si la proyección lo acerca)
                if (player && Vector3.SqrMagnitude(pos - player.position) < exclusionRadiusFromPlayer * exclusionRadiusFromPlayer)
                    continue;

                // instanciar
                GameObject go = Instantiate(zombiePrefab, pos, Quaternion.identity);

                var z = go.GetComponent<ZombieController>();
                if (z)
                {
                    z.player = player;
                    z.playerCamera = playerCamera;
                    z.patrolArea = spawnArea;
                }
                return true;
            }
        }

        Debug.LogWarning("[ZombieSpawnerArea] No se encontró punto válido para un zombie (intentos agotados).");
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

    void OnDrawGizmosSelected()
    {
        if (!spawnArea) return;
        Gizmos.color = new Color(0, 1, 0, 0.15f);
        Gizmos.matrix = spawnArea.transform.localToWorldMatrix;
        Gizmos.DrawCube(spawnArea.center, spawnArea.size);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(spawnArea.center, spawnArea.size);

        if (player && exclusionRadiusFromPlayer > 0f)
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.6f);
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawWireSphere(player.position, exclusionRadiusFromPlayer);
        }
    }
}
