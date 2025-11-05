using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class Zombie : MonoBehaviour
{
    public enum State { Idle, Wander, Investigate, Chase }

    [Header("Referencias")]
    public Transform player;
    public Animator animator;
    public SmellSensor smellSensor;

    [Header("Movimiento")]
    public float wanderRadius = 6f;
    public float wanderDelay = 3f;
    public float repathInterval = 0.2f;

    [Header("Percepción y pérdida de objetivo")]
    public float lostTargetAfterSeconds = 3f;

    [Header("Comunicación")]
    public float allyAlertRadius = 15f;
    public LayerMask zombieLayer;

    [Header("Prioridades")]
    public bool prioritizePlayerOverSmell = true;
    public float smellConsiderCooldown = 0.5f;

    private float lastSmellConsiderTime = -999f;
    private NavMeshAgent agent;
    private State state = State.Idle;
    private float lastSeenTime = Mathf.NegativeInfinity;
    private Vector3 lastInterestingPoint;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponent<Animator>();
        if (!smellSensor) smellSensor = GetComponentInChildren<SmellSensor>(true);
    }

    void OnEnable()
    {
        if (smellSensor) smellSensor.onSmell += OnSmellDetected;
    }

    void OnDisable()
    {
        if (smellSensor) smellSensor.onSmell -= OnSmellDetected;
    }

    void Start()
    {
        StartCoroutine(WanderLoop());
        StartCoroutine(RepathLoop());
    }

    void Update()
    {
        // Si perseguía al jugador y ya hace mucho que no lo ve/oye, pasa a investigar el último punto
        if (state == State.Chase && Time.time - lastSeenTime > lostTargetAfterSeconds)
        {
            state = State.Investigate;
            SetDestination(lastInterestingPoint);
        }

        // Control de animaciones (opcional)
        if (animator)
        {
            float speed = agent.velocity.magnitude;
            animator.SetFloat("Speed", speed);
        }
    }

    IEnumerator WanderLoop()
    {
        var wait = new WaitForSeconds(wanderDelay);
        while (true)
        {
            if (state == State.Idle || state == State.Wander || state == State.Investigate)
            {
                if (!agent.hasPath || agent.remainingDistance < agent.stoppingDistance + 0.1f)
                {
                    Vector3 random = Random.insideUnitSphere * wanderRadius + transform.position;
                    if (NavMesh.SamplePosition(random, out var hit, 3f, NavMesh.AllAreas))
                    {
                        state = State.Wander;
                        SetDestination(hit.position);
                    }
                }
            }
            yield return wait;
        }
    }

    IEnumerator RepathLoop()
    {
        var wait = new WaitForSeconds(repathInterval);
        while (true)
        {
            if (state == State.Chase && player)
                SetDestination(player.position);
            yield return wait;
        }
    }

    void SetDestination(Vector3 pos)
    {
        if (agent.enabled) agent.SetDestination(pos);
    }


    // Llamado por ZombieFrustumPerception cuando el zombie entra en el frustum de la cámara
    public void OnSeenByPlayerCamera(Camera cam)
    {
        if (!player) player = cam.transform;
        lastSeenTime = Time.time;
        lastInterestingPoint = player.position;

        state = State.Chase;
        SetDestination(player.position);

        AlertAllies(player);
    }

    // Llamado por SmellSensor cuando detecta una gota "SmellSource"
    public void OnSmellSource(Vector3 smellPos)
    {
        // Evita spam si ya consideró una gota hace poco
        if (Time.time - lastSmellConsiderTime < smellConsiderCooldown)
            return;
        lastSmellConsiderTime = Time.time;

        if (prioritizePlayerOverSmell && state == State.Chase && player != null)
            return;

        // Si no hay jugador visible, las gotas sirven para investigar
        lastSeenTime = Time.time;
        lastInterestingPoint = smellPos;

        if (state != State.Chase)
            state = State.Investigate;

        SetDestination(smellPos);
    }

    // Mensaje recibido de otro zombi que vio al jugador
    public void OnAllyDetectedPlayer(Transform playerTransform)
    {
        if (!player) player = playerTransform;
        lastSeenTime = Time.time;
        lastInterestingPoint = player.position;
        state = State.Chase;
        SetDestination(player.position);
    }

    // Conexión con el SmellSensor
    void OnSmellDetected(Vector3 pos) => OnSmellSource(pos);

    // Avisa a zombis cercanos usando BroadcastMessage
    void AlertAllies(Transform playerTransform)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, allyAlertRadius, zombieLayer);
        foreach (var h in hits)
        {
            var root = h.transform.root.gameObject;
            root.BroadcastMessage("OnAllyDetectedPlayer", playerTransform, SendMessageOptions.DontRequireReceiver);
        }
    }

    // Gizmo de depuración
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, allyAlertRadius);
    }
}
