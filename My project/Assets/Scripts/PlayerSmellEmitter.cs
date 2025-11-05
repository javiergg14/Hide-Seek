using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class PlayerSmellEmitter : MonoBehaviour
{
    public GameObject bloodSmellPrefab;
    public float minInterval = 1.0f;
    public float maxInterval = 2.0f;
    public float minSpeedToEmit = 0.1f;
    public int maxActiveSmells = 2;   

    NavMeshAgent agent;
    float nextDropTime;
    List<GameObject> activeSmells = new List<GameObject>();

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        ScheduleNext();
    }

    void Update()
    {
        activeSmells.RemoveAll(s => s == null);

        if (agent.velocity.sqrMagnitude < minSpeedToEmit * minSpeedToEmit)
            return;

        if (Time.time >= nextDropTime && activeSmells.Count < maxActiveSmells)
        {
            var newSmell = Instantiate(
                bloodSmellPrefab,
                transform.position + Vector3.down * 0.1f,
                Quaternion.identity
            );

            activeSmells.Add(newSmell);
            ScheduleNext();
        }
    }

    void ScheduleNext()
    {
        nextDropTime = Time.time + Random.Range(minInterval, maxInterval);
    }
}
