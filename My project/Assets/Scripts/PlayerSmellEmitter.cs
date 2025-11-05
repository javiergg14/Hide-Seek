using UnityEngine;

public class PlayerSmellEmitter : MonoBehaviour
{
    [Header("Prefab & reglas")]
    public GameObject bloodPrefab;        // Prefab "BloodMarker" con tag SmellSource
    public float minInterval = 1f;
    public float maxInterval = 2f;

    [Header("Movimiento")]
    public float minDistanceBetweenDrops = 0.75f; // distancia mínima recorrida para soltar otra gota
    public float moveEpsilon = 0.01f;             // umbral para considerar que te mueves
    public float dropOffsetY = 0.05f;

    private float nextTime;
    private Vector3 lastDropPos;
    private bool hasDroppedOnce = false;

    void Start() { ScheduleNext(); }

    void Update()
    {
        // ¿Te estás moviendo realmente?
        float moved = (transform.position - (hasDroppedOnce ? lastDropPos : transform.position)).sqrMagnitude;

        bool movedEnough = moved >= (hasDroppedOnce ? minDistanceBetweenDrops * minDistanceBetweenDrops : moveEpsilon);

        if (Time.time >= nextTime && movedEnough)
        {
            Vector3 p = transform.position; p.y += dropOffsetY;
            Instantiate(bloodPrefab, p, Quaternion.identity);

            lastDropPos = transform.position;
            hasDroppedOnce = true;
            ScheduleNext();
        }
    }

    void ScheduleNext()
    {
        nextTime = Time.time + Random.Range(minInterval, maxInterval);
    }
}
