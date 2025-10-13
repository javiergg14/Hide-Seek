using UnityEngine;
using UnityEngine.AI;

public class EnemyScript : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform player;

    [Header("Velocidades del enemigo")]
    public float minSpeed = 10f;   // cuando está muy cerca
    public float maxSpeed = 20f;   // cuando está lejos
    public float detectionRange = 1000f; // rango en el que la velocidad escala

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        player = FindFirstObjectByType<Player>().transform;
    }

    void Update()
    {
        if (player == null) return;

        // calcular distancia al jugador
        float distance = Vector3.Distance(transform.position, player.position);

        // escalar la velocidad entre minSpeed y maxSpeed en función de la distancia
        float t = Mathf.Clamp01(distance / detectionRange);
        agent.speed = Mathf.Lerp(minSpeed, maxSpeed, t);

        // perseguir al jugador
        agent.SetDestination(player.position);
    }
}
