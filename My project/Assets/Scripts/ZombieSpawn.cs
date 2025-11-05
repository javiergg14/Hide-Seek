using UnityEngine;
using System.Collections.Generic;

public class ZombieSpawner : MonoBehaviour
{
    public Zombie zombiePrefab;          // tu prefab único
    public Transform player;             // asigna el player (o lo resuelve en Start)
    public int count = 8;
    public float radius = 20f;           // radio alrededor del spawner
    public LayerMask navMeshMask;        // opcional si haces validaciones

    [Header("Puntos fijos (opcional)")]
    public List<Transform> fixedSpawnPoints = new List<Transform>();

    void Start()
    {
        if (!player && Camera.main) player = Camera.main.transform; // fallback

        if (fixedSpawnPoints != null && fixedSpawnPoints.Count > 0)
        {
            for (int i = 0; i < fixedSpawnPoints.Count; i++)
                SpawnAt(fixedSpawnPoints[i].position);
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = transform.position + Random.insideUnitSphere * radius;
                pos.y = transform.position.y; // ajusta a tu escena
                SpawnAt(pos);
            }
        }
    }

    void SpawnAt(Vector3 pos)
    {
        Zombie z = Instantiate(zombiePrefab, pos, Quaternion.identity);
        if (player) z.player = player;   // ayuda inicial para persecución
        // Asegúrate de que el prefab ya tenga Layer "Zombie" y sus componentes
    }
}
