using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class ZombieAI : MonoBehaviour
{
    public enum State { Idle, Patrol, Investigate, Chase }
    public State state = State.Patrol;

    public NavMeshAgent agent;
    public ZombieVision vision;
    public Transform[] patrolPoints;
    public float chaseSpeed = 2.2f;
    public float patrolSpeed = 1.4f;
    public float closeEnough = 0.4f;
    public float alertRadius = 8f;  
    public float replanCooldown = 0.25f;

    Vector3 investigatePos;
    int patrolIndex = 0;
    float lastSetDestTime = -999f;

    void Awake()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        agent.stoppingDistance = 0.2f;
    }

    void Start()
    {
        StartCoroutine(Brain());
    }

    IEnumerator Brain()
    {
        while (true)
        {
            switch (state)
            {
                case State.Idle: ThinkIdle(); break;
                case State.Patrol: ThinkPatrol(); break;
                case State.Investigate: ThinkInvestigate(); break;
                case State.Chase: ThinkChase(); break;
            }
            yield return new WaitForSeconds(0.05f);
        }
    }

    void ThinkIdle()
    {
        if (vision && vision.PlayerVisible) ToChase(vision.LastSeenPos);
    }

    void ThinkPatrol()
    {
        agent.speed = patrolSpeed;

        if (vision && vision.PlayerVisible)
        {
            ToChase(vision.LastSeenPos);
            return;
        }

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            if (!agent.pathPending && agent.remainingDistance < closeEnough)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                SafeSetDestination(patrolPoints[patrolIndex].position);
            }
            else if (!agent.hasPath)
            {
                SafeSetDestination(patrolPoints[patrolIndex].position);
            }
        }
    }

    void ThinkInvestigate()
    {
        agent.speed = patrolSpeed * 1.2f;

        if (vision && vision.PlayerVisible)
        {
            ToChase(vision.LastSeenPos);
            return;
        }

        if (!agent.pathPending && agent.remainingDistance < closeEnough)
        {
            state = State.Patrol;
        }
    }

    void ThinkChase()
    {
        agent.speed = chaseSpeed;

        if (vision && vision.PlayerVisible)
        {
            SafeSetDestination(vision.LastSeenPos);

            BroadcastAlert(vision.LastSeenPos);
        }
        else
        {
            state = State.Investigate;
            SafeSetDestination(investigatePos);
        }
    }

    void BroadcastAlert(Vector3 pos)
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, alertRadius);
        foreach (var c in cols)
        {
            var other = c.GetComponentInParent<ZombieAI>();
            if (other != null && other != this)
                other.gameObject.BroadcastMessage("OnPlayerSpotted", pos, SendMessageOptions.DontRequireReceiver);
        }
    }

    void OnPlayerSpotted(Vector3 pos)
    {
        ToChase(pos);
    }

    public void SetInvestigateTarget(Vector3 pos)
    {
        investigatePos = pos;
        state = State.Investigate;
        SafeSetDestination(investigatePos);
    }

    void ToChase(Vector3 pos)
    {
        investigatePos = pos;
        state = State.Chase;
        SafeSetDestination(investigatePos);
    }

    void SafeSetDestination(Vector3 p)
    {
        if (Time.time - lastSetDestTime < replanCooldown) return;
        agent.SetDestination(p);     
        lastSetDestTime = Time.time;
    }
}
