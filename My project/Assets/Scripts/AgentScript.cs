using UnityEngine;
using UnityEngine.AI;

public class Player : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        agent.speed = 2.5f;              // velocidad de caminata humana (~2–3 m/s)
        agent.acceleration = 3.0f;       // arranque y frenado suaves
        agent.angularSpeed = 200.0f;     // giros más lentos y naturales
        agent.stoppingDistance = 0.3f;  // deja de moverse justo antes del destino
    }

    void Update()
    {
        // Movimiento con clic
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                agent.SetDestination(hit.point);
            }
        }

        // Suavizar rotación manual si no quieres depender del NavMeshAgent
        if (agent.velocity.sqrMagnitude > 0.1f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(agent.velocity.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }
    }
}
