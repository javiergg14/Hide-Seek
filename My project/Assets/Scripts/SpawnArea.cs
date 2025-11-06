using UnityEngine;
using UnityEngine.AI;

public class ZombieSpawnerArea : MonoBehaviour
{
    [Header("Prefab & Cantidad")]
    public GameObject zombiePrefab;
    public int count = 15;

    [Header("Área de spawn (BoxCollider)")]
    public BoxCollider spawnArea;          // Arrastra aquí el BoxCollider que delimita el terreno

    [Header("Jugador / Cámara")]
    public Transform player;
    public Camera playerCamera;

    [Header("Ajuste al NavMesh")]
    [Tooltip("Distancia máxima para proyectar el punto de spawn al NavMesh.")]
    public float navmeshProjectMaxDist = 10f;

    [Header("Opcional")]
    public bool spawnOnStart = true;

    void Start()
    {
        if (!spawnArea)
        {
            Debug.LogError("[ZombieSpawnerArea] Falta asignar 'spawnArea' (BoxCollider).");
            return;
        }

        if (!zombiePrefab)
        {
            Debug.LogError("[ZombieSpawnerArea] Falta asignar 'zombiePrefab'.");
            return;
        }

        if (spawnOnStart)
        {
            for (int i = 0; i < count; i++)
                SpawnOne();
        }
    }

    public void SpawnOne()
    {
        Vector3 pos = RandomPointInsideBoxWorld(spawnArea);

        // Proyectar al NavMesh para que caiga en suelo navegable
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, navmeshProjectMaxDist, NavMesh.AllAreas))
            pos = hit.position;
        else
            Debug.LogWarning("[ZombieSpawnerArea] No se pudo proyectar al NavMesh. Revisa el área/bake.", this);

        GameObject go = Instantiate(zombiePrefab, pos, Quaternion.identity);

        // Pasar referencias al zombie
        var z = go.GetComponent<Zombie>();
        if (z)
        {
            z.player = player;
            z.playerCamera = playerCamera;
            z.patrolArea = spawnArea; // misma área para patrullar
        }
    }

    // === Utilidades ===
    public static Vector3 RandomPointInsideBoxWorld(BoxCollider box)
    {
        // Elegimos un punto aleatorio en el espacio local del collider
        Vector3 half = box.size * 0.5f;
        Vector3 local = new Vector3(
            Random.Range(-half.x, half.x),
            Random.Range(-half.y, half.y),
            Random.Range(-half.z, half.z)
        );

        // Lo convertimos a mundo respetando posición/rotación/escala del collider
        return box.transform.TransformPoint(box.center + local);
    }

    // Gizmos para ver el área
    void OnDrawGizmosSelected()
    {
        if (!spawnArea) return;

        Gizmos.color = new Color(0, 1, 0, 0.15f);
        Gizmos.matrix = spawnArea.transform.localToWorldMatrix;
        Gizmos.DrawCube(spawnArea.center, spawnArea.size);

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(spawnArea.center, spawnArea.size);
    }
}
