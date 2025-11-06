using UnityEngine;

public class BloodEmitter : MonoBehaviour
{
    [Header("Blood prefab (tag = SmellSource, Trigger Collider)")]
    public GameObject bloodPrefab;

    [Header("Intervalo de emisión (segundos)")]
    public float minInterval = 1f;
    public float maxInterval = 2f;

    [Header("Movimiento")]
    public float moveThreshold = 0.05f;   // distancia mínima para considerarse “en movimiento”
    public float yOffset = 0.05f;         // subir un poco la gota sobre el suelo

    private float nextTime;
    private Vector3 lastPos;

    void Start()
    {
        lastPos = transform.position;
        ScheduleNext();
    }

    void Update()
    {
        if (!bloodPrefab) return;

        float moved = (transform.position - lastPos).magnitude;
        if (Time.time >= nextTime && moved >= moveThreshold)
        {
            Vector3 spawnPos = transform.position;
            spawnPos.y += yOffset;
            Instantiate(bloodPrefab, spawnPos, Quaternion.identity);
            lastPos = transform.position;
            ScheduleNext();
        }
        else if (moved >= moveThreshold)
        {
            lastPos = transform.position; // actualiza para evitar “acumular” distancia
        }
    }

    void ScheduleNext()
    {
        nextTime = Time.time + Random.Range(minInterval, maxInterval);
    }
}
